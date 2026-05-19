using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Animations;
using Runtime.Controllers.BlockSceneBuilder.Blocks;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Conditions;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using Runtime.Domain.Enums;
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
        
        private readonly HashSet<BlockColor> _missingMaterialWarnings = new();
        private readonly BlockViewRuntimePool _blockViewPool = new();

        [Header("Board Layout")] [SerializeField]
        private float boardCellsZOffset = 0.75f;

        [SerializeField] private float boardBackdropZOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells = 0.48f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells = 0.28f;
        [SerializeField, Min(0f)] private float doorInsetInCells = 0.02f;
        [SerializeField, Min(0f)] private float doorDepthBiasFromFrame = 0.02f;
        [SerializeField, Min(0f)] private float blockedCellZOffsetFromGrid = 0.03f;

        [Header("Block Layout")] [SerializeField, Range(0.75f, 1f)]
        private float blockCellVisualScale = 1f;

        [SerializeField, Min(0.01f)] private float blockLayerForwardOffsetFromGrid = 0.24f;
        [SerializeField] private Vector3 blockRootScale = new(1f, 1f, 0.45f);

        [Header("Block Movement")] [SerializeField, Min(2f)]
        private float blockMoveSpeedInCellsPerSecond = 16f;

        [SerializeField, Min(0.001f)] private float blockMoveSnapDistanceInCells = 0.02f;

        [Header("Block Indicators")] [SerializeField]
        private bool showBlockConditionIndicators = true;

        [SerializeField, Min(0.01f)] private float indicatorCharacterSizeInCells = 0.16f;
        [SerializeField, Min(0f)] private float indicatorHeightOffsetInCells = 0.28f;
        [SerializeField] private float indicatorLocalZOffset = -0.05f;
        [SerializeField, Min(8)] private int indicatorFontSize = 36;
        [SerializeField] private Color indicatorTextColor = Color.white;
        [SerializeField] private Camera indicatorCamera;

        [Header("Particle FX")] [SerializeField]
        private List<ParticleSystem> doorExitBurstParticles = new();
        [SerializeField] private List<ParticleSystemRenderer> doorExitBurstParticleRenderers = new();

        [SerializeField, Min(0)] private int doorExitBurstPoolWarmupCount = 8;
        [SerializeField] private Material dragOutlineSourceMaterial;
        [SerializeField, Min(0f)] private float dragOutlineBaseOffsetInCells = 0.01f;
        [SerializeField, Min(0f)] private float dragOutlineGapInCells = 0.035f;
        [SerializeField] private float dragOutlineVerticalOffsetInCells = 0.03f;
        [SerializeField, Min(0.005f)] private float dragOutlineThicknessInCells = 0.095f;
        [SerializeField] private Color dragOutlineColor = new(1f, 1f, 1f, 0.9f);

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
        private readonly Dictionary<string, int> _requiredBlockCellCountByKey = new(System.StringComparer.Ordinal);
        private readonly ConditionIndicatorPresenter _conditionIndicatorPresenter = new();
        private readonly DragHighlightPresenter _dragHighlightPresenter = new();
        private readonly BlockExitFxController _blockExitFxController = new();
        private readonly BoardVisualBuilder _boardVisualBuilder = new();
        private readonly BlockVisualPresenter _blockVisualPresenter = new();

        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();
        private readonly Dictionary<int, Coroutine> _blockMoveRoutineById = new();
        private readonly Dictionary<int, Vector3> _blockMoveTargetWorldById = new();
        private IReadOnlyList<GameObject> _borderObjects = System.Array.Empty<GameObject>();
        private GameObject _backdropObject;
        private LayoutMetrics _currentLayout;
        private bool _hasCurrentLayout;
        private MaterialPropertyBlock _fxRendererPropertyBlock;
        private readonly List<ParticleSystem> _doorExitBurstParticlePool = new();
        private readonly Stack<ParticleSystem> _availableDoorExitBurstParticles = new();
        private readonly HashSet<int> _availableDoorExitBurstParticleIds = new();
        private readonly Dictionary<int, ParticleSystemRenderer> _doorExitBurstRendererByParticleId = new();
        private bool _hasLoggedDoorExitBurstPoolShortWarning;

        private Vector2 BoardOrigin => boardController.BoardOrigin;
        private float CellSize => Mathf.Max(0.01f, boardController.CellSize);

        private LayoutMetrics ResolveLayoutMetrics()
        {
            var cellSize = CellSize;
            var gridZ = Mathf.Abs(boardCellsZOffset);
            // Keep frame outside playable cells without visually reading as +2 grid cells.
            var frameThicknessInCells = Mathf.Clamp(edgeFrameThicknessInCells, 0.01f, 0.2f);
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

        private void OnValidate()
        {
            _missingMaterialWarnings.Clear();
            blockRootScale.x = Mathf.Max(0.01f, blockRootScale.x);
            blockRootScale.y = Mathf.Max(0.01f, blockRootScale.y);
            blockRootScale.z = Mathf.Max(0.01f, blockRootScale.z);
            blockMoveSpeedInCellsPerSecond = Mathf.Max(2f, blockMoveSpeedInCellsPerSecond);
            blockMoveSnapDistanceInCells = Mathf.Max(0.001f, blockMoveSnapDistanceInCells);
            indicatorCharacterSizeInCells = Mathf.Max(0.01f, indicatorCharacterSizeInCells);
            doorExitBurstPoolWarmupCount = Mathf.Max(0, doorExitBurstPoolWarmupCount);
            dragOutlineGapInCells = Mathf.Max(0f, dragOutlineGapInCells);
            dragOutlineBaseOffsetInCells = Mathf.Max(0f, dragOutlineBaseOffsetInCells);
            dragOutlineVerticalOffsetInCells = Mathf.Clamp(dragOutlineVerticalOffsetInCells, -0.25f, 0.25f);
            dragOutlineThicknessInCells = Mathf.Max(0.005f, dragOutlineThicknessInCells);
            doorPassThroughDuration = Mathf.Max(0.05f, doorPassThroughDuration);
            doorMatchDipInCells = Mathf.Max(0f, doorMatchDipInCells);
            doorMatchFxDuration = Mathf.Max(0.02f, doorMatchFxDuration);
            blockedCellZOffsetFromGrid = Mathf.Max(0f, blockedCellZOffsetFromGrid);
            _hasCurrentLayout = false;
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
            ReleaseRuntimeFxResources();
            _missingMaterialWarnings.Clear();
            _conditionIndicatorPresenter.ResetRuntimeState();
            _dragHighlightPresenter.ResetRuntimeResources();
            _hasCurrentLayout = false;
            _hasLoggedDoorExitBurstPoolShortWarning = false;
        }

        public virtual void BuildForLevel(LevelDefinition levelData)
        {
            if (levelData == null)
            {
                return;
            }

            if (!HasRequiredReferences())
            {
                Debug.LogError("BlockSceneBuilder requires BoardController and BlockScenePoolManager references.", this);
                return;
            }

            CacheRequiredBlockRootCounts(levelData);
            StopAllBlockRoutines();
            UnsubscribeBoardEvents();

            ConfigurePoolsFromManager(levelData);
            _hasLoggedDoorExitBurstPoolShortWarning = false;
            EnsureDoorExitBurstPoolCapacity(Mathf.Max(doorExitBurstPoolWarmupCount,
                levelData.GetDoorOpenings()?.Count ?? 0));

            var layout = ResolveLayoutMetrics();
            CacheCurrentLayout(layout);

            ApplyBoardVisuals(levelData, layout);

            ApplyBlockVisuals(levelData, layout);

            SubscribeBoardEvents();
        }

        private void ConfigurePoolsFromManager(LevelDefinition levelData)
        {
            poolManager.RefreshPools(validateAuthoringTargets: true);
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

            poolManager.EnsureBlockPoolSizes(_requiredBlockRootCountByKey, _requiredBlockCellCountByKey);
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
            _requiredBlockCellCountByKey.Clear();
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

                var poolKey = sourceBlocks[i].ResolvePoolKey();

                _requiredBlockRootCountByKey.TryGetValue(poolKey, out var existingCount);
                _requiredBlockRootCountByKey[poolKey] = existingCount + 1;

                var cellCount = Mathf.Max(1, runtimeBlock.LocalCells?.Length ?? 1);
                if (!_requiredBlockCellCountByKey.TryGetValue(poolKey, out var existingCellCount) ||
                    cellCount > existingCellCount)
                {
                    _requiredBlockCellCountByKey[poolKey] = cellCount;
                }
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

        private bool HasRequiredReferences()
        {
            return boardController != null && poolManager != null;
        }
        
    }
}
