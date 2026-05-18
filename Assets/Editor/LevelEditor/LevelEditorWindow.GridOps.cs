using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
        private void HandleCellClick(Vector2Int cell)
        {
            switch (_editMode)
            {
                case LevelEditorMode.BlockedCells:
                    ToggleBlockedCell(cell);
                    break;
                case LevelEditorMode.Doors:
                    ToggleDoor(cell);
                    break;
                case LevelEditorMode.Blocks:
                    ToggleBlock(cell);
                    break;
            }
        }

        
        private void ToggleBlockedCell(Vector2Int cell)
        {
            if (IsFrameCell(cell))
            {
                return;
            }

            int index = _activeLevel.blockedCells.IndexOf(cell);
            RecordLevelChange("Toggle Blocked Cell");

            if (index >= 0)
            {
                _activeLevel.blockedCells.RemoveAt(index);
            }
            else
            {
                _activeLevel.blockedCells.Add(cell);
                RemoveDoorsOnCell(cell);
                RemoveBlocksIntersectingCell(cell);
            }

            SaveLevelChange();
        }

        private void ToggleDoor(Vector2Int cell)
        {
            int existingIndex = GetDoorIndexAtCell(cell);
            if (existingIndex >= 0)
            {
                RecordLevelChange("Toggle Door");
                _activeLevel.doors.RemoveAt(existingIndex);
                SaveLevelChange();
                return;
            }

            if (!IsFrameCell(cell))
            {
                ShowNotification(DoorMustBeEdgeNotification);
                return;
            }

            if (DoorOpeningMap.IsCornerCell(cell, _activeLevel.gridDimensions))
            {
                ShowNotification(DoorCannotBeCornerNotification);
                return;
            }

            DoorData nextDoor = new DoorData
            {
                position = cell,
                colorType = _selectedDoorColor
            };

            _doorCellsBuffer.Clear();
            if (!DoorOpeningMap.TryCollectDoorCells(nextDoor, _activeLevel.gridDimensions, _doorCellsBuffer))
            {
                ShowNotification(DoorCannotBePlacedNotification);
                return;
            }

            RecordLevelChange("Toggle Door");
            RemoveDoorsOnCells(_doorCellsBuffer);

            for (int i = 0; i < _doorCellsBuffer.Count; i++)
            {
                Vector2Int doorCell = _doorCellsBuffer[i];
                _activeLevel.blockedCells.Remove(doorCell);
            }
            RemoveBlocksIntersectingCells(_doorCellsBuffer);

            _activeLevel.doors.Add(nextDoor);
            SaveLevelChange();
        }

        private void ToggleBlock(Vector2Int anchorCell)
        {
            int existingAnchorIndex = GetBlockByAnchor(anchorCell);

            if (existingAnchorIndex >= 0)
            {
                RecordLevelChange("Toggle Block");
                _activeLevel.blocks.RemoveAt(existingAnchorIndex);
                SaveLevelChange();
                return;
            }

            if (_selectedBlockShape == null)
            {
                return;
            }

            if (!CanPlaceShape(anchorCell, _selectedBlockShape))
            {
                return;
            }

            RecordLevelChange("Toggle Block");

            if (!ContainsShapeKey(_activeLevel.availableShapeKeys, _selectedBlockShape.ShapeKey))
            {
                _activeLevel.availableShapeKeys.Add(_selectedBlockShape.ShapeKey);
            }

            LevelJsonBlockData block = new LevelJsonBlockData
            {
                position = anchorCell,
                shapeKey = _selectedBlockShape.ShapeKey,
                blockType = BlockShapeTypeUtility.FromShapeKey(_selectedBlockShape.ShapeKey),
                blockFeatures = _selectedBlockFeatures,
                colorType = _selectedBlockColor
            };
            block.NormalizeMovementConstraint();

            _activeLevel.blocks.Add(block);
            SaveLevelChange();
        }

        private bool CanPlaceShape(Vector2Int anchorPosition, BlockShapeJsonData shape)
        {
            if (shape == null)
            {
                return false;
            }

            Vector2Int[] localCells = shape.GetLocalCells();

            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                if (worldCell.x < 0 || worldCell.y < 0 ||
                    worldCell.x >= _activeLevel.gridDimensions.x ||
                    worldCell.y >= _activeLevel.gridDimensions.y)
                {
                    return false;
                }

                if (IsFrameCell(worldCell))
                {
                    return false;
                }

                if (IsBlockedCell(worldCell))
                {
                    return false;
                }

                if (GetBlockAtCell(worldCell) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanReplaceBlockShape(int blockIndex, Vector2Int anchorPosition, BlockShapeJsonData shape)
        {
            if (shape == null)
            {
                return false;
            }

            Vector2Int[] localCells = shape.GetLocalCells();

            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                if (worldCell.x < 0 || worldCell.y < 0 ||
                    worldCell.x >= _activeLevel.gridDimensions.x ||
                    worldCell.y >= _activeLevel.gridDimensions.y)
                {
                    return false;
                }

                if (IsFrameCell(worldCell))
                {
                    return false;
                }

                if (IsBlockedCell(worldCell))
                {
                    return false;
                }

                int occupiedBlockIndex = GetBlockAtCell(worldCell);
                if (occupiedBlockIndex >= 0 && occupiedBlockIndex != blockIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetDoorIndexAtCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _doorIndexByCell.TryGetValue(cell, out int index) ? index : -1;
        }

        private int GetBlockByAnchor(Vector2Int anchor)
        {
            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                if (_activeLevel.blocks[i].position == anchor)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetBlockAtCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _blockIndexByCell.TryGetValue(cell, out int index) ? index : -1;
        }

        private void RemoveDoorsOnCell(Vector2Int cell)
        {
            _cellSelectionBuffer.Clear();
            _cellSelectionBuffer.Add(cell);
            RemoveDoorsOnSelection(_cellSelectionBuffer);
        }

        private void RemoveDoorsOnCells(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            _cellSelectionBuffer.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                _cellSelectionBuffer.Add(cells[i]);
            }

            RemoveDoorsOnSelection(_cellSelectionBuffer);
        }

        private void RemoveDoorsOnSelection(HashSet<Vector2Int> targetCells)
        {
            if (targetCells == null || targetCells.Count == 0 || _activeLevel.doors.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            for (int i = _activeLevel.doors.Count - 1; i >= 0; i--)
            {
                _doorCellsBuffer.Clear();
                if (!DoorOpeningMap.TryCollectDoorCells(_activeLevel.doors[i], _activeLevel.gridDimensions, _doorCellsBuffer))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                {
                    if (!targetCells.Contains(_doorCellsBuffer[cellIndex]))
                    {
                        continue;
                    }

                    _activeLevel.doors.RemoveAt(i);
                    removedAny = true;
                    break;
                }
            }

            if (removedAny)
            {
                MarkGridLookupCacheDirty();
            }
        }

        private void RemoveBlocksIntersectingCell(Vector2Int cell)
        {
            _cellSelectionBuffer.Clear();
            _cellSelectionBuffer.Add(cell);
            RemoveBlocksOnSelection(_cellSelectionBuffer);
        }

        private void RemoveBlocksIntersectingCells(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            _cellSelectionBuffer.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                _cellSelectionBuffer.Add(cells[i]);
            }

            RemoveBlocksOnSelection(_cellSelectionBuffer);
        }

        private void RemoveBlocksOnSelection(HashSet<Vector2Int> targetCells)
        {
            if (targetCells == null || targetCells.Count == 0 || _activeLevel.blocks.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];
                Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
                bool shouldRemove = false;

                for (int cellIndex = 0; cellIndex < localCells.Length; cellIndex++)
                {
                    Vector2Int worldCell = block.position + localCells[cellIndex];
                    if (!targetCells.Contains(worldCell))
                    {
                        continue;
                    }

                    shouldRemove = true;
                    break;
                }

                if (!shouldRemove)
                {
                    continue;
                }

                _activeLevel.blocks.RemoveAt(i);
                removedAny = true;
            }

            if (removedAny)
            {
                MarkGridLookupCacheDirty();
            }
        }

        private void RemoveDoorsWithColor(BlockColor color)
        {
            for (int i = _activeLevel.doors.Count - 1; i >= 0; i--)
            {
                if (_activeLevel.doors[i].colorType == color)
                {
                    _activeLevel.doors.RemoveAt(i);
                }
            }
        }

        private void RemoveBlocksWithColor(BlockColor color)
        {
            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                if (_activeLevel.blocks[i].colorType == color)
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
            }
        }

        private void ClampLevelJsonToGrid()
        {
            Vector2Int grid = _activeLevel.gridDimensions;

            for (int i = _activeLevel.blockedCells.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = _activeLevel.blockedCells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y || IsFrameCell(cell))
                {
                    _activeLevel.blockedCells.RemoveAt(i);
                }
            }

            for (int i = _activeLevel.doors.Count - 1; i >= 0; i--)
            {
                DoorData door = _activeLevel.doors[i];
                if (!DoorOpeningMap.TryCollectDoorCells(door, grid, _doorCellsBuffer))
                {
                    _activeLevel.doors.RemoveAt(i);
                }
            }

            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];
                if (!IsBlockWithinGrid(block, grid))
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
            }

            MarkGridLookupCacheDirty();
        }

        private bool IsBlockWithinGrid(LevelJsonBlockData block, Vector2Int gridSize)
        {
            Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int cell = block.position + localCells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= gridSize.x || cell.y >= gridSize.y)
                {
                    return false;
                }

                if (BoardFrameMap.IsFrameCell(cell, gridSize))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsFrameCell(Vector2Int cell)
        {
            return BoardFrameMap.IsFrameCell(cell, _activeLevel.gridDimensions);
        }
    }
}
