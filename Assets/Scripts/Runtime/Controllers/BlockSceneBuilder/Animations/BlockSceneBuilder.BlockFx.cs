using System.Collections;
using Runtime.Controllers.BlockSceneBuilder.Animations;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private const float DoorExitBurstCleanupDelay = 0.9f;
        private const float DoorPassThroughBurstAtProgress = 0f;
        private const float DoorPassThroughHideCellsAtProgress = 0.86f;

        private void StopDoorExitBurstParticle(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            var burstParticle = blockView.PooledDoorExitBurstParticle;
            if (burstParticle == null)
            {
                return;
            }

            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetDoorExitBurstRenderersEnabled(blockView, false);

            var burstObject = burstParticle.gameObject;
            if (burstObject)
            {
                SetActiveIfChanged(burstObject, false);
            }
        }

        private void HandleBlockDragHighlightChanged(int blockId, bool isActive)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            SetDragHighlightActive(blockView, isActive);
        }

        private void SetDragHighlightActive(BlockRootView blockView, bool isActive)
        {
            _dragHighlightPresenter.SetDragHighlightActive(blockView, isActive, SetActiveIfChanged);
        }

        private void SetBlockCellsActive(BlockRootView blockView, bool isActive)
        {
            if (blockView == null)
            {
                return;
            }

            var cells = blockView.Cells;
            for (var i = 0; i < cells.Count; i++)
            {
                var cellObject = cells[i];
                if (cellObject)
                {
                    SetActiveIfChanged(cellObject, isActive);
                }
            }
        }

        private void PlayBlockExitDisintegrateFx(BlockRootView blockView, Vector2Int _ = default)
        {
            if (blockView == null)
            {
                return;
            }

            PlayDoorExitBurstParticleFx(blockView);
        }

        private void PlayDoorExitBurstParticleFx(BlockRootView blockView)
        {
            var burstParticle = blockView?.PooledDoorExitBurstParticle;
            if (burstParticle == null)
            {
                return;
            }

            var burstObject = burstParticle.gameObject;
            if (!burstObject)
            {
                return;
            }

            SetActiveIfChanged(burstObject, true);
            var burstColor = ResolveBlockBurstColor(blockView);
            ApplyDoorExitBurstRenderersTint(blockView, burstColor);
            SetDoorExitBurstRenderersEnabled(blockView, true);

            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burstParticle.Play(true);
        }

        private void ApplyDoorExitBurstRenderersTint(BlockRootView blockView, Color burstColor)
        {
            var burstRenderers = ResolveDoorExitBurstRenderers(blockView);
            if (burstRenderers == null || burstRenderers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < burstRenderers.Length; i++)
            {
                ApplyDoorExitBurstRendererTint(burstRenderers[i], burstColor);
            }
        }

        private static void SetDoorExitBurstRenderersEnabled(BlockRootView blockView, bool isEnabled)
        {
            var burstRenderers = ResolveDoorExitBurstRenderers(blockView);
            if (burstRenderers == null || burstRenderers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < burstRenderers.Length; i++)
            {
                var renderer = burstRenderers[i];
                if (renderer)
                {
                    renderer.enabled = isEnabled;
                }
            }
        }

        private static ParticleSystemRenderer[] ResolveDoorExitBurstRenderers(BlockRootView blockView)
        {
            var burstParticle = blockView?.PooledDoorExitBurstParticle;
            if (burstParticle)
            {
                return burstParticle.GetComponentsInChildren<ParticleSystemRenderer>(true);
            }

            var fallbackRenderer = blockView?.PooledDoorExitBurstRenderer;
            if (fallbackRenderer)
            {
                return new[] { fallbackRenderer };
            }

            return null;
        }

        private void ApplyDoorExitBurstRendererTint(ParticleSystemRenderer particleRenderer, Color burstColor)
        {
            if (!particleRenderer)
            {
                return;
            }

            _fxRendererPropertyBlock ??= new MaterialPropertyBlock();
            _fxRendererPropertyBlock.Clear();

            var sharedMaterial = particleRenderer.sharedMaterial;
            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty(BaseColorPropertyId))
                {
                    _fxRendererPropertyBlock.SetColor(BaseColorPropertyId, burstColor);
                }

                if (sharedMaterial.HasProperty(ColorPropertyId))
                {
                    _fxRendererPropertyBlock.SetColor(ColorPropertyId, burstColor);
                }
            }

            particleRenderer.SetPropertyBlock(_fxRendererPropertyBlock);
        }

        private static Color ResolveBlockBurstColor(BlockRootView blockView)
        {
            if (TryResolveBlockCellMaterialColor(blockView, out var color))
            {
                color.a = 1f;
                return color;
            }

            return Color.white;
        }

        private static bool TryResolveBlockCellMaterialColor(BlockRootView blockView, out Color color)
        {
            color = Color.white;
            if (blockView == null || !blockView.HasCachedBlockColor)
            {
                return false;
            }

            color = blockView.CachedBlockColor;
            return true;
        }

        private IEnumerator AnimateBlockDoorExitSequence(BlockRootView blockView, DoorOpeningData matchedDoor,
            Vector2Int resolvedExitDirection)
        {
            yield return AnimateBlockDoorPassThrough(blockView, matchedDoor, resolvedExitDirection);
        }

        private IEnumerator AnimateBlockDoorPassThrough(BlockRootView blockView, DoorOpeningData matchedDoor,
            Vector2Int resolvedExitDirection)
        {
            if (blockView == null)
            {
                yield break;
            }

            if (!TryResolveDoorPassThroughMotion(blockView, matchedDoor, resolvedExitDirection, out var motion))
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
                SetBlockCellsActive(blockView, false);
                yield break;
            }

            var elapsed = 0f;
            var burstTriggered = false;
            var cellsHidden = false;
            var hideCellsAt = motion.TravelDuration * DoorPassThroughHideCellsAtProgress;

            while (elapsed < motion.TravelDuration)
            {
                if (!motion.PlacementTransform)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / motion.TravelDuration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                motion.PlacementTransform.position =
                    Vector3.Lerp(motion.StartPosition, motion.EndPosition, easedProgress);

                if (!burstTriggered && elapsed >= motion.BurstAt)
                {
                    PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
                    burstTriggered = true;
                }

                if (!cellsHidden && elapsed >= hideCellsAt)
                {
                    SetBlockCellsActive(blockView, false);
                    cellsHidden = true;
                }

                yield return null;
            }

            if (motion.PlacementTransform)
            {
                motion.PlacementTransform.position = motion.EndPosition;
            }

            if (!burstTriggered)
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
            }

            if (!cellsHidden)
            {
                SetBlockCellsActive(blockView, false);
            }
        }

        private bool TryResolveDoorPassThroughMotion(
            BlockRootView blockView,
            DoorOpeningData matchedDoor,
            Vector2Int resolvedExitDirection,
            out DoorPassThroughMotion motion)
        {
            motion = default;

            var placementTransform =
                blockView.PlacementTransform ? blockView.PlacementTransform : blockView.RootTransform;
            if (!placementTransform)
            {
                return false;
            }

            var exitVector = new Vector3(resolvedExitDirection.x, resolvedExitDirection.y, 0f);
            if (exitVector.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            exitVector.Normalize();

            var layout = GetCurrentLayout();
            var startPosition = placementTransform.position;
            var doorCenter = ResolveDoorWorldCenter(matchedDoor, boardController.GridDimensions, layout);
            var blockCenterOffset = new Vector3(blockView.LocalCenter.x, blockView.LocalCenter.y, 0f);
            var currentCenter = startPosition + blockCenterOffset;
            var centerAlignDelta = new Vector3(doorCenter.x - currentCenter.x, doorCenter.y - currentCenter.y, 0f);

            var entryAdvance = Mathf.Max(0f, CellSize * doorEntryAdvanceInCells);
            var exitAdvance = Mathf.Max(CellSize * 0.25f,
                (CellSize * doorExitForwardTravelInCells) + (layout.FrameThickness * 0.5f));

            var entryPosition = startPosition + centerAlignDelta + (exitVector * entryAdvance);
            var traverseEndPosition = entryPosition + (exitVector * exitAdvance);
            var travelDuration = Mathf.Max(0.05f, doorPassThroughDuration);
            var burstAt = travelDuration * DoorPassThroughBurstAtProgress;

            motion = new DoorPassThroughMotion(
                placementTransform,
                startPosition,
                traverseEndPosition,
                travelDuration,
                burstAt);

            return true;
        }

        private IEnumerator CleanupDoorExitBurstAfterDelay(BlockRootView blockView, float delay)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, delay));

            if (blockView == null)
            {
                yield break;
            }

            StopDoorExitBurstParticle(blockView);
        }

    }
}
