using System;
using System.Collections.Generic;
using System.IO;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEditor;
using UnityEngine;

namespace Editor.DataPipeline
{
    internal static class BlockOutlinePrefabBakeTool
    {
        private const string BlocksRootPath = "Assets/Art/GeneratedBlocks/Prefabs/Blocks";
        private const string OutlineMaterialPath = "Assets/Art/GeneratedBlocks/Materials/MAT_BlockOutlineActive.mat";
        private const string OutlineObjectName = "BlockOutline";
        private static readonly Vector3 OutlineLocalPosition = new(0f, 0f, -0.305999994f);
        private const float OutlineZ = 0f;
        private const float OuterMinXInset = 0.036f;
        private const float OuterMaxXInset = 0.036f;
        private const float OuterMinYInset = 0.066f;
        private const float OuterMaxYInset = 0.006f;
        private const float InnerYOffset = 0.03f;

        [MenuItem("Tools/Color Block Jam/Pools/Bake Block Outlines")]
        private static void BakeOutlinesFromMenu()
        {
            BakeOutlines();
        }

        public static void BakeOutlines()
        {
            var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (!outlineMaterial)
            {
                Debug.LogError($"[BlockOutlinePrefabBakeTool] Missing outline material at path: {OutlineMaterialPath}");
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BlocksRootPath });
            var bakedCount = 0;
            var skippedCount = 0;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileName(prefabPath);
                if (!fileName.StartsWith("Block_Shape_", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    if (!prefabRoot.TryGetComponent<BlockPoolBindings>(out var bindings) || bindings == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!TryCollectShapeCells(bindings.ShapeLocalCells, out var cells, out var bounds))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!TryBuildOutlineLoop(cells, out var loop))
                    {
                        skippedCount++;
                        continue;
                    }

                    var outlineRenderer = ResolveOrCreateOutlineRenderer(prefabRoot.transform);
                    ConfigureOutlineRenderer(outlineRenderer, outlineMaterial);
                    ApplyOutlineLoop(outlineRenderer, loop, bounds);
                    bindings.EditorRebuildBindingsFromHierarchy();
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                    bakedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BlockOutlinePrefabBakeTool] Baked outlines for {bakedCount} prefabs. Skipped {skippedCount} prefabs.");
        }

        private static LineRenderer ResolveOrCreateOutlineRenderer(Transform root)
        {
            var existing = root.Find(OutlineObjectName);
            if (existing && existing.TryGetComponent<LineRenderer>(out var existingRenderer) && existingRenderer)
            {
                existing.gameObject.SetActive(true);
                existing.SetLocalPositionAndRotation(OutlineLocalPosition, Quaternion.identity);
                existing.localScale = Vector3.one;
                return existingRenderer;
            }

            var outlineObject = new GameObject(OutlineObjectName);
            outlineObject.transform.SetParent(root, false);
            outlineObject.transform.SetLocalPositionAndRotation(OutlineLocalPosition, Quaternion.identity);
            outlineObject.transform.localScale = Vector3.one;
            outlineObject.SetActive(true);
            return outlineObject.AddComponent<LineRenderer>();
        }

