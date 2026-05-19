using System;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    public struct BlockVisualBuildRequest
    {
        public LevelDefinition LevelData;
        public BoardController BoardController;
        public BlockViewRuntimePool BlockViewPool;
        public LayoutMetrics Layout;
        public float BlockCellVisualScale;
        public Vector3 BlockRootScale;
        public float IndicatorHeightOffsetInCells;
        public float IndicatorLocalZOffset;
        public Func<BlockColor, Material> ResolveMaterial;
        public Action<BlockRootView, int> EnsureBlockCells;
        public Action<GameObject, bool> SetActiveIfChanged;
        public Action<Transform, Vector3, Vector3> ApplyWorldTransform;
        public Action<BlockRootView, bool> SetDragHighlightActive;
        public Action<BlockRootView, Vector2Int[]> CacheBlockOutlineGridLoop;
        public Action<BlockRootView> RefreshDragHighlightBounds;
    }
}
