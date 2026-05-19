using System.Collections;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Animations
{
    public sealed class BlockExitFxController
    {
        public IEnumerator PlayClearAndExitSequence(BlockExitSequenceRequest request)
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