        private static void ConfigureOutlineRenderer(LineRenderer lineRenderer, Material outlineMaterial)
        {
            lineRenderer.enabled = true;
            lineRenderer.sharedMaterial = outlineMaterial;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.widthMultiplier = 0.072f;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.numCapVertices = 2;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.sortingOrder = 0;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.92156863f, 0f),
                    new GradientAlphaKey(0.92156863f, 1f)
                });
            lineRenderer.colorGradient = gradient;
        }

        private static bool TryCollectShapeCells(IReadOnlyList<Vector2Int> shapeLocalCells, out HashSet<Vector2Int> cells,
            out RectInt bounds)
        {
            cells = new HashSet<Vector2Int>();
            bounds = default;
            if (shapeLocalCells == null || shapeLocalCells.Count == 0)
            {
                return false;
            }

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            for (var i = 0; i < shapeLocalCells.Count; i++)
            {
                var cell = shapeLocalCells[i];
                if (!cells.Add(cell))
                {
                    continue;
                }

                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            if (cells.Count == 0)
            {
                return false;
            }

            bounds = new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
            return true;
        }

        private static bool TryBuildOutlineLoop(HashSet<Vector2Int> cells, out List<IntPoint> loop)
        {
            loop = new List<IntPoint>(32);
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            var boundaryEdgesByKey = new Dictionary<string, DirectedEdge>(64, StringComparer.Ordinal);
            foreach (var cell in cells)
            {
                var x = cell.x;
                var y = cell.y;
                TryToggleEdge(boundaryEdgesByKey, new DirectedEdge(new IntPoint(x, y), new IntPoint(x + 1, y)));
                TryToggleEdge(boundaryEdgesByKey, new DirectedEdge(new IntPoint(x + 1, y), new IntPoint(x + 1, y + 1)));
                TryToggleEdge(boundaryEdgesByKey, new DirectedEdge(new IntPoint(x + 1, y + 1), new IntPoint(x, y + 1)));
                TryToggleEdge(boundaryEdgesByKey, new DirectedEdge(new IntPoint(x, y + 1), new IntPoint(x, y)));
            }

            if (boundaryEdgesByKey.Count == 0)
            {
                return false;
            }

            var edgeByStart = new Dictionary<IntPoint, DirectedEdge>(boundaryEdgesByKey.Count);
            var firstEdge = default(DirectedEdge);
            var hasFirstEdge = false;
            foreach (var edge in boundaryEdgesByKey.Values)
            {
                edgeByStart[edge.Start] = edge;
                if (hasFirstEdge && ComparePoints(edge.Start, firstEdge.Start) >= 0)
                {
                    continue;
                }

                firstEdge = edge;
                hasFirstEdge = true;
            }

            if (!hasFirstEdge)
            {
                return false;
            }

            var visited = new HashSet<IntPoint>();
            var currentEdge = firstEdge;
            var startPoint = firstEdge.Start;
            loop.Add(startPoint);
            visited.Add(startPoint);

            while (true)
            {
                var nextPoint = currentEdge.End;
                if (nextPoint.Equals(startPoint))
                {
                    break;
                }

                if (!visited.Add(nextPoint))
                {
                    return false;
                }

                loop.Add(nextPoint);
                if (!edgeByStart.TryGetValue(nextPoint, out currentEdge))
                {
                    return false;
                }
            }

            return loop.Count >= 3;
        }

        private static void ApplyOutlineLoop(LineRenderer lineRenderer, IReadOnlyList<IntPoint> loop, RectInt bounds)
        {
            var maxX = bounds.xMax;
            var maxY = bounds.yMax;
            lineRenderer.positionCount = loop.Count;
            for (var i = 0; i < loop.Count; i++)
            {
                var point = loop[i];
                lineRenderer.SetPosition(i, new Vector3(
                    ResolveOutlineX(point.X, maxX),
                    ResolveOutlineY(point.Y, maxY),
                    OutlineZ));
            }
        }

        private static float ResolveOutlineX(int x, int maxX)
        {
            if (x <= 0)
            {
                return OuterMinXInset;
            }

            if (x >= maxX)
            {
                return maxX - OuterMaxXInset;
            }

            return x;
        }

        private static float ResolveOutlineY(int y, int maxY)
        {
            if (y <= 0)
            {
                return OuterMinYInset;
            }

            if (y >= maxY)
            {
                return maxY - OuterMaxYInset;
            }

            return y + InnerYOffset;
        }

        private static void TryToggleEdge(IDictionary<string, DirectedEdge> edgesByKey, DirectedEdge edge)
        {
            var normalizedStart = ComparePoints(edge.Start, edge.End) <= 0 ? edge.Start : edge.End;
            var normalizedEnd = ComparePoints(edge.Start, edge.End) <= 0 ? edge.End : edge.Start;
            var key = $"{normalizedStart.X}:{normalizedStart.Y}>{normalizedEnd.X}:{normalizedEnd.Y}";
            if (edgesByKey.ContainsKey(key))
            {
                edgesByKey.Remove(key);
                return;
            }

            edgesByKey[key] = edge;
        }

        private static int ComparePoints(IntPoint left, IntPoint right)
        {
            var yCompare = left.Y.CompareTo(right.Y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return left.X.CompareTo(right.X);
        }

        private readonly struct DirectedEdge
        {
            public DirectedEdge(IntPoint start, IntPoint end)
            {
                Start = start;
                End = end;
            }

            public IntPoint Start { get; }
            public IntPoint End { get; }
        }

        private readonly struct IntPoint : IEquatable<IntPoint>
        {
            public IntPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(IntPoint other)
            {
                return X == other.X && Y == other.Y;
            }

            public override bool Equals(object obj)
            {
                return obj is IntPoint other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Y;
                }
            }
        }
    }
}
