using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using Runtime.Managers;
using UnityEngine;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardController : MonoBehaviour, IBoardGestureMoveHost
    {
        [SerializeField] private float cellSize = 1f;
        [SerializeField, Range(0.01f, 1f)] private float dragActivationInCells = 0.35f;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.0001f;

        [SerializeField] private Camera inputCamera;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int, DoorOpeningData> BlockCleared;
        public event Action<int, Vector2Int> BlockLayerExited;
        public event Action<int, bool> BlockOutlineDragStateChanged;
        public event Action ConditionStatesChanged;
        public event Action ConditionFailed;

        public Vector2Int GridDimensions => _runtimeState.GridDimensions;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => new(transform.position.x, transform.position.y);
        
        private BoardRuntimeState _runtimeState;
        private BoardInput _input;
        private BoardPointerGestureController _pointerGestureController;
        private BoardBlockSlideService _blockSlideService;
        private BoardBlockConditionService _blockConditionService;
        private bool _levelCompletedRaised;
        private int _draggingBlockId = -1;
        private bool _hasActiveGestureMoveTracking;
        private bool _activeGestureMovedAnyBlock;
        private int _activeGestureMovedBlockId = -1;
        private bool _activeGestureMovedBlockRemoved;

        private void Awake()
        {
            InitializeDependencies();
            RefreshProjectionState();
        }

        private void OnDisable()
        {
            _pointerGestureController.EndPointerGesture();
            ClearDraggingBlock();
            ResetActiveGestureMoveTracking();
        }


        public void Setup(LevelDefinition levelData, BlockShapeCatalog shapeCatalog)
        {
            _levelCompletedRaised = false;
            _pointerGestureController.EndPointerGesture();
            ClearDraggingBlock();
            ResetActiveGestureMoveTracking();

            _runtimeState.Setup(levelData, shapeCatalog);
            _blockConditionService.Setup(_runtimeState.RuntimeBlocks);
            RefreshProjectionState();
            EvaluateCompletionState();
        }

        public bool TryBeginPointerGesture(Vector2 pointerPosition)
        {
            if (!_pointerGestureController.TryBeginPointerGesture(pointerPosition, inputCamera, out var blockId))
            {
                return false;
            }

            if (_blockConditionService.IsBlockLocked(blockId))
            {
                _pointerGestureController.EndPointerGesture();
                return false;
            }

            BeginActiveGestureMoveTracking(blockId);
            SetDraggingBlock(blockId);
            AudioManager.Instance?.PlayBlockSelect();
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition)
        {
            return _pointerGestureController.TryUpdatePointerGesture(pointerPosition, inputCamera);
        }

        public void EndPointerGesture()
        {
            CommitActiveGestureMoveIfAny();
            _pointerGestureController?.EndPointerGesture();
            ClearDraggingBlock();
            ResetActiveGestureMoveTracking();
        }

        public bool TryGetRuntimeBlock(int blockId, out RuntimeBlockState block)
        {
            return _runtimeState.TryGetRuntimeBlock(blockId, out block);
        }

        public bool TryGetConditionIndicatorState(int blockId, out BlockConditionIndicatorState indicatorState)
        {
            return _blockConditionService.TryGetIndicatorState(blockId, out indicatorState);
        }

        public bool IsBlockLocked(int blockId) => _blockConditionService.IsBlockLocked(blockId);

        bool IBoardGestureMoveHost.TryMoveGestureBlock(int blockId, Direction direction,
            int requestedCellCount, out int movedCellCount, out bool blockCleared)
        {
            movedCellCount = 0;
            blockCleared = false;
            if (requestedCellCount <= 0)
            {
                return false;
            }

            if (_blockConditionService.IsBlockLocked(blockId))
            {
                return false;
            }

            if (!_blockSlideService.TrySlide(blockId, direction, requestedCellCount, out var slideResult))
            {
                return false;
            }

            movedCellCount = slideResult.MovedCellCount;
            blockCleared = slideResult.BlockRemovedFromBoard || slideResult.LayerExitedWithRemainingBlock;
            RegisterActiveGestureMove(slideResult.BlockId, slideResult.BlockRemovedFromBoard);

            if (slideResult.ClearedThroughDoor)
            {
                if (slideResult.BlockRemovedFromBoard && _draggingBlockId == slideResult.BlockId)
                {
                    ClearDraggingBlock();
                }

                var exitDirection = slideResult.MatchedDoor.ResolveExitDirection(GridDimensions);
                if (slideResult.BlockRemovedFromBoard)
                {
                    BlockCleared?.Invoke(slideResult.BlockId, slideResult.EndPosition, exitDirection,
                        slideResult.MatchedDoor);
                    EvaluateCompletionState();
                }
                else if (slideResult.LayerExitedWithRemainingBlock)
                {
                    BlockLayerExited?.Invoke(slideResult.BlockId, slideResult.EndPosition);
                }
            }
            else
            {
                BlockMoved?.Invoke(slideResult.BlockId, slideResult.StartPosition, slideResult.EndPosition);
            }

            return true;
        }

        private void BeginActiveGestureMoveTracking(int blockId)
        {
            _hasActiveGestureMoveTracking = true;
            _activeGestureMovedAnyBlock = false;
            _activeGestureMovedBlockId = blockId;
            _activeGestureMovedBlockRemoved = false;
        }

        private void RegisterActiveGestureMove(int blockId, bool blockRemovedFromBoard)
        {
            if (!_hasActiveGestureMoveTracking)
            {
                return;
            }

            _activeGestureMovedAnyBlock = true;
            _activeGestureMovedBlockId = blockId;
            _activeGestureMovedBlockRemoved = _activeGestureMovedBlockRemoved || blockRemovedFromBoard;
        }

        private void CommitActiveGestureMoveIfAny()
        {
            if (!_hasActiveGestureMoveTracking || !_activeGestureMovedAnyBlock)
            {
                return;
            }

            _blockConditionService.ConsumeSuccessfulMove(_activeGestureMovedBlockId, _activeGestureMovedBlockRemoved,
                out var conditionFailed);
            ConditionStatesChanged?.Invoke();
            if (conditionFailed)
            {
                ConditionFailed?.Invoke();
            }
        }

        private void ResetActiveGestureMoveTracking()
        {
            _hasActiveGestureMoveTracking = false;
            _activeGestureMovedAnyBlock = false;
            _activeGestureMovedBlockId = -1;
            _activeGestureMovedBlockRemoved = false;
        }

        private void EvaluateCompletionState()
        {
            if (_levelCompletedRaised || _runtimeState.ActiveBlockCount > 0)
            {
                return;
            }

            _levelCompletedRaised = true;
            LevelCompleted?.Invoke();
        }

        private void InitializeDependencies()
        {
            _runtimeState = new BoardRuntimeState();
            _input = new BoardInput();
            _pointerGestureController = new BoardPointerGestureController(_runtimeState, _input, this);
            _blockSlideService =
                new BoardBlockSlideService(_runtimeState.RuntimeBlocks, _runtimeState.DoorOpenings,
                    _runtimeState.OccupancyMap);
            _blockConditionService = new BoardBlockConditionService();
        }


        private void RefreshProjectionState()
        {
            var gridDimensions = _runtimeState.GridDimensions;
            _input.Refresh(BoardOrigin, cellSize, gridDimensions, dragActivationInCells, directionDeadZone,
                transform.position.z);
        }

        private void SetDraggingBlock(int blockId)
        {
            if (blockId < 0)
            {
                return;
            }

            if (_draggingBlockId == blockId)
            {
                return;
            }

            ClearDraggingBlock();
            _draggingBlockId = blockId;
            BlockOutlineDragStateChanged?.Invoke(blockId, true);
        }

        private void ClearDraggingBlock()
        {
            if (_draggingBlockId < 0)
            {
                return;
            }

            var releasedBlockId = _draggingBlockId;
            _draggingBlockId = -1;
            BlockOutlineDragStateChanged?.Invoke(releasedBlockId, false);
        }
    }
}
