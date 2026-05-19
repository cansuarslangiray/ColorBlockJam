using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    internal readonly struct DirectedGridEdge
    {
        public DirectedGridEdge(Vector2Int start, Vector2Int end)
        {
            Start = start;
            End = end;
        }

        public Vector2Int Start { get; }
        public Vector2Int End { get; }
    }
}
