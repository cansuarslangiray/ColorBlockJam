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
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 boardOrigin;
        [SerializeField] private BoardGameplayConfig gameplayConfig;
        [SerializeField, Range(0.01f, 1f)] private float dragActivationInCells = 0.35f;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.0001f;

        [SerializeField] private Camera inputCamera;
        [SerializeField] private AudioManager audioManager;

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

        private void Awake()
        {
            RefreshInputPlane();
            RefreshCachedBoardMetrics();
        }

        private void OnDisable()
        {
            EndPointerGesture();
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

        public bool TryBeginPointerGesture(Vector2 pointerPosition)
        {
            if (!IsInitialized ||
                !TryResolveGestureBoardPoint(pointerPosition, out var boardWorldPoint) ||
                !TryWorldToCell(boardWorldPoint, out var touchedCell) ||
                !TryGetBlockAtCell(touchedCell, out var blockId))
            {
                return false;
            }

            _activeGestureBlockId = blockId;
            _activeGestureStartBoardPoint = boardWorldPoint;
            _activeGestureAxis = GestureAxis.None;
            _activeGestureAppliedStepCount = 0;
            audioManager.PlayBlockSelect();
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition)
        {
            if (_activeGestureBlockId < 0)
            {
                return false;
            }

            if (!TryResolveGestureBoardPoint(pointerPosition, out var boardWorldPoint))
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

            return TryUpdateConstrainedPointerGesture(activeBlock, boardWorldPoint);
        }

        private bool TryUpdateConstrainedPointerGesture(RuntimeBlockState activeBlock, Vector2 boardWorldPoint)
        {
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
            var requestedCellCount = Mathf.Abs(stepDelta);
            var direction = ResolveDirectionForAxis(_activeGestureAxis, stepSign);
            if (!TryMoveActiveGestureBlock(direction, requestedCellCount, out var movedCellCount))
            {
                return false;
            }

            _activeGestureAppliedStepCount += movedCellCount * stepSign;
            return true;
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

                if (!TryMoveActiveGestureBlock(direction, requestedCellCount, out var movedCellCount))
                {
                    break;
                }

                movedAny = true;
                AdvanceGestureStartPoint(direction, movedCellCount);

                deltaFromAnchor = boardWorldPoint - _activeGestureStartBoardPoint;
                if (deltaFromAnchor.sqrMagnitude < _dragActivationDistanceSqr)
                {
                    break;
                }
            }

            return movedAny;
        }

        private bool TryResolveGestureBoardPoint(Vector2 pointerPosition, out Vector2 boardWorldPoint)
        {
            boardWorldPoint = default;
            return inputCamera && TryResolveBoardPoint(pointerPosition, inputCamera, out boardWorldPoint);
        }

        private bool TryMoveActiveGestureBlock(Direction direction, int requestedCellCount, out int movedCellCount)
        {
            movedCellCount = 0;
            return requestedCellCount > 0 && _activeGestureBlockId >= 0 &&
                   TrySlideBlock(_activeGestureBlockId, direction, requestedCellCount, out movedCellCount);
        }

        private void AdvanceGestureStartPoint(Direction direction, int movedCellCount)
        {
            var consumedDistance = movedCellCount * _resolvedCellSize;
            var directionVector = direction.ToVector();
            _activeGestureStartBoardPoint.x += directionVector.x * consumedDistance;
            _activeGestureStartBoardPoint.y += directionVector.y * consumedDistance;
        }

        public void EndPointerGesture()
        {
            _activeGestureBlockId = -1;
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
            var currentlyOverlappingDoor = IsOverlappingAnyDoorOpening(block, currentPosition, _doorOpenings);

            while (requestedCells > 0)
            {
                requestedCells--;
                var nextPosition = currentPosition + directionVector;

                var canExitThroughDoor = DoorExitEvaluator.TryResolveDoorExit(block, nextPosition, direction, _doorOpenings,
                    out var resolvedDoor);
                var nextOverlapsDoor = IsOverlappingAnyDoorOpening(block, nextPosition, _doorOpenings);

                if (canExitThroughDoor && nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    matchedDoor = resolvedDoor;
                    reachedDoor = true;
                    hasMoved = true;
                    break;
                }

                if (!canExitThroughDoor && nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    break;
                }

                if (!_occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells))
                {
                    break;
                }

                currentPosition = nextPosition;
                currentlyOverlappingDoor = nextOverlapsDoor;
                hasMoved = true;
                movedCellCount++;

                if (!canExitThroughDoor)
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

            if (hasMoved && !reachedDoor)
            {
                var frontCellPosition = currentPosition + directionVector;
                if (DoorExitEvaluator.TryResolveDoorExit(block, frontCellPosition, direction, _doorOpenings,
                        out var frontDoor) &&
                    !IsOverlappingAnyDoorOpening(block, currentPosition, _doorOpenings))
                {
                    matchedDoor = frontDoor;
                    reachedDoor = true;
                }
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
                var exitDirection = matchedDoor.ResolveExitDirection(_gridDimensions);
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

        private static bool IsOverlappingAnyDoorOpening(RuntimeBlockState block, Vector2Int anchorPosition,
            IReadOnlyList<DoorOpeningData> doorOpenings)
        {
            if (doorOpenings == null || doorOpenings.Count == 0 || block.LocalCells == null || block.LocalCells.Length == 0)
            {
                return false;
            }

            for (var cellIndex = 0; cellIndex < block.LocalCells.Length; cellIndex++)
            {
                var worldCell = anchorPosition + block.LocalCells[cellIndex];
                for (var openingIndex = 0; openingIndex < doorOpenings.Count; openingIndex++)
                {
                    var opening = doorOpenings[openingIndex];
                    if (worldCell.x < opening.MinCell.x || worldCell.x > opening.MaxCell.x ||
                        worldCell.y < opening.MinCell.y || worldCell.y > opening.MaxCell.y)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool TryResolveDragAxis(Vector2 delta, BlockMovementConstraint movementConstraint,
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
            if (IsBelowDirectionDeadZone(absX, absY))
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

        private bool TryResolveDominantDirection(Vector2 delta, out Direction direction)
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

        private bool IsBelowDirectionDeadZone(float absX, float absY)
        {
            return absX < directionDeadZone && absY < directionDeadZone;
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

            var relativeX = boardWorldPoint.x - boardOrigin.x;
            var relativeY = boardWorldPoint.y - boardOrigin.y;
            return relativeX >= 0f && relativeX < _boardWidthWorld && relativeY >= 0f && relativeY < _boardHeightWorld;
        }

        private void RefreshInputPlane() =>
            _boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));

        private void RefreshCachedBoardMetrics()
        {
            _resolvedCellSize = Mathf.Max(0.01f, cellSize);
            _inverseCellSize = 1f / _resolvedCellSize;
            _boardWidthWorld = Mathf.Max(0, _gridDimensions.x) * _resolvedCellSize;
            _boardHeightWorld = Mathf.Max(0, _gridDimensions.y) * _resolvedCellSize;
            var dragActivationDistance = _resolvedCellSize * Mathf.Max(0.01f, dragActivationInCells);
            _dragActivationDistanceSqr = dragActivationDistance * dragActivationDistance;
        }
    }
}
