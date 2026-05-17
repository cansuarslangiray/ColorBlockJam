using System.Collections.Generic;
using Runtime.Data;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private BoardController boardController;

        [SerializeField] private LevelJsonData sourceLevel;
        [SerializeField] private BlockVisualProfile visualProfile;
        [SerializeField] private BoardGameplayConfig gameplayConfig;
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

        [Header("Animation Fallback")] [SerializeField, Min(0.05f)]
        private float blockMoveDuration = 0.14f;

        [SerializeField] private AnimationCurve blockMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
        private readonly Dictionary<int, Coroutine> _blockMoveRoutineById = new();
        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();

        private PooledVisual _backdropObject;
        private bool _baseGridInitialized;
        private Vector2Int _baseGridSize;
        private bool _loggedMissingBlockCellPrefab;
        private bool _loggedPrimitiveFallback;

        private Transform BoardRoot => boardRoot;
        private Transform BlocksRoot => blocksRoot;
        private Vector2 BoardOrigin => boardController.BoardOrigin;
        private float CellSize => Mathf.Max(0.01f, boardController.CellSize);

        private BoardGameplayConfig ResolvedGameplayConfig => gameplayConfig ? gameplayConfig :
            boardController.GameplayConfig;

        private float MoveDuration => Mathf.Max(0.05f,
            ResolvedGameplayConfig ? ResolvedGameplayConfig.blockMoveDuration : blockMoveDuration);

        private AnimationCurve MoveCurve => ResolvedGameplayConfig && ResolvedGameplayConfig.blockMoveCurve != null
            ? ResolvedGameplayConfig.blockMoveCurve
            : blockMoveCurve;

        private float ExitDuration => Mathf.Max(0.05f,
            ResolvedGameplayConfig ? ResolvedGameplayConfig.doorExitDuration : doorExitDuration);

        private float ExitTravelInCells => Mathf.Max(0.2f,
            ResolvedGameplayConfig ? ResolvedGameplayConfig.doorExitTravelInCells : doorExitTravelInCells);

        private AnimationCurve ExitMoveCurve =>
            ResolvedGameplayConfig && ResolvedGameplayConfig.doorExitMoveCurve != null
                ? ResolvedGameplayConfig.doorExitMoveCurve
                : doorExitMoveCurve;

        private AnimationCurve ExitScaleCurve =>
            ResolvedGameplayConfig && ResolvedGameplayConfig.doorExitScaleCurve != null
                ? ResolvedGameplayConfig.doorExitScaleCurve
                : doorExitScaleCurve;

        private float ExitMinScaleMultiplier => Mathf.Clamp01(
            ResolvedGameplayConfig ? ResolvedGameplayConfig.doorExitMinScaleMultiplier : doorExitMinScaleMultiplier);

        private void OnEnable()
        {
            SubscribeBoardEvents();
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
        }

        public void BuildForLevel(LevelJsonData levelData)
        {
            if (levelData == null)
            {
                return;
            }

            sourceLevel = levelData;
            StopAllBlockRoutines();
            _activeBlockRootById.Clear();
            UnsubscribeBoardEvents();

            EnsureBaseGridSize(levelData.gridDimensions);
            EnsureBoardPool(_baseGridSize);
            ApplyBoardVisuals(levelData);

            var sourceBlockCount = GetSourceBlockCount(levelData);
            EnsureBlockPool(sourceBlockCount);
            ApplyBlockVisuals(levelData);

            SubscribeBoardEvents();
        }

        [ContextMenu("Build Blocks From Level JSON")]
        public void BuildBlocksFromLevelJson()
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

            _baseGridSize = new Vector2Int(Mathf.Max(_baseGridSize.x, levelGridSize.x),
                Mathf.Max(_baseGridSize.y, levelGridSize.y));
        }

        private static int GetSourceBlockCount(LevelJsonData levelData)
        {
            return levelData != null && levelData.blocks != null ? levelData.blocks.Count : 0;
        }
    }
}
