using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardPointerGestureController
    {
        private readonly BoardRuntimeState _runtimeState;
        private readonly BoardInput _input;
        private readonly IBoardGestureMoveHost _moveHost;

        private int _activeGestureBlockId = -1;
        private Vector2 _activeGestureStartBoardPoint;
        private GestureAxis _activeGestureAxis;
        private int _activeGestureAppliedStepCount;
        private bool _activeGestureHasMoved;

        public BoardPointerGestureController(BoardRuntimeState runtimeState, BoardInput input,
            IBoardGestureMoveHost moveHost)
        {
            _runtimeState = runtimeState;
            _input = input;
            _moveHost = moveHost;
        }

        public bool TryBeginPointerGesture(Vector2 pointerPosition, Camera inputCamera, out int activeBlockId)
        {
            activeBlockId = -1;
            if (!_input.TryResolveGestureBoardPoint(pointerPosition, inputCamera, out var boardWorldPoint) ||
                !_input.TryWorldToCell(boardWorldPoint, _runtimeState.OccupancyMap, out var touchedCell) ||
                !_runtimeState.TryGetBlockAtCell(touchedCell, out var touchedBlockId))
            {
                return false;
            }

            _activeGestureBlockId = touchedBlockId;
            _activeGestureStartBoardPoint = boardWorldPoint;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
            _activeGestureHasMoved = false;
            activeBlockId = touchedBlockId;
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition, Camera inputCamera)
        {
            if (_activeGestureBlockId < 0)
            {
                return false;
            }

            if (!_input.TryResolveGestureBoardPoint(pointerPosition, inputCamera,
                    out var boardWorldPoint, clampToBoardBounds: true))
            {
                return false;
            }

            if (!_runtimeState.TryGetRuntimeBlock(_activeGestureBlockId, out var activeBlock))
            {
                EndPointerGesture();
                return false;
            }

            var movementConstraint =
                activeBlock.BlockFeatures.ResolveMovementConstraint();
            if (movementConstraint == BlockMovementConstraint.Default)
            {
                return TryUpdateFreePointerGesture(boardWorldPoint);
            }

            return TryUpdateConstrainedPointerGesture(activeBlock, movementConstraint, boardWorldPoint);
        }

        public void EndPointerGesture()
        {
            _activeGestureBlockId = -1;
            _activeGestureStartBoardPoint = default;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
            _activeGestureHasMoved = false;
        }

        private bool TryUpdateConstrainedPointerGesture(RuntimeBlockState activeBlock,
            BlockMovementConstraint movementConstraint, Vector2 boardWorldPoint)
        {
            var deltaFromGestureStart = boardWorldPoint - _activeGestureStartBoardPoint;
            if (_activeGestureAxis == GestureAxis.None)
            {
                if (deltaFromGestureStart.sqrMagnitude < _input.DragActivationDistanceSqr ||
                    !_input.TryResolveDragAxis(deltaFromGestureStart, movementConstraint,
                        out _activeGestureAxis))
                {
                    return false;
                }
            }

            var axisDelta = _activeGestureAxis == GestureAxis.Horizontal ? deltaFromGestureStart.x : deltaFromGestureStart.y;
            var desiredStepCount = Mathf.RoundToInt(axisDelta * _input.InverseCellSize);
            var stepDelta = desiredStepCount - _activeGestureAppliedStepCount;
            if (stepDelta == 0)
            {
                return false;
            }

            var stepSign = stepDelta > 0 ? 1 : -1;
            var requestedCellCount = Mathf.Abs(stepDelta);
            var direction = BoardInput.ResolveDirectionForAxis(_activeGestureAxis, stepSign);
            if (!TryMoveActiveGestureBlock(direction, requestedCellCount, out var movedCellCount, out _))
            {
                return false;
            }

            _activeGestureHasMoved = true;
            _activeGestureAppliedStepCount += movedCellCount * stepSign;
            return true;
        }

        private bool TryUpdateFreePointerGesture(Vector2 boardWorldPoint)
        {
            var deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
            if (!_activeGestureHasMoved && deltaFromAnchor.sqrMagnitude < _input.DragActivationDistanceSqr)
            {
                return false;
            }

            var movedAny = false;
            while (_input.TryResolveDominantDirection(deltaFromAnchor, out var direction))
            {
                var axisDelta = direction.IsHorizontal() ? deltaFromAnchor.x : deltaFromAnchor.y;
                var requestedCellCount = Mathf.Abs(Mathf.RoundToInt(axisDelta * _input.InverseCellSize));
                if (requestedCellCount <= 0)
                {
                    break;
                }

                if (!TryMoveActiveGestureBlock(direction, requestedCellCount, out var movedCellCount,
                        out var blockCleared))
                {
                    break;
                }

                movedAny = true;
                _activeGestureHasMoved = true;
                _activeGestureStartBoardPoint =
                    _input.AdvanceGestureAnchor(_activeGestureStartBoardPoint, direction, movedCellCount);
                if (blockCleared || _activeGestureBlockId < 0)
                {
                    break;
                }

                deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
            }

            return movedAny;
        }

        private bool TryMoveActiveGestureBlock(Direction direction, int requestedCellCount, out int movedCellCount,
            out bool blockCleared)
        {
            movedCellCount = 0;
            blockCleared = false;
            if (requestedCellCount <= 0 || _activeGestureBlockId < 0)
            {
                return false;
            }

            if (!_moveHost.TryMoveGestureBlock(_activeGestureBlockId, direction, requestedCellCount, out movedCellCount,
                    out blockCleared))
            {
                return false;
            }

            if (blockCleared)
            {
                _activeGestureBlockId = -1;
            }

            return true;
        }
    }
}
