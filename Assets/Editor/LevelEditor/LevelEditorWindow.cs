using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow : EditorWindow
    {
        private const float GridCellPixelSize = 26f;
        private const string ShapeJsonFolder = "Assets/Data/BlockShapes";
        private const string LevelJsonFolder = "Assets/Data/LevelsJson";
        private static readonly BlockColor[] AllBlockColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
        private static readonly string[] EditModeLabels = { "Blocked Cells", "Doors", "Blocks" };
        private static readonly Color BlockedCellColor = new(0.22f, 0.22f, 0.22f);
        private static readonly Color FrameCellColor = new(0.33f, 0.36f, 0.49f);
        private static readonly Color EmptyCellColor = new(0.9f, 0.9f, 0.9f);
        private static readonly GUIContent DoorMustBeEdgeNotification = new("Door sadece kenar hücresine konabilir.");
        private static readonly GUIContent DoorCannotBeCornerNotification = new("Door kose hucreye konamaz. Kosenin yanindaki kenar hucreyi sec.");
        private static readonly GUIContent DoorCannotBePlacedNotification = new("Door bu hücreye eklenemiyor.");
        private static readonly GUIContent ShapeReplaceCollisionNotification = new("Shape guncellenemedi: Cakisiyor veya grid disina tasiyor.");

        private TextAsset _activeLevelJson;
        private string _activeLevelJsonPath;
        private LevelJsonData _activeLevel;
        private LevelEditorMode _editMode;
        private Vector2 _scrollPosition;
        private BlockShapeRegistry _shapeRegistry;

        private BlockColor _selectedDoorColor = BlockColor.Red;
        private BlockColor _selectedBlockColor = BlockColor.Red;
        private BlockFeature _selectedBlockFeatures = BlockFeature.Default;
        private BlockShapeJsonData _selectedBlockShape;
        private readonly List<Vector2Int> _doorCellsBuffer = new List<Vector2Int>(8);
        private readonly List<Vector2Int> _frameCellsBuffer = new List<Vector2Int>(48);
       
        private readonly HashSet<Vector2Int> _blockedCellLookup = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _doorIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _blockIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly HashSet<Vector2Int> _cellSelectionBuffer = new HashSet<Vector2Int>();
        private readonly List<string> _layoutValidationIssues = new List<string>(16);
        private readonly BoardOccupancyMap _validationOccupancyMap = new BoardOccupancyMap();
        private bool _gridLookupCacheDirty = true;
        private readonly List<string> _projectJsonPaths = new List<string>(128);
        private readonly Dictionary<string, int> _projectJsonIndexByPath = new Dictionary<string, int>(128);
        private readonly Dictionary<string, int> _shapeOptionIndexByKey = new Dictionary<string, int>(64, StringComparer.Ordinal);
        private string[] _shapeOptionLabels = Array.Empty<string>();
        private string[] _availableColorOptionLabels = Array.Empty<string>();
        private int _availableColorOptionSignature = int.MinValue;
        private string[] _projectJsonOptions = { "None" };
        private bool _projectJsonCacheDirty = true;
        private bool _shapeRegistryCacheDirty = true;
        private int _newLevelNumber = 1;

        [MenuItem("Tools/Color Block Jam/Level Editor")]
        private static void OpenWindow()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(920f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += HandleProjectAssetsChanged;
            MarkProjectJsonCacheDirty();
            MarkShapeRegistryCacheDirty();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectAssetsChanged;
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_activeLevel == null)
            {
                EditorGUILayout.HelpBox("Önce bir level JSON dosyası seç veya yeni oluştur.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawBaseSettings();
            EditorGUILayout.Space(10f);
            DrawAvailabilitySettings();
            EditorGUILayout.Space(10f);
            DrawEditModeToolbar();
            EditorGUILayout.Space(8f);
            DrawModeSettings();
            EditorGUILayout.Space(8f);
            DrawGridEditor();
            EditorGUILayout.Space(8f);
            DrawLayoutValidationReport();
            EditorGUILayout.Space(12f);
            DrawBlockList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EnsureShapeRegistryLoaded();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Color Block Jam - Data Driven Level Editor", EditorStyles.boldLabel);

            DrawActiveLevelJsonSelector();

            int shapeCount = _shapeRegistry != null ? _shapeRegistry.Shapes.Count : 0;
            EditorGUILayout.LabelField("Shape JSON Count", shapeCount.ToString());

            EditorGUILayout.BeginHorizontal();
            _newLevelNumber = Mathf.Max(
                1,
                EditorGUILayout.IntField("New Level", _newLevelNumber, GUILayout.Width(170f)));

            if (GUILayout.Button("Create New Level JSON", GUILayout.Height(24f)))
            {
                CreateNewLevelJson(_newLevelNumber);
            }

            bool canSave = _activeLevel != null && !string.IsNullOrWhiteSpace(_activeLevelJsonPath);
            EditorGUI.BeginDisabledGroup(!canSave);
            if (GUILayout.Button("Save JSON", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                WriteActiveLevelToJson();
            }

            EditorGUI.EndDisabledGroup();

            if (_activeLevelJson != null && GUILayout.Button("Ping JSON", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                EditorGUIUtility.PingObject(_activeLevelJson);
            }

            if (GUILayout.Button("Reload Shape JSON", GUILayout.Height(24f), GUILayout.Width(150f)))
            {
                EnsureShapeRegistryLoaded(true);
                if (_activeLevelJson != null)
                {
                    LoadLevelFromJsonAsset(_activeLevelJson);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawActiveLevelJsonSelector()
        {
            EnsureProjectJsonCache();
            int selectedIndex = ResolveSelectedProjectJsonIndex();

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Active Level JSON", selectedIndex, _projectJsonOptions);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            string nextPath = nextIndex > 0 ? _projectJsonPaths[nextIndex - 1] : string.Empty;
            LoadLevelFromJsonPath(nextPath);
        }

        private int ResolveSelectedProjectJsonIndex()
        {
            if (string.IsNullOrWhiteSpace(_activeLevelJsonPath))
            {
                return 0;
            }

            return _projectJsonIndexByPath.TryGetValue(_activeLevelJsonPath, out int selectedIndex)
                ? selectedIndex
                : 0;
        }

        private void HandleProjectAssetsChanged()
        {
            MarkProjectJsonCacheDirty();
            MarkShapeRegistryCacheDirty();
        }

        private void MarkProjectJsonCacheDirty()
        {
            _projectJsonCacheDirty = true;
        }

        private void MarkShapeRegistryCacheDirty()
        {
            _shapeRegistryCacheDirty = true;
        }

    }
}
