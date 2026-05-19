using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardInput
    {
        private Vector2 _boardOrigin;
        private float _resolvedCellSize = 1f;
        private float _inverseCellSize = 1f;
        private float _boardWidthWorld;
        private float _boardHeightWorld;
        private float _dragActivationDistanceSqr;
        private float _directionDeadZone;
        private Plane _boardPlane = new(Vector3.forward, Vector3.zero);

        public float InverseCellSize => _inverseCellSize;
        public float DragActivationDistanceSqr => _dragActivationDistanceSqr;

        public void Refresh(Vector2 boardOrigin, float cellSize, Vector2Int gridDimensions, float dragActivationInCells,
            float directionDeadZone, float boardPlaneZ)
        {
            _boardOrigin = boardOrigin;
            _resolvedCellSize = Mathf.Max(0.01f, cellSize);
            _inverseCellSize = 1f / _resolvedCellSize;
            _boardWidthWorld = Mathf.Max(0, gridDimensions.x) * _resolvedCellSize;
            _boardHeightWorld = Mathf.Max(0, gridDimensions.y) * _resolvedCellSize;
            _directionDeadZone = Mathf.Max(0f, directionDeadZone);

            var dragActivationDistance = _resolvedCellSize * Mathf.Max(0.01f, dragActivationInCells);
            _dragActivationDistanceSqr = dragActivationDistance * dragActivationDistance;
            _boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, boardPlaneZ));
        }

        public bool TryResolveGestureBoardPoint(Vector2 pointerPosition, Camera inputCamera,
            out Vector2 boardWorldPoint, bool clampToBoardBounds = false)
        {
            boardWorldPoint = default;
            return inputCamera && TryResolveBoardPoint(pointerPosition, inputCamera, out boardWorldPoint,
                clampToBoardBounds);
        }

        public bool TryWorldToCell(Vector2 worldPosition, BoardOccupancyMap occupancyMap, out Vector2Int cell)
        {
            cell = default;
            if (_boardWidthWorld <= 0f || _boardHeightWorld <= 0f || _resolvedCellSize <= 0f)
            {
                return false;
            }

            var cellX = Mathf.FloorToInt((worldPosition.x - _boardOrigin.x) * _inverseCellSize);
            var cellY = Mathf.FloorToInt((worldPosition.y - _boardOrigin.y) * _inverseCellSize);
            if (!occupancyMap.IsInside(cellX, cellY))
            {
                return false;
            }

            cell = new Vector2Int(cellX, cellY);
            return true;
        }

        public bool TryResolveDragAxis(Vector2 delta, BlockMovementConstraint movementConstraint, out GestureAxis axis)
        {
            axis = movementConstraint switch
            {
                BlockMovementConstraint.Horizontal => GestureAxis.Horizontal,
                BlockMovementConstraint.Vertical => GestureAxis.Vertical,
                _ => GestureAxis.None
            };

            if (axis != GestureAxis.None)
            {
                return true;
            }

            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (IsBelowDirectionDeadZone(absX, absY))
            {
                return false;
            }

            axis = absX >= absY ? GestureAxis.Horizontal : GestureAxis.Vertical;
            return true;
        }

        public bool TryResolveDominantDirection(Vector2 delta, out Direction direction)
        {
            direction = default;
            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (IsBelowDirectionDeadZone(absX, absY))
            {
                return false;
            }

            if (absX >= absY)
            {
                direction = delta.x >= 0f ? Direction.Right : Direction.Left;
                return true;
            }

            direction = delta.y >= 0f ? Direction.Up : Direction.Down;
            return true;
        }

        public static Direction ResolveDirectionForAxis(GestureAxis axis, int sign)
        {
            if (axis == GestureAxis.Horizontal)
            {
                return sign >= 0 ? Direction.Right : Direction.Left;
            }

            return sign >= 0 ? Direction.Up : Direction.Down;
        }

        public Vector2 AdvanceGestureAnchor(Vector2 anchorPoint, Direction direction, int movedCellCount)
        {
            if (movedCellCount <= 0)
            {
                return anchorPoint;
            }

            var consumedDistance = movedCellCount * _resolvedCellSize;
            var directionVector = direction.ToVector();
            anchorPoint.x += directionVector.x * consumedDistance;
            anchorPoint.y += directionVector.y * consumedDistance;
            return anchorPoint;
        }

        private bool TryResolveBoardPoint(Vector2 screenPosition, Camera pointerCamera,
            out Vector2 boardWorldPoint, bool clampToBoardBounds)
        {
            boardWorldPoint = default;
            if (!pointerCamera)
            {
                return false;
            }

            var ray = pointerCamera.ScreenPointToRay(screenPosition);
            if (!_boardPlane.Raycast(ray, out var hitDistance))
            {
                return false;
            }

            var hitPoint = ray.GetPoint(hitDistance);
            if (IsPointInsideBoard(hitPoint))
            {
                boardWorldPoint = new Vector2(hitPoint.x, hitPoint.y);
                return true;
            }

            return clampToBoardBounds && TryClampPointInsideBoard(hitPoint, out boardWorldPoint);
        }

        private bool IsPointInsideBoard(Vector3 boardWorldPoint)
        {
            if (_boardWidthWorld <= 0f || _boardHeightWorld <= 0f)
            {
                return false;
            }

            var relativeX = boardWorldPoint.x - _boardOrigin.x;
            var relativeY = boardWorldPoint.y - _boardOrigin.y;
            return relativeX >= 0f && relativeX < _boardWidthWorld && relativeY >= 0f && relativeY < _boardHeightWorld;
        }

        private bool TryClampPointInsideBoard(Vector3 boardWorldPoint, out Vector2 clampedPoint)
        {
            clampedPoint = default;
            if (_boardWidthWorld <= 0f || _boardHeightWorld <= 0f)
            {
                return false;
            }

            var minX = _boardOrigin.x;
            var minY = _boardOrigin.y;
            var maxX = _boardOrigin.x + Mathf.Max(0f, _boardWidthWorld - Mathf.Epsilon);
            var maxY = _boardOrigin.y + Mathf.Max(0f, _boardHeightWorld - Mathf.Epsilon);

            clampedPoint = new Vector2(
                Mathf.Clamp(boardWorldPoint.x, minX, maxX),
                Mathf.Clamp(boardWorldPoint.y, minY, maxY));
            return true;
        }

        private bool IsBelowDirectionDeadZone(float absX, float absY)
        {
            return absX < _directionDeadZone && absY < _directionDeadZone;
        }
    }
}
