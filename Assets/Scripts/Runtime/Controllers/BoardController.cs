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
    public class BoardController : MonoBehaviour, BoardPointerGestureController.IMoveHost
    {
        [SerializeField] private float cellSize = 1f;
        [SerializeField, Range(0.01f, 1f)] private float dragActivationInCells = 0.35f;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.0001f;

        [SerializeField] private Camera inputCamera;
        [SerializeField] private AudioManager audioManager;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int, DoorOpeningData> BlockCleared;
        public event Action<int, bool> BlockDragHighlightChanged;

        public Vector2Int GridDimensions => _runtimeState?.GridDimensions ?? Vector2Int.zero;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => new(transform.position.x, transform.position.y);

        private BoardRuntimeState _runtimeState;
        private BoardInput _input;
        private BoardPointerGestureController _pointerGestureController;
        private BoardBlockSlideService _blockSlideService;
        private bool _levelCompletedRaised;
        private int _highlightedGestureBlockId = -1;

        private void Awake()
        {
            EnsureDependencies();
            RefreshProjectionState();
        }

        private void OnDisable()
        {
            _pointerGestureController?.EndPointerGesture();
            ClearGestureHighlight();
        }

        private void OnValidate()
        {
            RefreshProjectionState();
        }

        public void Setup(LevelJsonData levelData, BlockShapeRegistry shapeRegistry)
        {
            EnsureDependencies();
            _levelCompletedRaised = false;
            _pointerGestureController.EndPointerGesture();
            ClearGestureHighlight();

            _runtimeState.Setup(levelData, shapeRegistry);
            RefreshProjectionState();
        }

        public bool TryBeginPointerGesture(Vector2 pointerPosition)
        {
            EnsureDependencies();
            if (!_pointerGestureController.TryBeginPointerGesture(pointerPosition, inputCamera, out var blockId))
            {
                return false;
            }

            SetGestureHighlight(blockId);
            audioManager?.PlayBlockSelect();
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition)
        {
            EnsureDependencies();
            return _pointerGestureController.TryUpdatePointerGesture(pointerPosition, inputCamera);
        }

        public void EndPointerGesture()
        {
            _pointerGestureController?.EndPointerGesture();
            ClearGestureHighlight();
        }

        public bool TryGetRuntimeBlock(int blockId, out RuntimeBlockState block)
        {
            block = default;
            return _runtimeState != null && _runtimeState.TryGetRuntimeBlock(blockId, out block);
        }

        bool BoardPointerGestureController.IMoveHost.TryMoveGestureBlock(int blockId, Direction direction,
            int requestedCellCount, out int movedCellCount, out bool blockCleared)
        {
            movedCellCount = 0;
            blockCleared = false;
            if (requestedCellCount <= 0 || _blockSlideService == null)
            {
                return false;
            }

            if (!_blockSlideService.TrySlide(blockId, direction, requestedCellCount, out var slideResult))
            {
                return false;
            }

            movedCellCount = slideResult.MovedCellCount;
            blockCleared = slideResult.ClearedThroughDoor;

            if (slideResult.ClearedThroughDoor)
            {
                if (_highlightedGestureBlockId == slideResult.BlockId)
                {
                    ClearGestureHighlight();
                }

                var exitDirection = slideResult.MatchedDoor.ResolveExitDirection(GridDimensions);
                BlockCleared?.Invoke(slideResult.BlockId, slideResult.EndPosition, exitDirection, slideResult.MatchedDoor);
                EvaluateCompletionState();
                return true;
            }

            BlockMoved?.Invoke(slideResult.BlockId, slideResult.StartPosition, slideResult.EndPosition);
            return true;
        }

        private void EvaluateCompletionState()
        {
            if (_levelCompletedRaised || _runtimeState == null || _runtimeState.ActiveBlockCount > 0)
            {
                return;
            }

            _levelCompletedRaised = true;
            LevelCompleted?.Invoke();
        }

        private void EnsureDependencies()
        {
            _runtimeState ??= new BoardRuntimeState();
            _input ??= new BoardInput();
            _pointerGestureController ??= new BoardPointerGestureController(_runtimeState, _input, this);
            _blockSlideService ??=
                new BoardBlockSlideService(_runtimeState.RuntimeBlocks, _runtimeState.DoorOpenings,
                    _runtimeState.OccupancyMap);
        }

        private void RefreshProjectionState()
        {
            if (_input == null)
            {
                return;
            }

            var gridDimensions = _runtimeState != null ? _runtimeState.GridDimensions : Vector2Int.zero;
            _input.Refresh(BoardOrigin, cellSize, gridDimensions, dragActivationInCells, directionDeadZone,
                transform.position.z);
        }

        private void SetGestureHighlight(int blockId)
        {
            if (blockId < 0)
            {
                return;
            }

            if (_highlightedGestureBlockId == blockId)
            {
                return;
            }

            ClearGestureHighlight();
            _highlightedGestureBlockId = blockId;
            BlockDragHighlightChanged?.Invoke(blockId, true);
        }

        private void ClearGestureHighlight()
        {
            if (_highlightedGestureBlockId < 0)
            {
                return;
            }

            var releasedBlockId = _highlightedGestureBlockId;
            _highlightedGestureBlockId = -1;
            BlockDragHighlightChanged?.Invoke(releasedBlockId, false);
        }
    }
}
