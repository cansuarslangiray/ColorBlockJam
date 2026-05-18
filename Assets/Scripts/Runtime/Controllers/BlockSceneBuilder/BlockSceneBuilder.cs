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

        [SerializeField] private BlockScenePoolManager poolManager;

        [Header("Material References")] [SerializeField]
        private List<BlockColorMaterialEntry> materialsByColor = new();

        private readonly Dictionary<BlockColor, Material> _configuredMaterialByColor = new();
        private bool _isConfiguredMaterialCacheDirty = true;

        [Header("Board Layout")] [SerializeField]
        private float boardCellGap = 0.08f;

        [SerializeField] private float boardCellsZOffset = 0.75f;
        [SerializeField] private float boardBackdropPaddingInCells = 0.4f;
        [SerializeField] private float boardBackdropZOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells = 0.48f;
        [SerializeField, Min(0f)] private float edgeFramePaddingInCells = 0.03f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells = 0.28f;
        [SerializeField, Min(0f)] private float doorInsetInCells = 0.02f;
        [SerializeField, Min(0f)] private float doorDepthBiasFromFrame = 0.02f;

        [Header("Block Layout")] [SerializeField, Range(0.75f, 1f)]
        private float blockCellVisualScale = 0.92f;

        [SerializeField, Min(0.01f)] private float blockLayerForwardOffsetFromGrid = 0.24f;

        private readonly Dictionary<Vector2Int, GameObject> _gridCellPoolByCell = new();
        private readonly List<GameObject> _doorPool = new();
        private readonly Dictionary<BlockShapeType, List<BlockRootView>> _inactiveBlockRootsByType = new();
        private readonly Dictionary<BlockShapeType, int> _requiredBlockRootCountByType = new();

        private readonly Dictionary<int, BlockRootView> _activeBlockRootById = new();
        private readonly Dictionary<int, Coroutine> _blockMoveRoutineById = new();
        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();

        private IReadOnlyList<GameObject> _borderObjects = System.Array.Empty<GameObject>();
        private GameObject _backdropObject;
        private GameObject _sharedBlockCellTemplate;
        private BoardGameplayConfig _gameplayConfig;
        private LayoutMetrics _currentLayout;
        private bool _hasCurrentLayout;

        private Vector2 BoardOrigin => boardController.BoardOrigin;
        private float CellSize => Mathf.Max(0.01f, boardController.CellSize);

        private float MoveDuration => Mathf.Max(0.05f, _gameplayConfig.blockMoveDuration);
        private AnimationCurve MoveCurve => _gameplayConfig.blockMoveCurve;
        private float ExitDuration => _gameplayConfig.doorExitDuration;
        private float ExitTravelInCells => _gameplayConfig.doorExitTravelInCells;
        private AnimationCurve ExitMoveCurve => _gameplayConfig.doorExitMoveCurve;
        private AnimationCurve ExitScaleCurve => _gameplayConfig.doorExitScaleCurve;
        private float ExitMinScaleMultiplier => _gameplayConfig.doorExitMinScaleMultiplier;

        private LayoutMetrics ResolveLayoutMetrics()
        {
            var cellSize = CellSize;
            var gridZ = Mathf.Abs(boardCellsZOffset);
            var frameThickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var framePadding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var borderZ = gridZ - 0.01f;
            var doorDepth = frameDepth * 1.08f;
            var doorDepthBias = Mathf.Max(0.005f, doorDepthBiasFromFrame);

            return new LayoutMetrics(BoardOrigin, cellSize, gridZ,
                gridZ - Mathf.Max(0.01f, blockLayerForwardOffsetFromGrid), frameThickness,
                frameDepth, framePadding, borderZ, doorDepth, borderZ - doorDepthBias);
        }

        private void OnEnable() => SubscribeBoardEvents();

        private void OnValidate()
        {
            _isConfiguredMaterialCacheDirty = true;
            _hasCurrentLayout = false;
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
            _sharedBlockCellTemplate = null;
            _gameplayConfig = null;
            _rendererCacheByObjectId.Clear();
            _hasCurrentLayout = false;
        }

        public void BuildForLevel(LevelJsonData levelData)
        {
            if (levelData == null)
            {
                return;
            }

            _gameplayConfig = boardController.GameplayConfig;

            CacheRequiredBlockRootCounts(levelData);
            StopAllBlockRoutines();
            UnsubscribeBoardEvents();

            ConfigurePoolsFromManager(levelData);

            var layout = ResolveLayoutMetrics();
            CacheCurrentLayout(layout);

            ApplyBoardVisuals(levelData, layout);

            ApplyBlockVisuals(levelData, layout);

            SubscribeBoardEvents();
        }

        private void ConfigurePoolsFromManager(LevelJsonData levelData)
        {
            poolManager.RefreshPools();
            EnsurePoolCoverage(levelData);
            BindGridCellPool(poolManager.GridCellObjects, levelData.gridDimensions);
            BindBoardVisualReferences(poolManager.BorderObjects, poolManager.BackdropObject);
            BindDoorPool(poolManager.DoorObjects);
            BindBlockRootPools();
        }

        private void EnsurePoolCoverage(LevelJsonData levelData)
        {
            var gridSize = levelData.gridDimensions;
            var requiredGridCellCount = Mathf.Max(0, gridSize.x * gridSize.y);
            poolManager.EnsureGridCellPoolSize(requiredGridCellCount);

            var openings = levelData.GetDoorOpenings();
            poolManager.EnsureDoorPoolSize(openings?.Count ?? 0);

            poolManager.EnsureBlockPoolSizes(_requiredBlockRootCountByType);
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

        private void BindDoorPool(IReadOnlyList<GameObject> doorObjects)
        {
            _doorPool.Clear();
            var doorCount = doorObjects?.Count ?? 0;
            for (var i = 0; i < doorCount; i++)
            {
                var doorObject = doorObjects[i];
                if (!doorObject)
                {
                    continue;
                }

                _doorPool.Add(doorObject);
                SetActiveIfChanged(doorObject, false);
            }
        }

        private void BindBlockRootPools()
        {
            _activeBlockRootById.Clear();
            _inactiveBlockRootsByType.Clear();
            _sharedBlockCellTemplate = null;

            var pooledRootIds = new HashSet<int>();
            foreach (var pair in poolManager.BlockObjectsByType)
            {
                AddBlockViewsFromPool(pair.Key, pair.Value, pooledRootIds);
            }
        }

        private void AddBlockViewsFromPool(
            BlockShapeType blockType,
            IReadOnlyList<GameObject> sourcePool,
            HashSet<int> pooledRootIds)
        {
            if (sourcePool == null)
            {
                return;
            }

            var sourceCount = sourcePool.Count;
            for (var i = 0; i < sourceCount; i++)
            {
                var rootObject = sourcePool[i];
                if (!rootObject)
                {
                    continue;
                }

                var rootId = rootObject.GetInstanceID();
                if (!pooledRootIds.Add(rootId))
                {
                    continue;
                }

                var blockView = new BlockRootView(rootObject)
                {
                    BlockType = blockType
                };
                CacheBlockCellPool(blockView);
                CacheBlockCellTemplate(blockView);
                GetOrCreateInactivePool(blockType).Add(blockView);

                SetActiveIfChanged(rootObject, false);
            }
        }

        private List<BlockRootView> GetOrCreateInactivePool(BlockShapeType blockType)
        {
            if (_inactiveBlockRootsByType.TryGetValue(blockType, out var pool))
            {
                return pool;
            }

            pool = new List<BlockRootView>(16);
            _inactiveBlockRootsByType[blockType] = pool;
            return pool;
        }

        private void CacheBlockCellPool(BlockRootView blockView)
        {
            blockView.Cells.Clear();
            if (blockView.RootTransform == null)
            {
                return;
            }

            var childCount = blockView.RootTransform.childCount;
            if (childCount <= 0)
            {
                var fallbackRootObject = blockView.RootObject;
                blockView.Cells.Add(fallbackRootObject);
                return;
            }

            for (var i = 0; i < childCount; i++)
            {
                var child = blockView.RootTransform.GetChild(i);
                if (!child)
                {
                    continue;
                }

                var cellObject = child.gameObject;
                blockView.Cells.Add(cellObject);
                SetActiveIfChanged(cellObject, false);
            }
        }

        private void CacheBlockCellTemplate(BlockRootView blockView)
        {
            if (_sharedBlockCellTemplate)
            {
                return;
            }

            if (blockView == null)
            {
                return;
            }

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                if (cellObject && cellObject != blockView.RootObject)
                {
                    _sharedBlockCellTemplate = cellObject;
                    return;
                }
            }
        }

        private static int GetSourceBlockCount(LevelJsonData levelData) =>
            levelData != null && levelData.blocks != null ? levelData.blocks.Count : 0;

        private void CacheRequiredBlockRootCounts(LevelJsonData levelData)
        {
            _requiredBlockRootCountByType.Clear();
            var sourceBlocks = levelData != null ? levelData.blocks : null;
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

                var blockType = sourceBlocks[i].ResolveBlockType(runtimeBlock.LocalCells?.Length ?? 1);
                _requiredBlockRootCountByType[blockType] =
                    _requiredBlockRootCountByType.GetValueOrDefault(blockType) + 1;
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
