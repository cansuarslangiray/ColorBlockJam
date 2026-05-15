using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "ColorBlockJam/LevelData")]
    public class LevelData : ScriptableObject
    {
        [Header("Level Settings")] public int levelNumber = 1;

        public float timeLimit = 60f;

        [Header("Grid Configurations")] public Vector2Int gridDimensions = new(6, 8);

        public List<Vector2Int> blockedCells = new();

        [Header("Level Editor Data")] public List<BlockColor> availableColors = new List<BlockColor>
        {
            BlockColor.Red,
            BlockColor.Blue,
            BlockColor.Green
        };

        public List<BlockShapeData> availableShapes = new();

        public List<DoorData> doors = new();

        [Header("Block Placements")] public List<BlockData> blocks = new();

        private void OnValidate()
        {
            if (gridDimensions.x < 1) gridDimensions.x = 1;
            if (gridDimensions.y < 1) gridDimensions.y = 1;
            if (timeLimit < 1f) timeLimit = 1f;

            blockedCells ??= new List<Vector2Int>();
            availableColors ??= new List<BlockColor>();
            availableShapes ??= new List<BlockShapeData>();
            doors ??= new List<DoorData>();
            blocks ??= new List<BlockData>();

            var doorCellsBuffer = new List<Vector2Int>(8);
            for (var i = doors.Count - 1; i >= 0; i--)
            {
                var door = doors[i];
                door.openingWidth = Mathf.Max(1, door.openingWidth);

                if (!DoorOpeningMap.TryCollectDoorCells(door, gridDimensions, doorCellsBuffer))
                {
                    doors.RemoveAt(i);
                    continue;
                }

                doors[i] = door;
            }
        }
    }
}