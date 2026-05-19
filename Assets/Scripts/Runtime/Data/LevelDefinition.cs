using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "ColorBlockJam/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        private static readonly List<BlockColor> DefaultColors = CreateDefaultColors();

        public string levelKey = string.Empty;
        public int levelNumber = 1;
        public float timeLimit = 60f;
        public Vector2Int gridDimensions = new(6, 8);
        public List<Vector2Int> blockedCells = new();
        public List<BlockColor> availableColors = CreateDefaultColors();
        public List<DoorData> doors = new();
        public List<LevelBlockEntry> blocks = new();

        [NonSerialized] private List<DoorOpeningData> _cachedDoorOpenings = new();
        [NonSerialized] private bool _isDoorOpeningsCacheDirty = true;
        [NonSerialized] private List<Vector2Int> _doorCellsBuffer = new(8);

        public IReadOnlyList<DoorOpeningData> GetDoorOpenings()
        {
            EnsureDoorOpeningsCache();
            return _cachedDoorOpenings;
        }

        public void Sanitize()
        {
            levelKey = string.IsNullOrWhiteSpace(levelKey) ? string.Empty : levelKey.Trim();
            levelNumber = Mathf.Max(1, levelNumber);
            timeLimit = Mathf.Max(1f, timeLimit);
            gridDimensions = new Vector2Int(Mathf.Max(1, gridDimensions.x), Mathf.Max(1, gridDimensions.y));

            blockedCells = SanitizeBlockedCells(blockedCells, gridDimensions);
            availableColors = SanitizeColors(availableColors);
            doors = SanitizeDoors(doors, gridDimensions, _doorCellsBuffer);

            blocks ??= new List<LevelBlockEntry>();
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                block.Normalize();
                blocks[i] = block;
            }

            _doorCellsBuffer ??= new List<Vector2Int>(8);
            _isDoorOpeningsCacheDirty = true;
        }

        private void EnsureDoorOpeningsCache()
        {
            _cachedDoorOpenings ??= new List<DoorOpeningData>();
            if (!_isDoorOpeningsCacheDirty)
            {
                return;
            }

            DoorOpeningMap.BuildOpenings(doors, gridDimensions, _cachedDoorOpenings);
            _isDoorOpeningsCacheDirty = false;
        }

        private static List<BlockColor> SanitizeColors(List<BlockColor> source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<BlockColor>(DefaultColors);
            }

            return new List<BlockColor>(source);
        }

        private static List<BlockColor> CreateDefaultColors()
        {
            var colors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
            return new List<BlockColor>(colors);
        }

        private static List<Vector2Int> SanitizeBlockedCells(List<Vector2Int> source, Vector2Int dimensions)
        {
            var result = new List<Vector2Int>();
            if (source == null || source.Count == 0)
            {
                return result;
            }

            var uniqueCells = new HashSet<Vector2Int>();
            for (var i = 0; i < source.Count; i++)
            {
                var cell = source[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= dimensions.x || cell.y >= dimensions.y)
                {
                    continue;
                }

                if (uniqueCells.Add(cell))
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        private static List<DoorData> SanitizeDoors(List<DoorData> source, Vector2Int dimensions, List<Vector2Int> buffer)
        {
            var result = new List<DoorData>();
            if (source == null || source.Count == 0)
            {
                return result;
            }

            buffer ??= new List<Vector2Int>(8);
            var uniqueCells = new HashSet<Vector2Int>();

            for (var i = 0; i < source.Count; i++)
            {
                var door = source[i];
                if (!DoorOpeningMap.TryCollectDoorCells(door, dimensions, buffer))
                {
                    continue;
                }

                var doorCell = buffer[0];
                if (!uniqueCells.Add(doorCell))
                {
                    continue;
                }

                door.position = doorCell;
                result.Add(door);
            }

            return result;
        }
    }
}
