using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
        private void EnsureGridLookupCache()
        {
            if (!_gridLookupCacheDirty || _activeLevel == null)
            {
                return;
            }

            _gridLookupCacheDirty = false;
            _blockedCellLookup.Clear();
            _doorIndexByCell.Clear();
            _blockIndexByCell.Clear();
            _layoutValidationIssues.Clear();

            if (_activeLevel.blockedCells != null)
            {
                for (int i = 0; i < _activeLevel.blockedCells.Count; i++)
                {
                    _blockedCellLookup.Add(_activeLevel.blockedCells[i]);
                }
            }

            BuildDoorLookup();
            BuildBlockLookupAndValidation();
        }

        private void BuildDoorLookup()
        {
            if (_activeLevel.doors == null)
            {
                return;
            }

            for (int i = 0; i < _activeLevel.doors.Count; i++)
            {
                DoorData door = _activeLevel.doors[i];
                _doorCellsBuffer.Clear();
                if (!DoorOpeningMap.TryCollectDoorCells(door, _activeLevel.gridDimensions, _doorCellsBuffer))
                {
                    _layoutValidationIssues.Add($"Door #{i} gecersiz: Pos={door.position}");
                    continue;
                }

                for (int cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                {
                    Vector2Int doorCell = _doorCellsBuffer[cellIndex];
                    if (_doorIndexByCell.ContainsKey(doorCell))
                    {
                        _layoutValidationIssues.Add($"Door #{i} hucre cakismasi: {doorCell}");
                        continue;
                    }

                    _doorIndexByCell.Add(doorCell, i);
                }
            }
        }

        private void BuildBlockLookupAndValidation()
        {
            Vector2Int grid = _activeLevel.gridDimensions;
            _validationOccupancyMap.Configure(grid.x, grid.y);
            _validationOccupancyMap.MarkBlockedCells(_activeLevel.blockedCells);
            BoardFrameMap.CollectFrameCellsExceptDoorOpenings(grid, _activeLevel.GetDoorOpenings(), _frameCellsBuffer);
            _validationOccupancyMap.MarkBlockedCells(_frameCellsBuffer);

            if (_activeLevel.blocks == null)
            {
                return;
            }

            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];

                if (!string.IsNullOrWhiteSpace(block.shapeKey) &&
                    (_shapeRegistry == null || !_shapeRegistry.TryResolveShape(block.shapeKey, out _)))
                {
                    _layoutValidationIssues.Add(
                        $"Blok #{i} cozulemeyen shape '{block.shapeKey.Trim()}'. Runtime 1x1 fallback uygular.");
                }

                Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
                if (!_validationOccupancyMap.CanPlace(i, block.position, localCells))
                {
                    string shapeLabel = string.IsNullOrWhiteSpace(block.shapeKey) ? "1x1(default)" : block.shapeKey.Trim();
                    _layoutValidationIssues.Add(
                        $"Blok #{i} yerlestirilemedi: Shape={shapeLabel}, Pos={block.position}, Cells={FormatWorldCells(block.position, localCells)}");
                    continue;
                }

                _validationOccupancyMap.FillBlock(i, block.position, localCells);
                for (int cellIndex = 0; cellIndex < localCells.Length; cellIndex++)
                {
                    Vector2Int worldCell = block.position + localCells[cellIndex];
                    if (!_blockIndexByCell.ContainsKey(worldCell))
                    {
                        _blockIndexByCell.Add(worldCell, i);
                    }
                }
            }
        }

        private bool IsBlockedCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _blockedCellLookup.Contains(cell);
        }

        private void MarkGridLookupCacheDirty()
        {
            _gridLookupCacheDirty = true;
        }

        private static string FormatWorldCells(Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return "[]";
            }

            var worldCells = new string[localCells.Length];
            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                worldCells[i] = $"({worldCell.x},{worldCell.y})";
            }

            return $"[{string.Join(", ", worldCells)}]";
        }
    }
}
