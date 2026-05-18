using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
        private readonly List<string> _validatorIssueBuffer = new(32);

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
            AppendValidatorIssues();
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
                    continue;
                }

                for (int cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                {
                    Vector2Int doorCell = _doorCellsBuffer[cellIndex];
                    if (_doorIndexByCell.ContainsKey(doorCell))
                    {
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

                Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
                if (!_validationOccupancyMap.CanPlace(i, block.position, localCells))
                {
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

        private void AppendValidatorIssues()
        {
            _validatorIssueBuffer.Clear();
            string sourceName = _activeLevelJson != null && !string.IsNullOrWhiteSpace(_activeLevelJson.name)
                ? _activeLevelJson.name
                : _activeLevel?.levelKey;
            LevelJsonValidator.ValidateLevel(_activeLevel, _shapeRegistry, _validatorIssueBuffer, sourceName);
            if (_validatorIssueBuffer.Count == 0)
            {
                return;
            }

            var seenIssues = new HashSet<string>(_layoutValidationIssues, StringComparer.Ordinal);
            for (int i = 0; i < _validatorIssueBuffer.Count; i++)
            {
                string issue = _validatorIssueBuffer[i];
                if (string.IsNullOrWhiteSpace(issue) || !seenIssues.Add(issue))
                {
                    continue;
                }

                _layoutValidationIssues.Add(issue);
            }
        }

    }
}
