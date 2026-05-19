using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Animations;
using Runtime.Controllers.BlockSceneBuilder.Blocks;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Conditions;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using Runtime.Managers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private BoardController boardController;

        [SerializeField] private BlockScenePoolManager poolManager;
        [SerializeField] private AudioManager audioManager;

        [Header("Material References")] [SerializeField]
        private List<BlockColorMaterialEntry> materialsByColor = new();
        
        private readonly BlockViewRuntimePool _blockViewPool = new();

        [Header("Board Layout")] [SerializeField]
        private float boardCellsZOffset = 0.75f;

        [SerializeField] private float boardBackdropZOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells = 0.36f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells = 0.33f;
        [SerializeField, Min(0f)] private float doorInsetInCells = 0.02f;
        [SerializeField, Min(0f)] private float doorDepthBiasFromFrame = 0.02f;
        [SerializeField, Min(0f)] private float blockedCellZOffsetFromGrid = 0.03f;
        
        [SerializeField, Min(0.01f)] private float blockLayerForwardOffsetFromGrid = 0.24f;

        [Header("Block Outline")] [SerializeField, Range(0.2f, 1f)]
        private float outlineIdleDarkenFactor = 0.74f;
        
        [Header("Block Movement")] [SerializeField, Min(2f)]
        private float blockMoveSpeedInCellsPerSecond = 16f;

        [SerializeField, Min(0.001f)] private float blockMoveSnapDistanceInCells = 0.02f;

        [Header("Block Indicators")] [SerializeField]
        private bool showBlockConditionIndicators = true;
        [SerializeField] private float conditionIndicatorVerticalMovementRotationDegrees = 90f;
        [SerializeField, Min(0.05f)] private float minClearedUnlockColorTransitionDuration = 0.16f;

        [Header("Door Exit UX")] [SerializeField, Min(0f)]
        private float doorEntryAdvanceInCells = 0.2f;

        [SerializeField, Min(0f)] private float doorExitForwardTravelInCells = 0.9f;
        [SerializeField, Min(0.05f)] private float doorPassThroughDuration = 0.22f;
        [SerializeField, Min(0f)] private float doorMatchDipInCells = 0.08f;
        [SerializeField, Min(0.02f)] private float doorMatchFxDuration = 0.14f;

        private readonly Dictionary<Vector2Int, GameObject> _gridCellPoolByCell = new();
        private readonly List<Vector2Int> _resolvedBlockedCells = new(256);
        private readonly List<GameObject> _blockedCellPool = new();
        private readonly List<GameObject> _doorPool = new();
        private readonly Dictionary<string, int> _requiredBlockRootCountByKey = new(System.StringComparer.Ordinal);
        private readonly ConditionIndicatorPresenter _conditionIndicatorPresenter = new();
        private readonly BlockOutlinePresenter _blockOutlinePresenter = new();
        private readonly BlockExitFxController _blockExitFxController = new();
        private readonly BoardVisualBuilder _boardVisualBuilder = new();
        private readonly BlockVisualPresenter _blockVisualPresenter = new();

        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();
        private readonly Dictionary<int, Coroutine> _blockMoveRoutineById = new();
        private readonly Dictionary<int, Coroutine> _blockConditionUnlockTransitionRoutineById = new();
        private readonly Dictionary<int, Vector3> _blockMoveTargetWorldById = new();
        private IReadOnlyList<GameObject> _borderObjects = System.Array.Empty<GameObject>();
        private GameObject _backdropObject;
        private LayoutMetrics _currentLayout;
        private bool _hasCurrentLayout;
        private MaterialPropertyBlock _fxRendererPropertyBlock;
        private MaterialPropertyBlock _blockColorPropertyBlock;

        private Vector2 BoardOrigin => boardController.BoardOrigin;
        private float CellSize => Mathf.Max(0.01f, boardController.CellSize);

        private LayoutMetrics ResolveLayoutMetrics()
        {
            var cellSize = CellSize;
            var gridZ = Mathf.Abs(boardCellsZOffset);
            // Keep frame outside playable cells without visually reading as +2 grid cells.
            var frameThicknessInCells = Mathf.Clamp(edgeFrameThicknessInCells, 0.01f, 0.6f);
            var frameThickness = frameThicknessInCells * cellSize;
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize * 2f);
            var borderZ = gridZ - 0.01f;
            var doorDepth = frameDepth * 1.08f;
            var doorDepthBias = Mathf.Max(0.005f, doorDepthBiasFromFrame);

            return new LayoutMetrics(BoardOrigin, cellSize, gridZ,
                gridZ - Mathf.Max(0.01f, blockLayerForwardOffsetFromGrid), frameThickness,
                frameDepth, borderZ, doorDepth, borderZ - doorDepthBias);
        }


        private void OnEnable()
        {
            SubscribeBoardEvents();
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
            _hasCurrentLayout = false;
        }

        public virtual void BuildForLevel(LevelDefinition levelData)
        {
            CacheRequiredBlockRootCounts(levelData);
            StopAllBlockRoutines();
            ReleaseActiveBlockViewsToPool();
            UnsubscribeBoardEvents();

            ConfigurePoolsFromManager(levelData);

            var layout = ResolveLayoutMetrics();
            CacheCurrentLayout(layout);

            ApplyBoardVisuals(levelData, layout);

            ApplyBlockVisuals(levelData, layout);

            SubscribeBoardEvents();
        }

        private void ConfigurePoolsFromManager(LevelDefinition levelData)
        {
            poolManager.RefreshPools(validateAuthoringTargets: false);
            EnsurePoolCoverage(levelData);
            BindGridCellPool(poolManager.GridCellObjects, levelData.gridDimensions);
            BindBlockedCellPool(poolManager.BlockedCellObjects);
            BindBoardVisualReferences(poolManager.BorderObjects, poolManager.BackdropObject);
            BindDoorPool(poolManager.DoorBindings);
            BindBlockRootPools();
        }

        private void EnsurePoolCoverage(LevelDefinition levelData)
        {
            var gridSize = levelData.gridDimensions;
            var requiredGridCellCount = Mathf.Max(0, gridSize.x * gridSize.y);
            poolManager.EnsureGridCellPoolSize(requiredGridCellCount);
            var openings = levelData.GetDoorOpenings();
            Runtime.Controllers.BoardRuntimeState.CollectBlockedCellsForLayout(gridSize, levelData.blockedCells, openings,
                _resolvedBlockedCells);
            poolManager.EnsureBlockedCellPoolSize(_resolvedBlockedCells.Count);
            poolManager.EnsureDoorPoolSize(openings?.Count ?? 0);

            poolManager.EnsureBlockPoolSizes(_requiredBlockRootCountByKey);
        }

        private void BindGridCellPool(IReadOnlyList<GameObject> gridCellObjects, Vector2Int levelGridSize)
        {
            _gridCellPoolByCell.Clear();
            if (gridCellObjects == null || gridCellObjects.Count == 0)
            {
                return;
            }

            for (var i = 0; i < gridCellObjects.Count; i++)
            {
                var pooledCell = gridCellObjects[i];
                if (pooledCell)
                {
                    SetActiveIfChanged(pooledCell, false);
                }
            }

            var width = levelGridSize.x;
            var height = levelGridSize.y;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var maxCellCount = width * height;
            var poolCount = Mathf.Min(maxCellCount, gridCellObjects.Count);

            for (var index = 0; index < poolCount; index++)
            {
                var gridCellObject = gridCellObjects[index];
                if (!gridCellObject)
                {
                    continue;
                }

                var cell = new Vector2Int(index % width, index / width);
                _gridCellPoolByCell[cell] = gridCellObject;
            }
        }

        private void BindBoardVisualReferences(IReadOnlyList<GameObject> borderObjects, GameObject backdropObject)
        {
            _borderObjects = borderObjects ?? System.Array.Empty<GameObject>();
            for (var i = 0; i < _borderObjects.Count; i++)
            {
                var borderObject = _borderObjects[i];
                if (borderObject)
                {
                    SetActiveIfChanged(borderObject, true);
                }
            }

            _backdropObject = backdropObject;
            if (_backdropObject)
            {
                SetActiveIfChanged(_backdropObject, false);
            }
        }

        private void BindBlockedCellPool(IReadOnlyList<GameObject> blockedCellObjects)
        {
            _blockedCellPool.Clear();

            var blockedCount = blockedCellObjects?.Count ?? 0;
            for (var i = 0; i < blockedCount; i++)
            {
                var blockedCellObject = blockedCellObjects[i];
                if (!blockedCellObject)
                {
                    continue;
                }

                SetActiveIfChanged(blockedCellObject, false);
                _blockedCellPool.Add(blockedCellObject);
            }
        }

        private void BindDoorPool(IReadOnlyList<DoorPoolBindings> doorBindings)
        {
            ResetDoorRuntimeCache();
            _doorPool.Clear();
            var doorCount = doorBindings?.Count ?? 0;
            for (var i = 0; i < doorCount; i++)
            {
                var doorBinding = doorBindings[i];
                if (!doorBinding || !doorBinding.DoorObject)
                {
                    continue;
                }

                var doorObject = doorBinding.DoorObject;
                _doorPool.Add(doorObject);
                CacheDoorRuntimeReferences(_doorPool.Count - 1, doorBinding);
                SetActiveIfChanged(doorObject, false);
            }
        }

        private void BindBlockRootPools()
        {
            _blockViewPool.Rebind(poolManager.BlockBindingsByKey, SetActiveIfChanged);
        }

        private void CacheRequiredBlockRootCounts(LevelDefinition levelData)
        {
            _requiredBlockRootCountByKey.Clear();
            var sourceBlocks = levelData.blocks;
            if (sourceBlocks == null)
            {
                return;
            }

            for (var i = 0; i < sourceBlocks.Count; i++)
            {
                if (!boardController.TryGetRuntimeBlock(i, out var runtimeBlock))
                {
                    continue;
                }

                var poolKey = sourceBlocks[i].ResolvePoolKey();

                _requiredBlockRootCountByKey.TryGetValue(poolKey, out var existingCount);
                _requiredBlockRootCountByKey[poolKey] = existingCount + 1;
            }
        }

        private void CacheCurrentLayout(in LayoutMetrics layout)
        {
            _currentLayout = layout;
            _hasCurrentLayout = true;
        }

        private LayoutMetrics GetCurrentLayout()
        {
            if (!_hasCurrentLayout)
            {
                CacheCurrentLayout(ResolveLayoutMetrics());
            }

            return _currentLayout;
        }
        
    }
}
