using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private BoardController boardController;

        [SerializeField] private LevelData sourceLevel;
        [SerializeField] private BlockVisualProfile visualProfile;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform blocksRoot;

        [Header("Prefab References")] [SerializeField]
        private GameObject gridCellPrefab;

        [SerializeField] private GameObject borderPrefab;
        [SerializeField] private GameObject backdropPrefab;
        [SerializeField] private GameObject doorPrefab;

        [Header("Material References")] [SerializeField]
        private List<BlockColorMaterialEntry> materialsByColor = new();

        [Header("Runtime Naming")] [SerializeField]
        private bool applyRuntimeNames;

        [SerializeField] private string boardBackdropName = "BoardBackdrop";
        [SerializeField] private string gridCellNamePrefix = "GridCell";
        [SerializeField] private string borderNamePrefix = "Border";
        [SerializeField] private string doorNamePrefix = "Door";
        [SerializeField] private string blockRootNamePrefix = "BlockRoot";
        [SerializeField] private string blockCellNamePrefix = "BlockCell";

        [Header("Board Layout")] [SerializeField]
        private float boardCellGap = 0.08f;

        [SerializeField, Min(0.01f)] private float boardCellDepthInCells = 0.05f;
        [SerializeField] private float boardCellsZOffset = 0.75f;
        [SerializeField] private float boardBackdropPaddingInCells = 0.4f;
        [SerializeField] private float boardBackdropZOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells = 0.48f;
        [SerializeField, Min(0f)] private float edgeFramePaddingInCells = 0.03f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells = 0.14f;
        [SerializeField, Min(0f)] private float doorInsetInCells = 0.02f;
        [SerializeField, Min(0f)] private float doorDepthBiasFromFrame = 0.02f;

        [Header("Block Layout")] [SerializeField, Range(0.75f, 1f)]
        private float blockCellVisualScale = 0.92f;

        [SerializeField, Min(0.01f)] private float blockLayerForwardOffsetFromGrid = 0.24f;

        [SerializeField, Min(0f)] private float blockMoveSmoothingSpeed = 18f;
        [SerializeField, Min(0.05f)] private float doorExitDuration = 0.32f;
        [SerializeField, Min(0.2f)] private float doorExitTravelInCells = 1.15f;
        [SerializeField] private AnimationCurve doorExitMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve doorExitScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField, Range(0f, 1f)] private float doorExitMinScaleMultiplier = 0.05f;

        private readonly Dictionary<Vector2Int, PooledVisual> _gridCellPoolByCell = new();
        private readonly List<PooledVisual> _borderPool = new();
        private readonly List<PooledVisual> _doorPool = new();
        private readonly List<BlockRootView> _blockRootPool = new();

        private readonly Dictionary<int, BlockRootView> _activeBlockRootById = new();
        private readonly Dictionary<int, Vector3> _blockTargetPositionById = new();
        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();
        private readonly List<int> _reachedTargetIds = new();
        private readonly Dictionary<BlockColor, Material> _fallbackDoorMaterialByColor = new();
        private readonly Dictionary<BlockColor, Material> _fallbackBlockMaterialByColor = new();

        private PooledVisual _backdropObject;
        private bool _baseGridInitialized;
        private Vector2Int _baseGridSize;

        private Transform BoardRoot => boardRoot != null ? boardRoot : transform;
        private Transform BlocksRoot => blocksRoot != null ? blocksRoot : transform;
        private Vector2 BoardOrigin => boardController != null ? boardController.BoardOrigin : Vector2.zero;
        private float CellSize => Mathf.Max(0.01f, boardController != null ? boardController.CellSize : 1f);

        private void OnEnable()
        {
            SubscribeBoardEvents();
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
        }

        public void BuildForLevel(LevelData levelData)
        {
            if (!levelData)
            {
                return;
            }

            sourceLevel = levelData;
            StopAllBlockRoutines();
            _activeBlockRootById.Clear();
            _blockTargetPositionById.Clear();
            UnsubscribeBoardEvents();

            EnsureBaseGridSize(levelData.gridDimensions);
            EnsureBoardPool(_baseGridSize);
            ApplyBoardVisuals(levelData);

            var sourceBlockCount = GetSourceBlockCount(levelData);
            EnsureBlockPool(sourceBlockCount);
            ApplyBlockVisuals(levelData);

            SubscribeBoardEvents();
        }

        [ContextMenu("Build Blocks From Level Data")]
        public void BuildBlocksFromLevelData()
        {
            BuildForLevel(sourceLevel);
        }

        private void EnsureBaseGridSize(Vector2Int levelGridSize)
        {
            if (!_baseGridInitialized)
            {
                _baseGridSize = levelGridSize;
                _baseGridInitialized = true;
                return;
            }

            _baseGridSize = new Vector2Int(Mathf.Max(_baseGridSize.x, levelGridSize.x), Mathf.Max(_baseGridSize.y, levelGridSize.y));
        }

        private static int GetSourceBlockCount(LevelData levelData)
        {
            return levelData && levelData.blocks != null ? levelData.blocks.Count : 0;
        }

        private void OnDestroy()
        {
            _fallbackDoorMaterialByColor.Clear();
            _fallbackBlockMaterialByColor.Clear();
        }

    }
}
