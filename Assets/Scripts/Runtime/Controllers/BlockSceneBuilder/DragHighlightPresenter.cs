using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class DragHighlightPresenter
    {
        private const string DragOutlineObjectName = "BlockDragOutline";
        public delegate bool TryResolveBlockColorDelegate(BlockRootView blockView, out Color blockColor);

        public readonly struct DragHighlightSettings
        {
            public DragHighlightSettings(float cellSize, float baseOffsetInCells, float gapInCells,
                float verticalOffsetInCells, float thicknessInCells, Color defaultOutlineColor, Material sourceMaterial)
            {
                CellSize = cellSize;
                BaseOffsetInCells = baseOffsetInCells;
                GapInCells = gapInCells;
                VerticalOffsetInCells = verticalOffsetInCells;
                ThicknessInCells = thicknessInCells;
                DefaultOutlineColor = defaultOutlineColor;
                SourceMaterial = sourceMaterial;
            }

            public float CellSize { get; }
            public float BaseOffsetInCells { get; }
            public float GapInCells { get; }
            public float VerticalOffsetInCells { get; }
            public float ThicknessInCells { get; }
            public Color DefaultOutlineColor { get; }
            public Material SourceMaterial { get; }
        }

        private readonly struct DirectedGridEdge
        {
            public DirectedGridEdge(Vector2Int start, Vector2Int end)
            {
                Start = start;
                End = end;
            }

            public Vector2Int Start { get; }
            public Vector2Int End { get; }
        }

        private readonly struct UndirectedGridEdgeKey : IEquatable<UndirectedGridEdgeKey>
        {
            public UndirectedGridEdgeKey(Vector2Int a, Vector2Int b)
            {
                if (IsLexicographicallyBefore(a, b))
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public Vector2Int A { get; }
            public Vector2Int B { get; }

            public bool Equals(UndirectedGridEdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is UndirectedGridEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A.GetHashCode() * 397) ^ B.GetHashCode();
                }
            }
        }

        private const int DragOutlineCornerVertices = 4;
        private const int DragOutlineCapVertices = 2;

        private readonly HashSet<Vector2Int> _outlineOccupiedCellsBuffer = new();
        private readonly Dictionary<UndirectedGridEdgeKey, DirectedGridEdge> _outlineBoundaryEdgesBuffer = new();
        private readonly Dictionary<Vector2Int, Vector2Int> _outlineOutgoingEdgesBuffer = new();
        private readonly List<Vector2> _outlineVerticesBuffer = new();

        public void ResetRuntimeResources()
        {
            _outlineOccupiedCellsBuffer.Clear();
            _outlineBoundaryEdgesBuffer.Clear();
            _outlineOutgoingEdgesBuffer.Clear();
            _outlineVerticesBuffer.Clear();
        }

        public void SetDragHighlightActive(BlockRootView blockView, bool isActive, in DragHighlightSettings settings,
            Action<GameObject, bool> setActiveIfChanged, TryResolveBlockColorDelegate tryResolveBlockColor)
        {
            if (blockView == null || blockView.RootTransform == null)
            {
                return;
            }

            if (isActive)
            {
                EnsureDragOutlineFrame(blockView, settings);
            }

            var outlineRenderer = blockView.DragOutlineRenderer;
            var outlineObject = outlineRenderer ? outlineRenderer.gameObject : null;

            if (!isActive)
            {
                setActiveIfChanged?.Invoke(outlineObject, false);
                return;
            }

            var outlineColor = ResolveDragOutlineColor(blockView, settings, tryResolveBlockColor);
            var outlineMaterial =
                ResolveDragOutlineMaterial(settings, outlineRenderer ? outlineRenderer.sharedMaterial : null);
            if (outlineRenderer != null && outlineMaterial && outlineRenderer.sharedMaterial != outlineMaterial)
            {
                outlineRenderer.sharedMaterial = outlineMaterial;
            }

            RefreshDragHighlightBounds(blockView, settings, tryResolveBlockColor);
            if (outlineRenderer != null)
            {
                setActiveIfChanged?.Invoke(outlineObject, true);
            }
        }

        public void RefreshDragHighlightBounds(BlockRootView blockView, in DragHighlightSettings settings,
            TryResolveBlockColorDelegate tryResolveBlockColor)
        {
            if (blockView == null || !TryResolveActiveCellBoundsLocal(blockView, out var minLocal, out var maxLocal))
            {
                return;
            }

            var outlineHalfThickness = ResolveDragOutlineThickness(settings) * 0.5f;
            var insetGap = Mathf.Max(0f, settings.CellSize * settings.GapInCells);
            var totalInsetX = Mathf.Max(0f, Mathf.Min(insetGap, (maxLocal.x - minLocal.x) * 0.5f - Mathf.Epsilon));
            var totalInsetY = Mathf.Max(0f, Mathf.Min(insetGap, (maxLocal.y - minLocal.y) * 0.5f - Mathf.Epsilon));
            if (totalInsetX > 0f)
            {
                minLocal.x += totalInsetX;
                maxLocal.x -= totalInsetX;
            }

            if (totalInsetY > 0f)
            {
                minLocal.y += totalInsetY;
                maxLocal.y -= totalInsetY;
            }

            var outlineExpansion = Mathf.Max(0f, outlineHalfThickness - Mathf.Min(totalInsetX, totalInsetY));
            var baseInset = Mathf.Max(0f, settings.CellSize * settings.BaseOffsetInCells);
            var forwardBias = Mathf.Max(baseInset, settings.CellSize * 0.02f);
            var outlineBaseZ = minLocal.z - forwardBias;
            var outlineColor = ResolveDragOutlineColor(blockView, settings, tryResolveBlockColor);

            if (TryResolveActiveCellOutlineVerticesLocal(blockView, settings.CellSize, outlineExpansion,
                    out var outlineVertices))
            {
                RefreshDragOutlineFrame(blockView, outlineVertices, settings, outlineBaseZ, outlineColor);
                return;
            }

            RefreshDragOutlineFrameRectangle(blockView, minLocal, maxLocal, settings, outlineBaseZ, outlineColor);
        }

        public void CacheBlockOutlineGridLoop(BlockRootView blockView, Vector2Int[] localCells)
        {
            if (blockView == null)
            {
                return;
            }

            var cachedLoop = blockView.CachedOutlineGridLoop;
            cachedLoop.Clear();

            if (!TryCacheOutlineOccupiedCells(localCells))
            {
                return;
            }

            CacheOutlineBoundaryEdges();
            if (_outlineBoundaryEdgesBuffer.Count < 4)
            {
                return;
            }

            if (!TryCacheOutlineOutgoingEdges())
            {
                cachedLoop.Clear();
                return;
            }

            if (!TryResolveOutlineLoopStartVertex(out var startVertex))
            {
                cachedLoop.Clear();
                return;
            }

            if (!TryBuildOutlineLoop(cachedLoop, startVertex))
            {
                cachedLoop.Clear();
                return;
            }

            if (cachedLoop.Count < 4 || cachedLoop.Count != _outlineOutgoingEdgesBuffer.Count)
            {
                cachedLoop.Clear();
            }
        }

        private void EnsureDragOutlineFrame(BlockRootView blockView, in DragHighlightSettings settings)
        {
            if (blockView.DragOutlineRenderer != null)
            {
                return;
            }

            if (!TryResolveDragOutlineRendererFromPool(blockView, out var outlineRenderer))
            {
                if (!blockView.HasLoggedMissingDragOutline)
                {
                    Debug.LogWarning(
                        $"Block '{blockView.RootObject.name}' is missing a pooled '{DragOutlineObjectName}' LineRenderer. " +
                        "Runtime drag-outline creation is disabled.",
                        blockView.RootObject);
                    blockView.HasLoggedMissingDragOutline = true;
                }

                return;
            }

            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.alignment = LineAlignment.View;
            outlineRenderer.numCornerVertices = DragOutlineCornerVertices;
            outlineRenderer.numCapVertices = DragOutlineCapVertices;
            outlineRenderer.textureMode = LineTextureMode.Stretch;
            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness(settings);
            outlineRenderer.startColor = settings.DefaultOutlineColor;
            outlineRenderer.endColor = settings.DefaultOutlineColor;
            outlineRenderer.sortingOrder = 12000;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.sharedMaterial = ResolveDragOutlineMaterial(settings, outlineRenderer.sharedMaterial);

            blockView.DragOutlineRenderer = outlineRenderer;
            blockView.HasLoggedMissingDragOutline = false;
            if (outlineRenderer.gameObject)
            {
                outlineRenderer.gameObject.SetActive(false);
            }
        }

        private Material ResolveDragOutlineMaterial(in DragHighlightSettings settings, Material fallbackSourceMaterial)
        {
            var sourceMaterial = settings.SourceMaterial ? settings.SourceMaterial : fallbackSourceMaterial;
            return sourceMaterial ? sourceMaterial : null;
        }

        private static bool TryResolveDragOutlineRendererFromPool(BlockRootView blockView, out LineRenderer lineRenderer)
        {
            lineRenderer = null;
            if (blockView?.RootTransform == null)
            {
                return false;
            }

            var renderers = blockView.RootTransform.GetComponentsInChildren<LineRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (!candidate || !candidate.gameObject)
                {
                    continue;
                }

                if (!string.Equals(candidate.gameObject.name, DragOutlineObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                lineRenderer = candidate;
                return true;
            }

            return false;
        }

        private void RefreshDragOutlineFrame(BlockRootView blockView, IReadOnlyList<Vector2> outlineVertices,
            in DragHighlightSettings settings, float topZ, Color outlineColor)
        {
            var outlineRenderer = blockView.DragOutlineRenderer;
            if (outlineRenderer == null)
            {
                return;
            }

            if (outlineVertices == null || outlineVertices.Count < 3)
            {
                outlineRenderer.positionCount = 0;
                return;
            }

            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness(settings);
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.positionCount = outlineVertices.Count;
            var verticalOffset = settings.CellSize * settings.VerticalOffsetInCells;

            for (var i = 0; i < outlineVertices.Count; i++)
            {
                var point = outlineVertices[i];
                outlineRenderer.SetPosition(i, new Vector3(point.x, point.y + verticalOffset, topZ));
            }
        }

        private void RefreshDragOutlineFrameRectangle(BlockRootView blockView, Vector3 minLocal, Vector3 maxLocal,
            in DragHighlightSettings settings, float topZ, Color outlineColor)
        {
            var outlineRenderer = blockView.DragOutlineRenderer;
            if (outlineRenderer == null)
            {
                return;
            }

            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness(settings);
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.positionCount = 4;
            var verticalOffset = settings.CellSize * settings.VerticalOffsetInCells;
            outlineRenderer.SetPosition(0, new Vector3(minLocal.x, minLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(1, new Vector3(maxLocal.x, minLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(2, new Vector3(maxLocal.x, maxLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(3, new Vector3(minLocal.x, maxLocal.y + verticalOffset, topZ));
        }

        private static bool TryResolveActiveCellBoundsLocal(BlockRootView blockView, out Vector3 minLocal,
            out Vector3 maxLocal)
        {
            minLocal = default;
            maxLocal = default;
            if (blockView is not { HasCachedLocalBounds: true })
            {
                return false;
            }

            minLocal = blockView.CachedLocalBoundsMin;
            maxLocal = blockView.CachedLocalBoundsMax;
            return true;
        }

        private static float ResolveDragOutlineThickness(in DragHighlightSettings settings)
        {
            var configuredThickness = Mathf.Max(0.005f, settings.CellSize * settings.ThicknessInCells);
            var visualMinimumThickness = Mathf.Max(0.0045f, settings.CellSize * 0.058f);
            return Mathf.Max(configuredThickness, visualMinimumThickness);
        }

        private static Color ResolveDragOutlineColor(BlockRootView blockView, in DragHighlightSettings settings,
            TryResolveBlockColorDelegate tryResolveBlockColor)
        {
            if (tryResolveBlockColor == null || !tryResolveBlockColor(blockView, out var blockColor))
            {
                return settings.DefaultOutlineColor;
            }

            var liftedColor = Color.Lerp(blockColor, Color.white, 0.18f);
            liftedColor.a = settings.DefaultOutlineColor.a;
            return liftedColor;
        }

        private bool TryResolveActiveCellOutlineVerticesLocal(BlockRootView blockView, float cellSize,
            float outlineOffset, out List<Vector2> outlineVertices)
        {
            outlineVertices = null;
            if (blockView == null)
            {
                return false;
            }

            var gridLoop = blockView.CachedOutlineGridLoop;
            if (gridLoop == null || gridLoop.Count < 4)
            {
                return false;
            }

            var isClockwise = IsClockwiseLoop(gridLoop);
            _outlineVerticesBuffer.Clear();
            for (var i = 0; i < gridLoop.Count; i++)
            {
                var gridVertex = gridLoop[i];
                var worldVertex = new Vector2(gridVertex.x * cellSize, gridVertex.y * cellSize);
                var previousVertex = gridLoop[(i - 1 + gridLoop.Count) % gridLoop.Count];
                var nextVertex = gridLoop[(i + 1) % gridLoop.Count];
                var expandedVertex = ExpandOrthogonalCorner(gridVertex, previousVertex, nextVertex, outlineOffset,
                    worldVertex, cellSize, isClockwise);
                _outlineVerticesBuffer.Add(expandedVertex);
            }

            outlineVertices = _outlineVerticesBuffer;
            return outlineVertices.Count >= 4;
        }

        private bool TryCacheOutlineOccupiedCells(Vector2Int[] localCells)
        {
            _outlineOccupiedCellsBuffer.Clear();
            if (localCells == null || localCells.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < localCells.Length; i++)
            {
                _outlineOccupiedCellsBuffer.Add(localCells[i]);
            }

            return _outlineOccupiedCellsBuffer.Count > 0;
        }

        private void CacheOutlineBoundaryEdges()
        {
            _outlineBoundaryEdgesBuffer.Clear();
            foreach (var cell in _outlineOccupiedCellsBuffer)
            {
                var bottomLeft = new Vector2Int(cell.x, cell.y);
                var bottomRight = new Vector2Int(cell.x + 1, cell.y);
                var topRight = new Vector2Int(cell.x + 1, cell.y + 1);
                var topLeft = new Vector2Int(cell.x, cell.y + 1);

                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, bottomLeft, bottomRight);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, bottomRight, topRight);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, topRight, topLeft);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, topLeft, bottomLeft);
            }
        }

        private bool TryCacheOutlineOutgoingEdges()
        {
            _outlineOutgoingEdgesBuffer.Clear();
            foreach (var edge in _outlineBoundaryEdgesBuffer.Values)
            {
                if (_outlineOutgoingEdgesBuffer.ContainsKey(edge.Start))
                {
                    return false;
                }

                _outlineOutgoingEdgesBuffer.Add(edge.Start, edge.End);
            }

            return _outlineOutgoingEdgesBuffer.Count >= 4;
        }

        private bool TryResolveOutlineLoopStartVertex(out Vector2Int startVertex)
        {
            startVertex = Vector2Int.zero;
            var hasStart = false;
            foreach (var vertex in _outlineOutgoingEdgesBuffer.Keys)
            {
                if (!hasStart || IsLexicographicallyBefore(vertex, startVertex))
                {
                    startVertex = vertex;
                    hasStart = true;
                }
            }

            return hasStart;
        }

        private bool TryBuildOutlineLoop(List<Vector2Int> cachedLoop, Vector2Int startVertex)
        {
            var currentVertex = startVertex;
            for (var guard = 0; guard <= _outlineOutgoingEdgesBuffer.Count; guard++)
            {
                cachedLoop.Add(currentVertex);
                if (!_outlineOutgoingEdgesBuffer.TryGetValue(currentVertex, out var nextVertex))
                {
                    return false;
                }

                if (nextVertex == startVertex)
                {
                    return true;
                }

                currentVertex = nextVertex;
            }

            return false;
        }

        private static void ToggleBoundaryEdge(IDictionary<UndirectedGridEdgeKey, DirectedGridEdge> boundaryEdgesByKey,
            Vector2Int start, Vector2Int end)
        {
            var edgeKey = new UndirectedGridEdgeKey(start, end);
            if (boundaryEdgesByKey.Remove(edgeKey))
            {
                return;
            }

            boundaryEdgesByKey.Add(edgeKey, new DirectedGridEdge(start, end));
        }

        private static Vector2 ExpandOrthogonalCorner(Vector2Int currentVertex, Vector2Int previousVertex,
            Vector2Int nextVertex, float outlineOffset, Vector2 fallbackVertex, float cellSize, bool isClockwise)
        {
            if (Mathf.Approximately(outlineOffset, 0f))
            {
                return fallbackVertex;
            }

            var incoming = currentVertex - previousVertex;
            var outgoing = nextVertex - currentVertex;
            if (incoming == Vector2Int.zero || outgoing == Vector2Int.zero)
            {
                return fallbackVertex;
            }

            var incomingDirection = new Vector2Int(ClampToSign(incoming.x), ClampToSign(incoming.y));
            var outgoingDirection = new Vector2Int(ClampToSign(outgoing.x), ClampToSign(outgoing.y));
            if (incomingDirection == -outgoingDirection)
            {
                return fallbackVertex;
            }

            var incomingOutwardNormal = isClockwise
                ? new Vector2(-incomingDirection.y, incomingDirection.x)
                : new Vector2(incomingDirection.y, -incomingDirection.x);
            var outgoingOutwardNormal = isClockwise
                ? new Vector2(-outgoingDirection.y, outgoingDirection.x)
                : new Vector2(outgoingDirection.y, -outgoingDirection.x);
            var offsetDirection = incomingOutwardNormal + outgoingOutwardNormal;
            if (offsetDirection.sqrMagnitude < 0.000001f)
            {
                return fallbackVertex;
            }

            offsetDirection.Normalize();
            var maxOffset = Mathf.Max(0.0001f, cellSize * 0.49f);
            var clampedOffset = Mathf.Min(Mathf.Abs(outlineOffset), maxOffset);
            var signedOffset = outlineOffset >= 0f ? clampedOffset : -clampedOffset;
            return fallbackVertex + (offsetDirection * signedOffset);
        }

        private static bool IsClockwiseLoop(IReadOnlyList<Vector2Int> loopVertices)
        {
            if (loopVertices == null || loopVertices.Count < 3)
            {
                return true;
            }

            long doubledArea = 0;
            for (var i = 0; i < loopVertices.Count; i++)
            {
                var current = loopVertices[i];
                var next = loopVertices[(i + 1) % loopVertices.Count];
                doubledArea += ((long)current.x * next.y) - ((long)next.x * current.y);
            }

            return doubledArea < 0;
        }

        private static int ClampToSign(int value)
        {
            return value switch
            {
                > 0 => 1,
                < 0 => -1,
                _ => 0
            };
        }

        private static bool IsLexicographicallyBefore(Vector2Int a, Vector2Int b)
        {
            if (a.y != b.y)
            {
                return a.y < b.y;
            }

            return a.x < b.x;
        }
    }
}
