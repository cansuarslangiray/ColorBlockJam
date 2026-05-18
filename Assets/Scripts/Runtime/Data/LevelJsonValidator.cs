using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Data
{
    public static class LevelJsonValidator
    {
        public static void ValidateLevel(
            LevelJsonData levelData,
            BlockShapeRegistry shapeRegistry,
            IList<string> issues,
            string sourceName = null)
        {
            if (issues == null)
            {
                return;
            }

            string context = string.IsNullOrWhiteSpace(sourceName)
                ? (levelData != null && !string.IsNullOrWhiteSpace(levelData.levelKey) ? levelData.levelKey : "Level")
                : sourceName.Trim();

            if (levelData == null)
            {
                AddIssue(issues, context, "Level JSON okunamadi veya parse edilemedi.");
                return;
            }

            if (string.IsNullOrWhiteSpace(levelData.levelKey))
            {
                AddIssue(issues, context, "levelKey bos.");
            }

            if (levelData.levelNumber < 1)
            {
                AddIssue(issues, context, $"levelNumber gecersiz: {levelData.levelNumber}");
            }

            if (levelData.gridDimensions.x < 3 || levelData.gridDimensions.y < 3)
            {
                AddIssue(issues, context,
                    $"Grid cok kucuk ({levelData.gridDimensions.x}x{levelData.gridDimensions.y}). En az 3x3 onerilir.");
            }

            var availableColors = new HashSet<BlockColor>(levelData.availableColors ?? new List<BlockColor>());
            var availableShapeKeys = new HashSet<string>(
                levelData.availableShapeKeys ?? new List<string>(),
                StringComparer.Ordinal);

            var doorCellLookup = new HashSet<Vector2Int>();
            var doorCellsBuffer = new List<Vector2Int>(8);
            if (levelData.doors != null)
            {
                for (int i = 0; i < levelData.doors.Count; i++)
                {
                    DoorData door = levelData.doors[i];
                    if (!DoorOpeningMap.TryCollectDoorCells(door, levelData.gridDimensions, doorCellsBuffer))
                    {
                        AddIssue(issues, context, $"Door #{i} gecersiz konumda: {door.position}");
                        continue;
                    }

                    if (!availableColors.Contains(door.colorType))
                    {
                        AddIssue(issues, context,
                            $"Door #{i} colorType '{door.colorType}' availableColors listesinde yok.");
                    }

                    for (int cellIndex = 0; cellIndex < doorCellsBuffer.Count; cellIndex++)
                    {
                        Vector2Int doorCell = doorCellsBuffer[cellIndex];
                        if (!doorCellLookup.Add(doorCell))
                        {
                            AddIssue(issues, context, $"Door cakismasi: {doorCell}");
                        }
                    }
                }
            }

            var blockedCells = levelData.blockedCells ?? new List<Vector2Int>();
            var frameCells = new List<Vector2Int>(64);
            BoardFrameMap.CollectFrameCellsExceptDoorOpenings(levelData.gridDimensions, levelData.GetDoorOpenings(), frameCells);

            var occupancy = new BoardOccupancyMap();
            occupancy.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);
            occupancy.MarkBlockedCells(blockedCells);
            occupancy.MarkBlockedCells(frameCells);

            if (levelData.blocks == null)
            {
                return;
            }

            for (int i = 0; i < levelData.blocks.Count; i++)
            {
                LevelJsonBlockData block = levelData.blocks[i];
                string resolvedShapeKey = block.ResolveShapeKey();

                if (string.IsNullOrWhiteSpace(resolvedShapeKey))
                {
                    AddIssue(issues, context, $"Block #{i} shapeKey bos.");
                }
                else
                {
                    if (!availableShapeKeys.Contains(resolvedShapeKey))
                    {
                        AddIssue(issues, context,
                            $"Block #{i} shape '{resolvedShapeKey}' availableShapeKeys listesinde yok.");
                    }

                    if (shapeRegistry == null || !shapeRegistry.TryResolveShape(resolvedShapeKey, out _))
                    {
                        AddIssue(issues, context,
                            $"Block #{i} shape '{resolvedShapeKey}' registry'de cozulemedi.");
                    }
                }

                if (!availableColors.Contains(block.colorType))
                {
                    AddIssue(issues, context,
                        $"Block #{i} colorType '{block.colorType}' availableColors listesinde yok.");
                }

                Vector2Int[] localCells = block.GetLocalCells(shapeRegistry);
                if (!occupancy.CanPlace(i, block.position, localCells))
                {
                    AddIssue(issues, context,
                        $"Block #{i} yerlestirilemedi. Pos={block.position}, Shape={resolvedShapeKey}");
                    continue;
                }

                occupancy.FillBlock(i, block.position, localCells);
            }
        }

        public static void ValidateCollection(
            IReadOnlyList<LevelJsonData> levels,
            IList<string> issues,
            IReadOnlyList<string> sourceNames = null)
        {
            if (issues == null || levels == null || levels.Count == 0)
            {
                return;
            }

            var sourceByLevelNumber = new Dictionary<int, string>();
            var sourceByLevelKey = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < levels.Count; i++)
            {
                LevelJsonData levelData = levels[i];
                string source = ResolveSourceName(levelData, sourceNames, i);

                if (levelData == null)
                {
                    AddIssue(issues, source, "Collection icindeki level null.");
                    continue;
                }

                int levelNumber = Mathf.Max(1, levelData.levelNumber);
                if (sourceByLevelNumber.TryGetValue(levelNumber, out string existingLevelNumberSource))
                {
                    AddIssue(issues, source,
                        $"levelNumber tekrar ediyor ({levelNumber}). Diger kaynak: {existingLevelNumberSource}");
                }
                else
                {
                    sourceByLevelNumber.Add(levelNumber, source);
                }

                if (!string.IsNullOrWhiteSpace(levelData.levelKey))
                {
                    string levelKey = levelData.levelKey.Trim();
                    if (sourceByLevelKey.TryGetValue(levelKey, out string existingLevelKeySource))
                    {
                        AddIssue(issues, source,
                            $"levelKey tekrar ediyor ('{levelKey}'). Diger kaynak: {existingLevelKeySource}");
                    }
                    else
                    {
                        sourceByLevelKey.Add(levelKey, source);
                    }
                }
            }
        }

        private static string ResolveSourceName(LevelJsonData levelData, IReadOnlyList<string> sourceNames, int index)
        {
            if (sourceNames != null && index >= 0 && index < sourceNames.Count && !string.IsNullOrWhiteSpace(sourceNames[index]))
            {
                return sourceNames[index].Trim();
            }

            if (levelData != null && !string.IsNullOrWhiteSpace(levelData.levelKey))
            {
                return levelData.levelKey.Trim();
            }

            return $"Level[{index}]";
        }

        private static void AddIssue(ICollection<string> issues, string context, string message)
        {
            string safeContext = string.IsNullOrWhiteSpace(context) ? "Level" : context.Trim();
            issues.Add($"[{safeContext}] {message}");
        }
    }
}
