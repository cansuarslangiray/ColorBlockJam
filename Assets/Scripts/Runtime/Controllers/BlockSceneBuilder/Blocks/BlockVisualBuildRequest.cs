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
        public Func<BlockColor, Material> ResolveMaterial;
        public Func<int, bool> IsBlockLocked;
        public Action<GameObject, bool> SetActiveIfChanged;
        public Action<Transform, Vector3> ApplyWorldPosition;
        public Action<BlockRootView, bool> SetDragHighlightActive;
    }
}
