using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public readonly struct RuntimeBlockLayerState
    {
        public readonly string LayerId;
        public readonly ShapeLayerRole LayerRole;
        public readonly Vector2Int[] LocalCells;
        public readonly BlockColor ColorType;
        public readonly int ExitOrder;

        public RuntimeBlockLayerState(string layerId, ShapeLayerRole layerRole, Vector2Int[] localCells,
            BlockColor colorType, int exitOrder)
        {
            LayerId = string.IsNullOrWhiteSpace(layerId) ? string.Empty : layerId.Trim();
            LayerRole = layerRole;
            LocalCells = localCells ?? System.Array.Empty<Vector2Int>();
            ColorType = colorType;
            ExitOrder = Mathf.Max(0, exitOrder);
        }

        public bool ContainsLocalCell(Vector2Int localCell)
        {
            if (LocalCells == null || LocalCells.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < LocalCells.Length; i++)
            {
                if (LocalCells[i] == localCell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
