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

        private TextAsset _activeLevelJson;
        private string _activeLevelJsonPath;
        private LevelJsonData _activeLevel;
        private LevelEditorMode _editMode;
        private Vector2 _scrollPosition;
        private BlockShapeRegistry _shapeRegistry;

        private BlockColor _selectedDoorColor = BlockColor.Red;
        private BlockColor _selectedBlockColor = BlockColor.Red;
        private BlockMovementConstraint _selectedBlockMovementConstraint = BlockMovementConstraint.Free;
        private BlockShapeJsonData _selectedBlockShape;
        private readonly List<Vector2Int> _doorCellsBuffer = new List<Vector2Int>(8);
        private readonly List<Vector2Int> _frameCellsBuffer = new List<Vector2Int>(48);
       
        private readonly HashSet<Vector2Int> _blockedCellLookup = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _doorIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _blockIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly List<string> _layoutValidationIssues = new List<string>(16);
        private readonly BoardOccupancyMap _validationOccupancyMap = new BoardOccupancyMap();
        private bool _gridLookupCacheDirty = true;
        private readonly List<string> _projectJsonPaths = new List<string>(128);
        private readonly Dictionary<string, int> _projectJsonIndexByPath = new Dictionary<string, int>(128);
        private string[] _projectJsonOptions = { "None" };
        private bool _projectJsonCacheDirty = true;

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
            if (GUILayout.Button("Create New Level JSON", GUILayout.Height(24f)))
            {
                CreateNewLevelJson();
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
        }

        private void MarkProjectJsonCacheDirty()
        {
            _projectJsonCacheDirty = true;
        }

    }
}
