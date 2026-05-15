using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    public class BoardController : MonoBehaviour
    {
        [Header("Movement Rules")]
        [SerializeField] private bool slideUntilBlocked;

        [Header("Board Transform")]
        [SerializeField] private float cellSize= 1f;
        [SerializeField] private Vector2 boardOrigin;

        [Header("Scene Block Views")]
        [SerializeField] private BlockView[] blockViews= Array.Empty<BlockView>();

        public event Action LevelCompleted;
        public event Action<int, Vector2Int> BlockMoved;
        public event Action<int> BlockCleared;

        public LevelData CurrentLevel { get; private set; }
        public bool IsInitialized { get; private set; }
        public int RemainingBlockCount => _runtimeBlocks.Count;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => boardOrigin;

        public void SetBlockViews(BlockView[] views)
        {
            blockViews = views ?? Array.Empty<BlockView>();
        }

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new Dictionary<int, RuntimeBlockState>();
        private readonly Dictionary<int, BlockView> _blockViewById = new Dictionary<int, BlockView>();
        private readonly DoorOpeningMap _doorOpeningMap = new DoorOpeningMap();
        private readonly BoardOccupancyMap _occupancyMap = new BoardOccupancyMap();

        public void Setup(LevelData levelData)
        {
            CurrentLevel = levelData;
            IsInitialized = false;

            _runtimeBlocks.Clear();
            _doorOpeningMap.Clear();
            CacheBlockViews();

            if (levelData == null)
            {
                Debug.LogError("BoardController.Setup: LevelData is null.");
                return;
            }

            _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y, levelData.blockedCells);

            _doorOpeningMap.Build(levelData.doors, levelData.gridDimensions);

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                var blockLocalCells = blockData.GetLocalCells();
                var blockBoundsSize = blockData.GetSize();

                var runtimeBlock = new RuntimeBlockState(
                    i,
                    blockData.position,
                    blockLocalCells,
                    blockBoundsSize,
                    blockData.movementConstraint,
                    blockData.colorType);

                if (!_occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    Debug.LogError($"Invalid block placement in level {levelData.levelNumber}, block index: {i}");
                    continue;
                }

                _runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                _occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }

            SyncBlockViews();
            IsInitialized = true;
            CheckLevelCompletion();
        }

        public bool TryMoveBlock(int blockId, Direction direction)
        {
            if (!IsInitialized || !_runtimeBlocks.TryGetValue(blockId, out RuntimeBlockState block))
            {
                return false;
            }

            if (!CanMoveInDirection(block.MovementConstraint, direction))
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, block.Position, block.LocalCells);

            int stepCount = slideUntilBlocked
                ? GetMaxTravelSteps(block, direction)
                : (CanMoveByOneStep(block, direction) ? 1 : 0);

            if (stepCount <= 0)
            {
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);
                return false;
            }

            block.Position += direction.ToVector() * stepCount;

            if (TryGetClearingOpening(block, out DoorOpeningData clearingOpening))
            {
                _runtimeBlocks.Remove(blockId);

                if (_blockViewById.TryGetValue(blockId, out BlockView clearView) && clearView != null)
                {
                    clearView.SnapToGridPosition(block.Position, cellSize, boardOrigin);
                    if (TryGetExitDirectionFromEdgeSide(clearingOpening.edgeSide, out Vector2Int exitDirection))
                    {
                        clearView.PlayDoorExitAnimation(exitDirection, cellSize, () => clearView.gameObject.SetActive(false));
                    }
                    else
                    {
                        clearView.gameObject.SetActive(false);
                    }
                }

                BlockCleared?.Invoke(blockId);
            }
            else
            {
                _runtimeBlocks[blockId] = block;
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);

                if (_blockViewById.TryGetValue(blockId, out BlockView view) && view != null)
                {
                    view.SetGridPosition(block.Position, cellSize, boardOrigin);
                }

                BlockMoved?.Invoke(blockId, block.Position);
            }

            CheckLevelCompletion();
            return true;
        }

        private void CacheBlockViews()
        {
            _blockViewById.Clear();

            foreach (var view in blockViews)
            {
                if (view == null)
                {
                    continue;
                }

                if (_blockViewById.ContainsKey(view.BlockId))
                {
                    Debug.LogWarning($"Duplicate BlockView id found: {view.BlockId}");
                    continue;
                }

                _blockViewById.Add(view.BlockId, view);
            }
        }

        private void SyncBlockViews()
        {
            foreach (KeyValuePair<int, BlockView> pair in _blockViewById)
            {
                var exists = _runtimeBlocks.TryGetValue(pair.Key, out RuntimeBlockState runtimeBlock);
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.gameObject.SetActive(exists);
                if (exists)
                {
                    pair.Value.SetGridPosition(runtimeBlock.Position, cellSize, boardOrigin);
                }
            }
        }

        private static bool CanMoveInDirection(BlockMovementConstraint movementConstraint, Direction inputDirection)
        {
            return movementConstraint switch
            {
                BlockMovementConstraint.HorizontalOnly => inputDirection.IsHorizontal(),
                BlockMovementConstraint.VerticalOnly => inputDirection.IsVertical(),
                _ => true
            };
        }

        private int GetMaxTravelSteps(RuntimeBlockState block, Direction direction)
        {
            var delta = direction.ToVector();
            var steps = 0;

            while (_occupancyMap.CanPlace(block.Id, block.Position + (delta * (steps + 1)), block.LocalCells))
            {
                steps++;
            }

            return steps;
        }

        private bool CanMoveByOneStep(RuntimeBlockState block, Direction direction)
        {
            var nextPosition = block.Position + direction.ToVector();
            return _occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells);
        }

        private bool TryGetClearingOpening(RuntimeBlockState block, out DoorOpeningData openingData)
        {
            foreach (var localCell in block.LocalCells)
            {
                var cell = new Vector2Int(block.Position.x + localCell.x, block.Position.y + localCell.y);

                if (_doorOpeningMap.TryGetOpening(cell, out DoorOpeningData opening) && opening.colorType == block.ColorType && CanBlockPassOpening(block, opening))
                {
                    openingData = opening;
                    return true;
                }
            }

            openingData = default;
            return false;
        }

        private void CheckLevelCompletion()
        {
            if (_runtimeBlocks.Count == 0)
            {
                LevelCompleted?.Invoke();
            }
        }

        private static bool CanBlockPassOpening(RuntimeBlockState block, DoorOpeningData opening)
        {
            GetBlockAxisInterval(block, opening.edgeSide, out int blockMin, out int blockMax);
            var blockWidthOnDoorAxis = (blockMax - blockMin) + 1;
            return blockWidthOnDoorAxis <= opening.OpeningWidth;
        }

        private static void GetBlockAxisInterval(RuntimeBlockState block, int edgeSide, out int minAxis, out int maxAxis)
        {
            var verticalAxis = edgeSide is 0 or 1;
            minAxis = int.MaxValue;
            maxAxis = int.MinValue;

            foreach (var cell in block.LocalCells)
            {
                var worldCell = block.Position + cell;
                var axisValue = verticalAxis ? worldCell.y : worldCell.x;
                if (axisValue < minAxis)
                {
                    minAxis = axisValue;
                }

                if (axisValue > maxAxis)
                {
                    maxAxis = axisValue;
                }
            }

            if (minAxis == int.MaxValue)
            {
                minAxis = 0;
                maxAxis = 0;
            }
        }

        private static bool TryGetExitDirectionFromEdgeSide(int edgeSide, out Vector2Int direction)
        {
            if (edgeSide == 0)
            {
                direction = Vector2Int.left;
                return true;
            }

            if (edgeSide == 1)
            {
                direction = Vector2Int.right;
                return true;
            }

            if (edgeSide == 2)
            {
                direction = Vector2Int.down;
                return true;
            }

            if (edgeSide == 3)
            {
                direction = Vector2Int.up;
                return true;
            }

            direction = Vector2Int.zero;
            return false;
        }
    }
}
