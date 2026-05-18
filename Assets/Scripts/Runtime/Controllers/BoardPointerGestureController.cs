using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardPointerGestureController
    {
        internal interface IMoveHost
        {
            bool TryMoveGestureBlock(int blockId, Direction direction, int requestedCellCount, out int movedCellCount,
                out bool blockCleared);
        }

        private readonly BoardRuntimeState _runtimeState;
        private readonly BoardInput _input;
        private readonly IMoveHost _moveHost;

        private int _activeGestureBlockId = -1;
        private Vector2 _activeGestureStartBoardPoint;
        private GestureAxis _activeGestureAxis;
        private int _activeGestureAppliedStepCount;

        public BoardPointerGestureController(BoardRuntimeState runtimeState, BoardInput input,
            IMoveHost moveHost)
        {
            _runtimeState = runtimeState;
            _input = input;
            _moveHost = moveHost;
        }

        public bool TryBeginPointerGesture(Vector2 pointerPosition, Camera inputCamera)
        {
            if (_runtimeState == null ||
                !_runtimeState.IsInitialized ||
                !_input.TryResolveGestureBoardPoint(pointerPosition, inputCamera, _runtimeState.IsInitialized,
                    out var boardWorldPoint) ||
                !_input.TryWorldToCell(boardWorldPoint, _runtimeState.OccupancyMap, _runtimeState.IsInitialized,
                    out var touchedCell) ||
                !_runtimeState.TryGetBlockAtCell(touchedCell, out var blockId))
            {
                return false;
            }

            _activeGestureBlockId = blockId;
            _activeGestureStartBoardPoint = boardWorldPoint;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition, Camera inputCamera)
        {
            if (_activeGestureBlockId < 0 || _runtimeState == null)
            {
                return false;
            }

            if (!_input.TryResolveGestureBoardPoint(pointerPosition, inputCamera, _runtimeState.IsInitialized,
                    out var boardWorldPoint))
            {
                return false;
            }

            if (!_runtimeState.TryGetRuntimeBlock(_activeGestureBlockId, out var activeBlock))
            {
                EndPointerGesture();
                return false;
            }

            if (activeBlock.MovementConstraint == BlockMovementConstraint.Free)
            {
                return TryUpdateFreePointerGesture(boardWorldPoint);
            }

            return TryUpdateConstrainedPointerGesture(activeBlock, boardWorldPoint);
        }

        public void EndPointerGesture()
        {
            _activeGestureBlockId = -1;
            _activeGestureStartBoardPoint = default;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
        }

        private bool TryUpdateConstrainedPointerGesture(RuntimeBlockState activeBlock, Vector2 boardWorldPoint)
        {
            var deltaFromGestureStart = boardWorldPoint - _activeGestureStartBoardPoint;
            if (_activeGestureAxis == GestureAxis.None)
            {
                if (deltaFromGestureStart.sqrMagnitude < _input.DragActivationDistanceSqr ||
                    !_input.TryResolveDragAxis(deltaFromGestureStart, activeBlock.MovementConstraint,
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

            _activeGestureAppliedStepCount += movedCellCount * stepSign;
            return true;
        }

        private bool TryUpdateFreePointerGesture(Vector2 boardWorldPoint)
        {
            var deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
            if (deltaFromAnchor.sqrMagnitude < _input.DragActivationDistanceSqr)
            {
                return false;
            }

            var movedAny = false;
            while (_input.TryResolveDominantDirection(deltaFromAnchor, out var direction))
            {
                var axisDelta = direction.IsHorizontal() ? deltaFromAnchor.x : deltaFromAnchor.y;
                var requestedCellCount = Mathf.FloorToInt(Mathf.Abs(axisDelta) * _input.InverseCellSize);
                if (requestedCellCount <= 0)
                {
                    break;
                }

                if (!TryMoveActiveGestureBlock(direction, requestedCellCount, out var movedCellCount, out var blockCleared))
                {
                    break;
                }

                movedAny = true;
                _activeGestureStartBoardPoint =
                    _input.AdvanceGestureAnchor(_activeGestureStartBoardPoint, direction, movedCellCount);
                if (blockCleared || _activeGestureBlockId < 0)
                {
                    break;
                }

                deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
                if (deltaFromAnchor.sqrMagnitude < _input.DragActivationDistanceSqr)
                {
                    break;
                }
            }

            return movedAny;
        }

        private bool TryMoveActiveGestureBlock(Direction direction, int requestedCellCount, out int movedCellCount,
            out bool blockCleared)
        {
            movedCellCount = 0;
            blockCleared = false;
            if (requestedCellCount <= 0 || _activeGestureBlockId < 0 || _moveHost == null)
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
