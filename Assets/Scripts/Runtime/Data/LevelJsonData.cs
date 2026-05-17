using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public sealed class LevelJsonData
    {
        private static readonly List<BlockColor> DefaultColors = new()
        {
            BlockColor.Red,
            BlockColor.Blue,
            BlockColor.Green
        };

        public string levelKey = string.Empty;
        public int levelNumber = 1;
        public float timeLimit = 60f;
        public Vector2Int gridDimensions = new(6, 8);
        public List<Vector2Int> blockedCells = new();

        public List<BlockColor> availableColors = new()
        {
            BlockColor.Red,
            BlockColor.Blue,
            BlockColor.Green
        };

        public List<string> availableShapeKeys = new();
        public List<DoorData> doors = new();
        public List<LevelJsonBlockData> blocks = new();

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

            blockedCells ??= new List<Vector2Int>();
            availableColors = SanitizeColors(availableColors);
            availableShapeKeys = SanitizeShapeKeys(availableShapeKeys);
            doors ??= new List<DoorData>();
            blocks ??= new List<LevelJsonBlockData>();
            SynchronizeBlockShapeKeys();

            _doorCellsBuffer ??= new List<Vector2Int>(8);
            for (var i = doors.Count - 1; i >= 0; i--)
            {
                var door = doors[i];
                door.openingWidth = Mathf.Max(1, door.openingWidth);

                if (!DoorOpeningMap.TryCollectDoorCells(door, gridDimensions, _doorCellsBuffer))
                {
                    doors.RemoveAt(i);
                    continue;
                }

                doors[i] = door;
            }

            _isDoorOpeningsCacheDirty = true;
        }

        private void SynchronizeBlockShapeKeys()
        {
            if (blocks == null || blocks.Count == 0)
            {
                return;
            }

            var knownShapeKeys = new HashSet<string>(availableShapeKeys, StringComparer.Ordinal);

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                block.shapeKey = string.IsNullOrWhiteSpace(block.shapeKey) ? string.Empty : block.shapeKey.Trim();
                block.NormalizeBlockType();
                blocks[i] = block;

                if (string.IsNullOrWhiteSpace(block.shapeKey))
                {
                    continue;
                }

                if (knownShapeKeys.Add(block.shapeKey))
                {
                    availableShapeKeys.Add(block.shapeKey);
                }
            }
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

        private static List<string> SanitizeShapeKeys(List<string> source)
        {
            var result = new List<string>();
            if (source == null || source.Count == 0)
            {
                return result;
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < source.Count; i++)
            {
                var key = source[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                key = key.Trim();
                if (unique.Add(key))
                {
                    result.Add(key);
                }
            }

            return result;
        }
    }
}
