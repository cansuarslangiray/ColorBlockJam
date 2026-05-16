using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardController : MonoBehaviour
    {
        private const float DragActivationInCells = 0.35f;

        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 boardOrigin;
        [SerializeField] private BoardGameplayConfig gameplayConfig;

        [SerializeField] private Camera inputCamera;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int> BlockCleared;

        private LevelData CurrentLevel { get; set; }
        public bool IsInitialized { get; private set; }
        private int RuntimeBlockCount { get; set; }
        private bool _levelCompletedRaised;

        public Vector2Int GridDimensions => CurrentLevel ? CurrentLevel.gridDimensions : Vector2Int.zero;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => boardOrigin;
        public BoardGameplayConfig GameplayConfig => gameplayConfig;

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new();
        private readonly List<DoorOpeningData> _doorOpenings = new();
        private readonly BoardOccupancyMap _occupancyMap = new();

        private Plane _boardPlane = new(Vector3.forward, Vector3.zero);
        private int _activeDragBlockId = -1;
        private Vector2 _activeDragBoardPoint;
        private Camera _activeDragCamera;

        private bool IsBoardReadyForInput => IsInitialized && StateManager.HasInstance &&
                                             StateManager.Instance.CurrentState == GameState.Playing;

        private void Awake()
        {
            RefreshInputPlane();
        }

        private void OnValidate()
        {
            RefreshInputPlane();
        }

        public void Setup(LevelData levelData)
        {
            IsInitialized = false;
            _runtimeBlocks.Clear();
            _doorOpenings.Clear();
            RuntimeBlockCount = 0;
            _levelCompletedRaised = false;
            EndPointerGesture();

            if (!levelData)
            {
                CurrentLevel = null;
                return;
            }

            CurrentLevel = levelData;
            _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            if (levelData.blockedCells != null && levelData.blockedCells.Count > 0)
            {
                _occupancyMap.MarkBlockedCells(levelData.blockedCells);
            }

            var openings = levelData.GetDoorOpenings();
            for (var i = 0; i < openings.Count; i++)
            {
                _doorOpenings.Add(openings[i]);
            }

            if (levelData.blocks != null)
            {
                for (var i = 0; i < levelData.blocks.Count; i++)
                {
                    var blockData = levelData.blocks[i];
                    var blockLocalCells = blockData.GetLocalCells();
                    var runtimeBlock = new RuntimeBlockState(i, blockData.position, blockLocalCells,
                        blockData.movementConstraint, blockData.colorType);

                    if (!_occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                    {
                        continue;
                    }

                    _runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                    _occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
                }
            }

            RuntimeBlockCount = _runtimeBlocks.Count;
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

            _activeDragBlockId = blockId;
            _activeDragBoardPoint = boardWorldPoint;
            _activeDragCamera = resolvedCamera;
            return true;
        }

        public bool TryUpdatePointerGesture(Vector2 pointerPosition, Camera pointerCamera)
        {
            if (!IsBoardReadyForInput || _activeDragBlockId < 0)
            {
                return false;
            }

            var resolvedCamera = pointerCamera ? pointerCamera : _activeDragCamera ? _activeDragCamera : inputCamera;
            if (!resolvedCamera)
            {
                return false;
            }

            if (!TryResolveBoardPoint(pointerPosition, resolvedCamera, out var boardWorldPoint))
            {
                return false;
            }

            var delta = boardWorldPoint - _activeDragBoardPoint;
            var threshold = cellSize * DragActivationInCells;
            if (delta.sqrMagnitude < threshold * threshold)
            {
                return false;
            }

            _activeDragBoardPoint = boardWorldPoint;
            if (!TryResolveDragDirection(delta, out var direction))
            {
                return false;
            }

            return TrySlideBlock(_activeDragBlockId, direction);
        }

        public void EndPointerGesture()
        {
            _activeDragBlockId = -1;
            _activeDragCamera = null;
            _activeDragBoardPoint = default;
        }

        private bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
        {
            cell = default;
            if (!IsInitialized || cellSize <= 0f)
            {
                return false;
            }

            var cellX = Mathf.FloorToInt((worldPosition.x - boardOrigin.x) / cellSize);
            var cellY = Mathf.FloorToInt((worldPosition.y - boardOrigin.y) / cellSize);
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

        private bool TrySlideBlock(int blockId, Direction direction)
        {
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
            var nextPosition = startPosition + directionVector;
            if (!_occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells))
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, startPosition, block.LocalCells);
            block.Position = nextPosition;

            if (TryGetDoorExit(block, nextPosition, direction, out _))
            {
                _runtimeBlocks.Remove(blockId);
                BlockCleared?.Invoke(blockId, block.Position, directionVector);
                if (_activeDragBlockId == blockId)
                {
                    _activeDragBlockId = -1;
                }

                RuntimeBlockCount = _runtimeBlocks.Count;
                EvaluateCompletionState();
                return true;
            }

            _runtimeBlocks[blockId] = block;
            _occupancyMap.FillBlock(blockId, nextPosition, block.LocalCells);
            BlockMoved?.Invoke(blockId, startPosition, nextPosition);
            return true;
        }

        private static bool TryResolveDragDirection(Vector2 delta, out Direction direction)
        {
            direction = default;
            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (absX < 0.0001f && absY < 0.0001f)
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

            if (RuntimeBlockCount == 0)
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
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0 || cellSize <= 0f)
            {
                return false;
            }

            var width = gridDimensions.x * cellSize;
            var height = gridDimensions.y * cellSize;
            var relativeX = boardWorldPoint.x - boardOrigin.x;
            var relativeY = boardWorldPoint.y - boardOrigin.y;
            return relativeX >= 0f && relativeX < width && relativeY >= 0f && relativeY < height;
        }

        private void RefreshInputPlane()
        {
            _boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        }

        private bool TryGetDoorExit(RuntimeBlockState block, Vector2Int blockPosition, Direction moveDirection,
            out DoorOpeningData doorExit)
        {
            doorExit = default;

            var localCells = block.LocalCells;
            if (localCells == null || localCells.Length == 0 || _doorOpenings.Count == 0)
            {
                return false;
            }

            GetBlockBounds(localCells, blockPosition, out var minX, out var maxX, out var minY, out var maxY);

            for (var i = 0; i < _doorOpenings.Count; i++)
            {
                var opening = _doorOpenings[i];
                if (opening.ColorType != block.ColorType || opening.EdgeDirection != moveDirection)
                {
                    continue;
                }

                if (!IsBlockTouchingDoorEdge(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!IsBlockInsideDoorSpan(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!FitsInsideDoor(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                doorExit = opening;
                return true;
            }

            return false;
        }

        private static bool FitsInsideDoor(DoorOpeningData opening, Direction moveDirection, int minX, int maxX,
            int minY, int maxY)
        {
            var widthOnDoorAxis = moveDirection.IsHorizontal() ? (maxY - minY) + 1 : (maxX - minX) + 1;
            return widthOnDoorAxis <= opening.OpeningWidth;
        }

        private static void GetBlockBounds(Vector2Int[] localCells, Vector2Int blockPosition, out int minX,
            out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            for (var i = 0; i < localCells.Length; i++)
            {
                var worldCell = blockPosition + localCells[i];
                if (worldCell.x < minX) minX = worldCell.x;
                if (worldCell.x > maxX) maxX = worldCell.x;
                if (worldCell.y < minY) minY = worldCell.y;
                if (worldCell.y > maxY) maxY = worldCell.y;
            }
        }

        private static bool IsBlockTouchingDoorEdge(DoorOpeningData opening, Direction moveDirection, int minX,
            int maxX, int minY, int maxY)
        {
            return moveDirection switch
            {
                Direction.Left => minX == opening.MinCell.x,
                Direction.Right => maxX == opening.MaxCell.x,
                Direction.Down => minY == opening.MinCell.y,
                Direction.Up => maxY == opening.MaxCell.y,
                _ => false
            };
        }

        private static bool IsBlockInsideDoorSpan(DoorOpeningData opening, Direction moveDirection, int minX,
            int maxX, int minY, int maxY)
        {
            if (moveDirection.IsHorizontal())
            {
                return minY >= opening.MinCell.y && maxY <= opening.MaxCell.y;
            }
            return minX >= opening.MinCell.x && maxX <= opening.MaxCell.x;
        }

    }
}
