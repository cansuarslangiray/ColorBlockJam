using System;
using System.Collections;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Animations
{
    public struct BlockExitSequenceRequest
    {
        public BlockRootView BlockView;
        public DoorOpeningData MatchedDoor;
        public Vector2Int ResolvedExitDirection;
        public Action PlayBlockMatchSuccessSfx;
        public Action<DoorOpeningData> PlayDoorMatchFx;
        public Func<BlockRootView, DoorOpeningData, Vector2Int, IEnumerator> AnimateBlockDoorExitSequence;
        public Action<BlockRootView, Vector2Int> PlayBlockExitDisintegrateFx;
    }
}
