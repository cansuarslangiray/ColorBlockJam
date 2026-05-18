using System;
using System.Collections;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockExitFxController
    {
        public struct ExitSequenceRequest
        {
            public BlockRootView BlockView;
            public DoorOpeningData MatchedDoor;
            public Vector2Int ResolvedExitDirection;
            public Action PlayBlockMatchSuccessSfx;
            public Action<DoorOpeningData> PlayDoorMatchFx;
            public Func<BlockRootView, DoorOpeningData, Vector2Int, IEnumerator> AnimateBlockDoorExitSequence;
            public Action<BlockRootView, Vector2Int> PlayBlockExitDisintegrateFx;
        }

        public IEnumerator PlayClearAndExitSequence(ExitSequenceRequest request)
        {
            request.PlayBlockMatchSuccessSfx?.Invoke();
            request.PlayDoorMatchFx?.Invoke(request.MatchedDoor);

            if (request.ResolvedExitDirection == Vector2Int.zero)
            {
                request.PlayBlockExitDisintegrateFx?.Invoke(request.BlockView, Vector2Int.zero);
                yield break;
            }

            if (request.AnimateBlockDoorExitSequence != null)
            {
                yield return request.AnimateBlockDoorExitSequence(request.BlockView, request.MatchedDoor,
                    request.ResolvedExitDirection);
            }
        }
    }
}
