using System;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    internal readonly struct UndirectedGridEdgeKey : IEquatable<UndirectedGridEdgeKey>
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
