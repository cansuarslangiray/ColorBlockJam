using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using Runtime.Managers;
using UnityEngine;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardController : MonoBehaviour
    {
        private const float DragActivationInCells = 0.35f;
        private const float DirectionDeadZone = 0.0001f;

        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 boardOrigin;
        [SerializeField] private BoardGameplayConfig gameplayConfig;

        [SerializeField] private Camera inputCamera;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int, DoorOpeningData> BlockCleared;

        public bool IsInitialized { get; private set; }
        public Vector2Int GridDimensions => _gridDimensions;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => boardOrigin;
        public BoardGameplayConfig GameplayConfig => gameplayConfig;

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new();
        private readonly List<DoorOpeningData> _doorOpenings = new();
        private readonly BoardOccupancyMap _occupancyMap = new();
        private Vector2Int _gridDimensions;
        private float _boardWidthWorld;
        private float _boardHeightWorld;
        private float _dragActivationDistanceSqr;
        private float _resolvedCellSize = 1f;
        private float _inverseCellSize = 1f;
        private bool _levelCompletedRaised;

        private Plane _boardPlane = new(Vector3.forward, Vector3.zero);
        private int _activeGestureBlockId = -1;
        private Vector2 _activeGestureStartBoardPoint;
        private GestureAxis _activeGestureAxis;
        private int _activeGestureAppliedStepCount;
        private Camera _activeGestureCamera;

        private bool IsBoardReadyForInput => IsInitialized && StateManager.HasInstance &&
                                             StateManager.Instance.CurrentState == GameState.Playing;

        private void Awake()
        {
            RefreshInputPlane();
            RefreshCachedBoardMetrics();
        }

        private void OnValidate()
        {
            RefreshInputPlane();
            RefreshCachedBoardMetrics();
        }

        public void Setup(LevelJsonData levelData, BlockShapeRegistry shapeRegistry)
        {
            IsInitialized = false;
            _runtimeBlocks.Clear();
            _doorOpenings.Clear();
            _levelCompletedRaised = false;
            EndPointerGesture();

            if (levelData == null)
            {
                _gridDimensions = Vector2Int.zero;
                RefreshCachedBoardMetrics();
                return;
            }

            _gridDimensions = levelData.gridDimensions;
            RefreshCachedBoardMetrics();
            RuntimeBoardSetupBuilder.Populate(levelData, shapeRegistry, _occupancyMap, _runtimeBlocks, _doorOpenings);
            IsInitialized = true;

            RefreshInputPlane();
        }

        public bool TryBeginPointerGesture(Vector2 pointerPosition, Camera pointerCamera)
        {
            if (!IsBoardReadyForInput)
            {
                return false;
            }

            var resolvedCamera = pointerCamera ? pointerCamera : inputCamera;
            if (!resolvedCamera)
            {
                return false;
            }

            if (!TryResolveBoardPoint(pointerPosition, resolvedCamera, out var boardWorldPoint))
            {
                return false;
            }

            if (!TryWorldToCell(boardWorldPoint, out var touchedCell) ||
                !TryGetBlockAtCell(touchedCell, out var blockId))
            {
                return false;
            }

            _activeGestureBlockId = blockId;
            _activeGestureStartBoardPoint = boardWorldPoint;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
            _activeGestureCamera = resolvedCamera;
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition, Camera pointerCamera)
        {
            if (!IsBoardReadyForInput || _activeGestureBlockId < 0)
            {
                return false;
            }

            var resolvedCamera = pointerCamera ? pointerCamera : _activeGestureCamera;
            if (!resolvedCamera)
            {
                resolvedCamera = inputCamera;
            }

            if (!resolvedCamera)
            {
                return false;
            }

            if (!TryResolveBoardPoint(pointerPosition, resolvedCamera, out var boardWorldPoint))
            {
                return false;
            }

            if (!TryGetRuntimeBlock(_activeGestureBlockId, out var activeBlock))
            {
                EndPointerGesture();
                return false;
            }

            if (activeBlock.MovementConstraint == BlockMovementConstraint.Free)
            {
                return TryUpdateFreePointerGesture(boardWorldPoint);
            }

            var deltaFromGestureStart = boardWorldPoint - _activeGestureStartBoardPoint;
            if (_activeGestureAxis == GestureAxis.None)
            {
                if (deltaFromGestureStart.sqrMagnitude < _dragActivationDistanceSqr ||
                    !TryResolveDragAxis(deltaFromGestureStart, activeBlock.MovementConstraint,
                        out _activeGestureAxis))
                {
                    return false;
                }
            }

            var axisDelta = _activeGestureAxis == GestureAxis.Horizontal ? deltaFromGestureStart.x : deltaFromGestureStart.y;
            var desiredStepCount = Mathf.RoundToInt(axisDelta * _inverseCellSize);
            var stepDelta = desiredStepCount - _activeGestureAppliedStepCount;
            if (stepDelta == 0)
            {
                return false;
            }

            var stepSign = stepDelta > 0 ? 1 : -1;
            var direction = ResolveDirectionForAxis(_activeGestureAxis, stepSign);
            var requestedCellCount = Mathf.Abs(stepDelta);
            if (requestedCellCount == 0)
            {
                return false;
            }

            var moved = TrySlideBlock(_activeGestureBlockId, direction, requestedCellCount, out var movedCellCount);
            if (movedCellCount > 0)
            {
                _activeGestureAppliedStepCount += movedCellCount * stepSign;
            }

            return moved;
        }

        private bool TryUpdateFreePointerGesture(Vector2 boardWorldPoint)
        {
            var deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
            if (deltaFromAnchor.sqrMagnitude < _dragActivationDistanceSqr)
            {
                return false;
            }

            var movedAny = false;
            while (TryResolveDominantDirection(deltaFromAnchor, out var direction))
            {
                var axisDelta = direction.IsHorizontal() ? deltaFromAnchor.x : deltaFromAnchor.y;
                var requestedCellCount = Mathf.FloorToInt(Mathf.Abs(axisDelta) * _inverseCellSize);
                if (requestedCellCount <= 0)
                {
                    break;
                }

                var moved = TrySlideBlock(_activeGestureBlockId, direction, requestedCellCount, out var movedCellCount);
                if (movedCellCount <= 0)
                {
                    break;
                }

                movedAny |= moved;
                var consumedDistance = movedCellCount * _resolvedCellSize;
                if (direction.IsHorizontal())
                {
                    _activeGestureStartBoardPoint.x += axisDelta >= 0f ? consumedDistance : -consumedDistance;
                }
                else
                {
                    _activeGestureStartBoardPoint.y += axisDelta >= 0f ? consumedDistance : -consumedDistance;
                }

                deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
                if (deltaFromAnchor.sqrMagnitude < _dragActivationDistanceSqr)
                {
                    break;
                }
            }

            return movedAny;
        }

        public void EndPointerGesture()
        {
            _activeGestureBlockId = -1;
            _activeGestureCamera = null;
            _activeGestureStartBoardPoint = default;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
        }

        private bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
        {
            cell = default;
            if (!IsInitialized || _resolvedCellSize <= 0f)
            {
                return false;
            }

            var cellX = Mathf.FloorToInt((worldPosition.x - boardOrigin.x) * _inverseCellSize);
            var cellY = Mathf.FloorToInt((worldPosition.y - boardOrigin.y) * _inverseCellSize);
            if (!_occupancyMap.IsInside(cellX, cellY))
            {
                return false;
            }

            cell = new Vector2Int(cellX, cellY);
            return true;
        }

        private bool TryGetBlockAtCell(Vector2Int cell, out int blockId)
        {
            blockId = -1;
            return IsInitialized && _occupancyMap.TryGetBlockAt(cell.x, cell.y, out blockId);
        }

        public bool TryGetRuntimeBlock(int blockId, out RuntimeBlockState block)
        {
            block = default;
            return IsInitialized && _runtimeBlocks.TryGetValue(blockId, out block);
        }

        private bool TrySlideBlock(int blockId, Direction direction, int maxCellsToMove, out int movedCellCount)
        {
            movedCellCount = 0;
            if (!TryGetRuntimeBlock(blockId, out var block))
            {
                return false;
            }

            if (!IsDirectionAllowed(block.MovementConstraint, direction))
            {
                return false;
            }

            var startPosition = block.Position;
            var directionVector = direction.ToVector();
            var requestedCells = Mathf.Max(1, maxCellsToMove);

            var currentPosition = startPosition;
            var hasMoved = false;
            var reachedDoor = false;
            var matchedDoor = default(DoorOpeningData);

            while (requestedCells > 0)
            {
                requestedCells--;
                var nextPosition = currentPosition + directionVector;
                if (!_occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells))
                {
                    break;
                }

                currentPosition = nextPosition;
                hasMoved = true;
                movedCellCount++;

                if (!DoorExitEvaluator.TryResolveDoorExit(block, currentPosition, direction, _doorOpenings,
                        out var resolvedDoor))
                {
                    continue;
                }

                matchedDoor = resolvedDoor;
                reachedDoor = true;
                break;
            }

            if (hasMoved && !reachedDoor &&
                DoorExitEvaluator.TryResolveDoorPullExit(block, currentPosition, _doorOpenings, out var pulledDoor))
            {
                matchedDoor = pulledDoor;
                reachedDoor = true;
            }

            if (!hasMoved)
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, startPosition, block.LocalCells);
            block.Position = currentPosition;

            if (reachedDoor)
            {
                _runtimeBlocks.Remove(blockId);
                var exitDirection = ResolveExitDirectionForDoor(matchedDoor);
                BlockCleared?.Invoke(blockId, block.Position, exitDirection, matchedDoor);
                if (_activeGestureBlockId == blockId)
                {
                    _activeGestureBlockId = -1;
                }

                EvaluateCompletionState();
                return true;
            }

            _runtimeBlocks[blockId] = block;
            _occupancyMap.FillBlock(blockId, currentPosition, block.LocalCells);
            BlockMoved?.Invoke(blockId, startPosition, currentPosition);
            return true;
        }

        private Vector2Int ResolveExitDirectionForDoor(DoorOpeningData matchedDoor)
        {
            var maxX = _gridDimensions.x - 1;
            var maxY = _gridDimensions.y - 1;
            if (_gridDimensions.x > 0 && _gridDimensions.y > 0)
            {
                if (matchedDoor.MinCell.x <= 0)
                {
                    return Vector2Int.left;
                }

                if (matchedDoor.MaxCell.x >= maxX)
                {
                    return Vector2Int.right;
                }

                if (matchedDoor.MinCell.y <= 0)
                {
                    return Vector2Int.down;
                }

                if (matchedDoor.MaxCell.y >= maxY)
                {
                    return Vector2Int.up;
                }
            }

            return matchedDoor.EdgeDirection.ToVector();
        }

        private static bool TryResolveDragAxis(Vector2 delta, BlockMovementConstraint movementConstraint,
            out GestureAxis axis)
        {
            axis = movementConstraint switch
            {
                BlockMovementConstraint.HorizontalOnly => GestureAxis.Horizontal,
                BlockMovementConstraint.VerticalOnly => GestureAxis.Vertical,
                _ => GestureAxis.None
            };

            if (axis != GestureAxis.None)
            {
                return true;
            }

            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (absX < DirectionDeadZone && absY < DirectionDeadZone)
            {
                return false;
            }

            axis = absX >= absY ? GestureAxis.Horizontal : GestureAxis.Vertical;
            return true;
        }

        private static Direction ResolveDirectionForAxis(GestureAxis axis, int sign)
        {
            if (axis == GestureAxis.Horizontal)
            {
                return sign >= 0 ? Direction.Right : Direction.Left;
            }

            return sign >= 0 ? Direction.Up : Direction.Down;
        }

        private static bool TryResolveDominantDirection(Vector2 delta, out Direction direction)
        {
            direction = default;
            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (absX < DirectionDeadZone && absY < DirectionDeadZone)
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

        private static bool IsDirectionAllowed(BlockMovementConstraint movementConstraint, Direction direction)
        {
            switch (movementConstraint)
            {
                case BlockMovementConstraint.HorizontalOnly:
                    return direction is Direction.Left or Direction.Right;
                case BlockMovementConstraint.VerticalOnly:
                    return direction is Direction.Up or Direction.Down;
                default:
                    return true;
            }
        }

        private void EvaluateCompletionState()
        {
            if (_levelCompletedRaised)
            {
                return;
            }

            if (_runtimeBlocks.Count == 0)
            {
                _levelCompletedRaised = true;
                LevelCompleted?.Invoke();
            }
        }

        private bool TryResolveBoardPoint(Vector2 screenPosition, Camera pointerCamera, out Vector2 boardWorldPoint)
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
            if (!IsPointInsideBoard(hitPoint))
            {
                return false;
            }

            boardWorldPoint = new Vector2(hitPoint.x, hitPoint.y);
            return true;
        }

        private bool IsPointInsideBoard(Vector3 boardWorldPoint)
        {
            if (!IsInitialized)
            {
                return false;
            }

            var gridDimensions = GridDimensions;
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0)
            {
                return false;
            }

            var relativeX = boardWorldPoint.x - boardOrigin.x;
            var relativeY = boardWorldPoint.y - boardOrigin.y;
            return relativeX >= 0f && relativeX < _boardWidthWorld && relativeY >= 0f && relativeY < _boardHeightWorld;
        }

        private void RefreshInputPlane()
        {
            _boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        }

        private void RefreshCachedBoardMetrics()
        {
            var resolvedCellSize = Mathf.Max(0.01f, cellSize);
            _resolvedCellSize = resolvedCellSize;
            _inverseCellSize = 1f / resolvedCellSize;
            _boardWidthWorld = Mathf.Max(0, _gridDimensions.x) * resolvedCellSize;
            _boardHeightWorld = Mathf.Max(0, _gridDimensions.y) * resolvedCellSize;
            var dragActivationDistance = resolvedCellSize * DragActivationInCells;
            _dragActivationDistanceSqr = dragActivationDistance * dragActivationDistance;
        }
    }
}
