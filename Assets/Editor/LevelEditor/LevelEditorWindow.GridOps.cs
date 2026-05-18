using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
        private void HandleCellClick(Vector2Int cell)
        {
            if (_editMode != LevelEditorMode.Doors && IsFrameCell(cell))
            {
                ShowNotification(new GUIContent("Kenar hucreler border alani. Bu alana blok/blocked yerlestirilemez."));
                return;
            }

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
                ShowNotification(new GUIContent("Kenar hucreler border alani olarak ayrildi."));
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

            if (!IsEdgeCell(cell))
            {
                ShowNotification(new GUIContent("Door sadece kenar hücresine konabilir."));
                return;
            }

            if (IsCornerCell(cell))
            {
                ShowNotification(new GUIContent("Door kose hucreye konamaz. Kosenin yanindaki kenar hucreyi sec."));
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
                ShowNotification(new GUIContent("Door bu hücreye eklenemiyor."));
                return;
            }

            RecordLevelChange("Toggle Door");
            RemoveDoorsOnCells(_doorCellsBuffer);

            for (int i = 0; i < _doorCellsBuffer.Count; i++)
            {
                Vector2Int doorCell = _doorCellsBuffer[i];
                _activeLevel.blockedCells.Remove(doorCell);
                RemoveBlocksIntersectingCell(doorCell);
            }

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
                movementConstraint = _selectedBlockMovementConstraint,
                colorType = _selectedBlockColor
            };

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

        private Color GetCellColor(Vector2Int cell)
        {
            if (IsBlockedCell(cell))
            {
                return new Color(0.22f, 0.22f, 0.22f);
            }

            int blockIndex = GetBlockAtCell(cell);
            if (blockIndex >= 0)
            {
                BlockColor color = _activeLevel.blocks[blockIndex].colorType;
                Color baseColor = BlockColorUtility.GetColor(color);
                baseColor.a = 0.9f;
                return baseColor;
            }

            int doorIndex = GetDoorIndexAtCell(cell);
            if (doorIndex >= 0)
            {
                BlockColor color = _activeLevel.doors[doorIndex].colorType;
                Color doorColor = BlockColorUtility.GetColor(color);
                return Color.Lerp(doorColor, Color.white, 0.35f);
            }

            if (IsFrameCell(cell))
            {
                return new Color(0.33f, 0.36f, 0.49f);
            }

            return new Color(0.9f, 0.9f, 0.9f);
        }

        private string GetCellLabel(Vector2Int cell)
        {
            if (IsBlockedCell(cell))
            {
                return "X";
            }

            if (GetBlockAtCell(cell) >= 0)
            {
                return "B";
            }

            if (GetDoorIndexAtCell(cell) >= 0)
            {
                return "D";
            }

            return string.Empty;
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
            while (true)
            {
                int index = GetDoorIndexAtCell(cell);
                if (index < 0)
                {
                    return;
                }

                _activeLevel.doors.RemoveAt(index);
                MarkGridLookupCacheDirty();
            }
        }

        private void RemoveDoorsOnCells(List<Vector2Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                RemoveDoorsOnCell(cells[i]);
            }
        }

        private void RemoveBlocksIntersectingCell(Vector2Int cell)
        {
            while (true)
            {
                int index = GetBlockAtCell(cell);
                if (index < 0)
                {
                    return;
                }

                _activeLevel.blocks.RemoveAt(index);
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

        private bool IsEdgeCell(Vector2Int cell)
        {
            return IsFrameCell(cell);
        }

        private bool IsCornerCell(Vector2Int cell)
        {
            return DoorOpeningMap.IsCornerCell(cell, _activeLevel.gridDimensions);
        }

        private bool IsFrameCell(Vector2Int cell)
        {
            return BoardFrameMap.IsFrameCell(cell, _activeLevel.gridDimensions);
        }
    }
}
