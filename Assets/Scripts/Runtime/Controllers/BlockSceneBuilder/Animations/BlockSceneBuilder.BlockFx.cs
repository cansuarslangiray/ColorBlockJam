using System.Collections;
using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Animations;
using Runtime.Controllers.BlockSceneBuilder.Blocks;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private const float DoorExitBurstCleanupDelay = 0.28f;
        private const float DoorPassThroughScatterDistanceInCells = 0.78f;
        private const float DoorPassThroughMinScale = 0.06f;
        private const float DoorPassThroughRotationRangeInDegrees = 280f;
        private const float DoorPassThroughBurstAtProgress = 0.66f;

        private void StopDoorExitBurstParticle(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            var burstParticle = blockView.PooledDoorExitBurstParticle;
            if (burstParticle != null)
            {
                burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var burstRenderer = blockView.PooledDoorExitBurstRenderer;
                if (burstRenderer)
                {
                    burstRenderer.enabled = false;
                }

                var burstObject = burstParticle.gameObject;
                if (burstObject != null)
                {
                    SetActiveIfChanged(burstObject, false);
                }
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
            var burstRenderer = blockView.PooledDoorExitBurstRenderer;
            var burstColor = ResolveBlockBurstColor(blockView);
            ApplyDoorExitBurstRendererTint(burstRenderer, burstColor);
            if (burstRenderer)
            {
                burstRenderer.enabled = true;
            }

            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burstParticle.Play(true);
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

            var cellTransforms = blockView.DoorPassThroughCellTransformsBuffer;
            var initialScales = blockView.DoorPassThroughInitialScalesBuffer;
            var initialPositions = blockView.DoorPassThroughInitialPositionsBuffer;
            var initialRotations = blockView.DoorPassThroughInitialRotationsBuffer;
            var scatterDirections = blockView.DoorPassThroughScatterDirectionBuffer;
            var scatterRotationTargets = blockView.DoorPassThroughScatterRotationBuffer;
            var scatterDelays = blockView.DoorPassThroughScatterDelayBuffer;
            var cellRenderers = blockView.DoorPassThroughCellRendererBuffer;
            CollectActiveDoorPassThroughCells(blockView, cellTransforms, initialScales, initialPositions,
                initialRotations, scatterRotationTargets, scatterDirections, scatterDelays, cellRenderers,
                resolvedExitDirection);
            if (cellTransforms.Count <= 0)
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
                ClearDoorPassThroughVisualOverrides(cellRenderers);
                yield break;
            }

            if (!TryResolveDoorPassThroughMotion(blockView, matchedDoor, resolvedExitDirection, out var motion))
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
                ClearDoorPassThroughVisualOverrides(cellRenderers);
                yield break;
            }

            var elapsed = 0f;
            var collapseDuration = Mathf.Max(0.01f, motion.TravelDuration - motion.CollapseStartAt);
            var collapseDurationReciprocal = 1f / collapseDuration;
            var fadeStartAt = Mathf.Clamp(motion.CollapseStartAt * 0.35f, 0f, motion.TravelDuration);
            var fadeDuration = Mathf.Max(0.01f, motion.TravelDuration - fadeStartAt);
            var fadeDurationReciprocal = 1f / fadeDuration;
            var burstTriggered = false;
            var scatterDistance = CellSize * DoorPassThroughScatterDistanceInCells;

            while (elapsed < motion.TravelDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / motion.TravelDuration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                motion.PlacementTransform.position =
                    Vector3.Lerp(motion.StartPosition, motion.EndPosition, easedProgress);

                var collapseProgress =
                    ResolveDoorPassThroughCollapseProgress(elapsed, motion.CollapseStartAt, collapseDurationReciprocal);
                var scaleFactor = Mathf.Lerp(1f, DoorPassThroughMinScale, collapseProgress);
                ApplyDoorPassThroughScale(cellTransforms, initialScales, scaleFactor);
                var scatterProgress =
                    ResolveDoorPassThroughScatterProgress(elapsed, motion.CollapseStartAt, collapseDurationReciprocal);
                ApplyDoorPassThroughScatter(cellTransforms, initialPositions, scatterDirections, scatterDelays,
                    scatterProgress, scatterDistance);
                ApplyDoorPassThroughRotations(cellTransforms, initialRotations, scatterRotationTargets, scatterDelays,
                    scatterProgress);
                var fadeProgress = ResolveDoorPassThroughFadeProgress(elapsed, fadeStartAt, fadeDurationReciprocal);
                ApplyDoorPassThroughFade(blockView, cellRenderers, fadeProgress);

                if (!burstTriggered && elapsed >= motion.BurstAt)
                {
                    PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
                    burstTriggered = true;
                }

                yield return null;
            }

            motion.PlacementTransform.position = motion.EndPosition;
            ApplyDoorPassThroughFade(blockView, cellRenderers, 1f);
            ApplyDoorPassThroughScatter(cellTransforms, initialPositions, scatterDirections, scatterDelays,
                1f, scatterDistance);
            ApplyDoorPassThroughRotations(cellTransforms, initialRotations, scatterRotationTargets, scatterDelays,
                1f);
            ClearDoorPassThroughVisualOverrides(cellRenderers);

            if (!burstTriggered)
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection);
            }
        }

        private static void CollectActiveDoorPassThroughCells(
            BlockRootView blockView,
            List<Transform> cellTransforms,
            List<Vector3> initialScales,
            List<Vector3> initialPositions,
            List<Quaternion> initialRotations,
            List<float> scatterRotationTargets,
            List<Vector3> scatterDirections,
            List<float> scatterDelays,
            List<Renderer> cellRenderers,
            Vector2Int resolvedExitDirection)
        {
            cellTransforms.Clear();
            initialScales.Clear();
            initialPositions.Clear();
            initialRotations.Clear();
            scatterRotationTargets.Clear();
            scatterDirections.Clear();
            scatterDelays.Clear();
            cellRenderers.Clear();

            var blockCells = blockView.Cells;
            var cachedNestedRenderers = blockView.CellNestedRenderers;
            var scatterDirection = ResolveDoorExitBurstFlowDirection(resolvedExitDirection);
            for (var i = 0; i < blockCells.Count; i++)
            {
                var cellObject = blockCells[i];
                if (!cellObject || !cellObject.activeSelf)
                {
                    continue;
                }

                var cellTransform = cellObject.transform;
                cellTransforms.Add(cellTransform);
                initialScales.Add(cellTransform.localScale);
                initialPositions.Add(cellTransform.localPosition);
                initialRotations.Add(cellTransform.localRotation);

                var childRenderers =
                    i < cachedNestedRenderers.Count ? cachedNestedRenderers[i] : System.Array.Empty<Renderer>();
                for (var rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
                {
                    var childRenderer = childRenderers[rendererIndex];
                    if (childRenderer)
                    {
                        cellRenderers.Add(childRenderer);
                    }
                }

                var directionSeed = ResolveDoorPassThroughScatterDirectionSeed(i, cellTransform.localPosition);
                var scatterAxis = ResolveDoorPassThroughScatterDirection(blockView, cellTransform.localPosition,
                    scatterDirection, i, directionSeed);
                scatterDirections.Add(scatterAxis);
                scatterDelays.Add(ResolveDoorPassThroughScatterDelay(directionSeed));
                scatterRotationTargets.Add(
                    ResolveDoorPassThroughScatterRotation(directionSeed, cellTransform.localPosition));
            }
        }

        private static Vector3 ResolveDoorPassThroughScatterDirection(
            BlockRootView blockView,
            Vector3 localCellPosition,
            Vector3 exitDirection,
            int cellIndex,
            float directionSeed)
        {
            var baseDirection = exitDirection;
            if (baseDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                baseDirection = Vector3.right;
            }

            baseDirection.Normalize();
            var blockCenter = Vector3.zero;
            if (blockView != null)
            {
                blockCenter = new Vector3(blockView.LocalCenter.x, blockView.LocalCenter.y, 0f);
            }

            var radialOffset = localCellPosition - blockCenter;
            if (radialOffset.sqrMagnitude <= Mathf.Epsilon)
            {
                radialOffset = Vector3.right * Mathf.Sign((cellIndex & 1) == 0 ? 1f : -1f);
            }

            radialOffset.Normalize();
            var tangentDirection = new Vector3(-baseDirection.y, baseDirection.x, 0f);
            if (tangentDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                tangentDirection = Vector3.right;
            }

            var seededTangentOffset = Mathf.Lerp(-0.45f, 0.45f, directionSeed);
            var direction = (baseDirection * 1.2f) + (tangentDirection * seededTangentOffset) + (radialOffset * 0.26f);
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return baseDirection;
            }

            direction.Normalize();
            return direction;
        }

        private static float ResolveDoorPassThroughScatterDirectionSeed(int cellIndex, Vector3 localCellPosition)
        {
            var seed = cellIndex * 17f + (localCellPosition.x * 3.17f) + (localCellPosition.y * 7.23f);
            return Mathf.Abs(Mathf.Sin(seed) * 0.5f + 0.5f);
        }

        private static float ResolveDoorPassThroughScatterDelay(float directionSeed)
        {
            return directionSeed * 0.1f;
        }

        private static float ResolveDoorPassThroughScatterRotation(float directionSeed, Vector3 localCellPosition)
        {
            var normalizedRotationSeed =
                Mathf.Repeat(directionSeed + Mathf.Repeat(localCellPosition.x * 0.17f, 1f), 1f);
            return Mathf.Lerp(-DoorPassThroughRotationRangeInDegrees, DoorPassThroughRotationRangeInDegrees,
                normalizedRotationSeed);
        }

        private static float ResolveDoorPassThroughScatterProgress(float elapsed, float scatterStartAt,
            float scatterDurationReciprocal)
        {
            if (elapsed <= scatterStartAt)
            {
                return 0f;
            }

            var normalizedProgress = Mathf.Clamp01((elapsed - scatterStartAt) * scatterDurationReciprocal);
            return Mathf.SmoothStep(0f, 1f, normalizedProgress);
        }

        private static void ApplyDoorPassThroughScatter(
            IReadOnlyList<Transform> cellTransforms,
            IReadOnlyList<Vector3> initialPositions,
            IReadOnlyList<Vector3> scatterDirections,
            IReadOnlyList<float> scatterDelays,
            float scatterProgress,
            float scatterDistance)
        {
            if (cellTransforms == null || initialPositions == null || scatterDirections == null ||
                scatterDelays == null)
            {
                return;
            }

            var transformCount = cellTransforms.Count;
            transformCount = Mathf.Min(transformCount, initialPositions.Count);
            transformCount = Mathf.Min(transformCount, scatterDirections.Count);
            transformCount = Mathf.Min(transformCount, scatterDelays.Count);
            if (transformCount <= 0)
            {
                return;
            }

            for (var i = 0; i < transformCount; i++)
            {
                var cellTransform = cellTransforms[i];
                if (!cellTransform)
                {
                    continue;
                }

                var delay = Mathf.Clamp01(scatterDelays[i]);
                var localScatterProgress = 0f;
                if (scatterProgress > delay)
                {
                    localScatterProgress = Mathf.InverseLerp(delay, 1f, scatterProgress);
                    localScatterProgress = Mathf.SmoothStep(0f, 1f, localScatterProgress);
                }

                cellTransform.localPosition = Vector3.LerpUnclamped(
                    initialPositions[i],
                    initialPositions[i] + (scatterDirections[i] * scatterDistance),
                    localScatterProgress);
            }
        }

        private static void ApplyDoorPassThroughRotations(
            IReadOnlyList<Transform> cellTransforms,
            IReadOnlyList<Quaternion> initialRotations,
            IReadOnlyList<float> scatterRotationTargets,
            IReadOnlyList<float> scatterDelays,
            float scatterProgress)
        {
            if (cellTransforms == null || initialRotations == null || scatterRotationTargets == null ||
                scatterDelays == null)
            {
                return;
            }

            var transformCount = cellTransforms.Count;
            transformCount = Mathf.Min(transformCount, initialRotations.Count);
            transformCount = Mathf.Min(transformCount, scatterRotationTargets.Count);
            transformCount = Mathf.Min(transformCount, scatterDelays.Count);
            if (transformCount <= 0)
            {
                return;
            }

            for (var i = 0; i < transformCount; i++)
            {
                var cellTransform = cellTransforms[i];
                if (!cellTransform)
                {
                    continue;
                }

                var delay = Mathf.Clamp01(scatterDelays[i]);
                var localScatterProgress = 0f;
                if (scatterProgress > delay)
                {
                    localScatterProgress = Mathf.InverseLerp(delay, 1f, scatterProgress);
                    localScatterProgress = Mathf.SmoothStep(0f, 1f, localScatterProgress);
                }

                var rotationOffset = Quaternion.Euler(0f, 0f, scatterRotationTargets[i] * localScatterProgress);
                cellTransform.localRotation = initialRotations[i] * rotationOffset;
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
            var collapseStartAt = travelDuration * 0.46f;
            var burstAt = travelDuration * DoorPassThroughBurstAtProgress;

            motion = new DoorPassThroughMotion(
                placementTransform,
                startPosition,
                traverseEndPosition,
                travelDuration,
                collapseStartAt,
                burstAt);

            return true;
        }

        private static float ResolveDoorPassThroughCollapseProgress(
            float elapsed,
            float collapseStartAt,
            float collapseDurationReciprocal)
        {
            if (elapsed <= collapseStartAt)
            {
                return 0f;
            }

            var normalizedProgress = Mathf.Clamp01((elapsed - collapseStartAt) * collapseDurationReciprocal);
            return Mathf.SmoothStep(0f, 1f, normalizedProgress);
        }

        private static float ResolveDoorPassThroughFadeProgress(
            float elapsed,
            float fadeStartAt,
            float fadeDurationReciprocal)
        {
            if (elapsed <= fadeStartAt)
            {
                return 0f;
            }

            var normalizedProgress = Mathf.Clamp01((elapsed - fadeStartAt) * fadeDurationReciprocal);
            return Mathf.SmoothStep(0f, 1f, normalizedProgress);
        }

        private static void ApplyDoorPassThroughScale(
            IReadOnlyList<Transform> cellTransforms,
            IReadOnlyList<Vector3> initialScales,
            float scaleFactor)
        {
            if (cellTransforms == null || initialScales == null)
            {
                return;
            }

            var transformCount = cellTransforms.Count;
            if (initialScales.Count < transformCount)
            {
                transformCount = initialScales.Count;
            }

            for (var i = 0; i < transformCount; i++)
            {
                var cellTransform = cellTransforms[i];
                if (!cellTransform)
                {
                    continue;
                }

                cellTransform.localScale = initialScales[i] * scaleFactor;
            }
        }

        private static void ApplyDoorPassThroughPositions(
            IReadOnlyList<Transform> cellTransforms,
            IReadOnlyList<Vector3> initialPositions)
        {
            if (cellTransforms == null || initialPositions == null)
            {
                return;
            }

            var transformCount = cellTransforms.Count;
            if (initialPositions.Count < transformCount)
            {
                transformCount = initialPositions.Count;
            }

            for (var i = 0; i < transformCount; i++)
            {
                var cellTransform = cellTransforms[i];
                if (!cellTransform)
                {
                    continue;
                }

                cellTransform.localPosition = initialPositions[i];
            }
        }

        private void ApplyDoorPassThroughFade(BlockRootView blockView, IReadOnlyList<Renderer> cellRenderers,
            float fadeProgress)
        {
            if (blockView == null || cellRenderers == null)
            {
                return;
            }

            if (!TryResolveBlockCellMaterialColor(blockView, out var blockColor))
            {
                return;
            }

            var alpha = Mathf.Clamp01(Mathf.Lerp(1f, 0f, fadeProgress));
            if (alpha <= 0f)
            {
                ClearDoorPassThroughVisualOverrides(cellRenderers);
                return;
            }

            var fadedColor = new Color(blockColor.r, blockColor.g, blockColor.b, alpha);
            _fxRendererPropertyBlock ??= new MaterialPropertyBlock();

            for (var i = 0; i < cellRenderers.Count; i++)
            {
                var renderer = cellRenderers[i];
                if (!renderer)
                {
                    continue;
                }

                _fxRendererPropertyBlock.Clear();
                var sharedMaterial = renderer.sharedMaterial;
                var hadColorProperty = false;

                if (sharedMaterial != null)
                {
                    if (sharedMaterial.HasProperty(BaseColorPropertyId))
                    {
                        _fxRendererPropertyBlock.SetColor(BaseColorPropertyId, fadedColor);
                        hadColorProperty = true;
                    }

                    if (sharedMaterial.HasProperty(ColorPropertyId))
                    {
                        _fxRendererPropertyBlock.SetColor(ColorPropertyId, fadedColor);
                        hadColorProperty = true;
                    }
                }

                if (hadColorProperty)
                {
                    renderer.SetPropertyBlock(_fxRendererPropertyBlock);
                }
            }
        }

        private static void ClearDoorPassThroughVisualOverrides(IReadOnlyList<Renderer> cellRenderers)
        {
            if (cellRenderers == null)
            {
                return;
            }

            for (var i = 0; i < cellRenderers.Count; i++)
            {
                var renderer = cellRenderers[i];
                if (!renderer)
                {
                    continue;
                }

                renderer.SetPropertyBlock(null);
            }
        }

        private static Vector3 ResolveDoorExitBurstFlowDirection(Vector2Int exitDirection)
        {
            var worldDirection = new Vector3(exitDirection.x, exitDirection.y, 0f);
            if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.right;
            }

            worldDirection.Normalize();
            return worldDirection;
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
