using System.Collections;
using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private static readonly int BlockDoorExitTriggerHash = Animator.StringToHash("DoorExit");
        private static readonly int BlockIdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private const float DoorExitBurstCleanupDelay = 0.28f;
        private const float DoorExitBurstCleanupMaxWait = 0.56f;
        private const float DoorPassThroughScatterDistanceInCells = 0.78f;
        private const float DoorPassThroughMinScale = 0.06f;
        private const float DoorPassThroughRotationRangeInDegrees = 280f;
        private const float DoorPassThroughBurstAtProgress = 0.66f;
        private const string RuntimeDoorExitBurstNamePrefix = "PS_DoorExitBurst_Runtime_";

        private static readonly ParticleSystem.Burst[] DoorExitBurstPattern =
        {
            new ParticleSystem.Burst(0f, (short)30, (short)42, 1, 0f),
            new ParticleSystem.Burst(0.048f, (short)14, (short)22, 1, 0f)
        };
        private static readonly AnimationCurve DoorExitBurstSizeCurve = new(
            new Keyframe(0f, 1f, 0f, -1.25f),
            new Keyframe(0.52f, 0.46f, -0.65f, -0.35f),
            new Keyframe(1f, 0.04f, -0.12f, 0f));
        private static readonly Gradient DoorExitBurstAlphaGradient = CreateDoorExitBurstAlphaGradient();
        private static Mesh _doorExitBurstCubeMesh;

        private readonly struct DoorPassThroughMotion
        {
            public DoorPassThroughMotion(
                Transform placementTransform,
                Vector3 startPosition,
                Vector3 endPosition,
                float travelDuration,
                float collapseStartAt,
                float burstAt)
            {
                PlacementTransform = placementTransform;
                StartPosition = startPosition;
                EndPosition = endPosition;
                TravelDuration = travelDuration;
                CollapseStartAt = collapseStartAt;
                BurstAt = burstAt;
            }

            public Transform PlacementTransform { get; }
            public Vector3 StartPosition { get; }
            public Vector3 EndPosition { get; }
            public float TravelDuration { get; }
            public float CollapseStartAt { get; }
            public float BurstAt { get; }
        }

        private void ReleaseRuntimeFxResources()
        {
            RecycleAllDoorExitBurstParticles();
            _doorExitBurstRendererByParticleId.Clear();
            _dragHighlightPresenter.ResetRuntimeResources();
        }

        private static void ResetBlockAnimatorState(Animator animator)
        {
            if (!animator || !animator.runtimeAnimatorController)
            {
                return;
            }

            if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            {
                return;
            }

            animator.ResetTrigger(BlockDoorExitTriggerHash);
            animator.Play(0, 0, 0f);
        }

        private static void PlayBlockDoorExitAnimation(BlockRootView blockView)
        {
            var animator = blockView?.Animator;
            if (!animator || !animator.runtimeAnimatorController)
            {
                return;
            }

            if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            {
                return;
            }

            animator.ResetTrigger(BlockDoorExitTriggerHash);
            animator.Play(0, 0, 0f);
            animator.SetTrigger(BlockDoorExitTriggerHash);
        }

        private IEnumerator WaitForBlockDoorExitAnimationComplete(Animator animator)
        {
            if (!animator || !animator.runtimeAnimatorController)
            {
                yield break;
            }

            if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            {
                yield break;
            }

            var timeoutAt = Time.unscaledTime + 4f;
            var exitedIdleState = false;
            while (animator && Time.unscaledTime < timeoutAt)
            {
                if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!exitedIdleState)
                {
                    if (stateInfo.fullPathHash != BlockIdleStateHash)
                    {
                        exitedIdleState = true;
                    }
                }
                else if (stateInfo.fullPathHash == BlockIdleStateHash)
                {
                    yield break;
                }

                yield return null;
            }

            ResetBlockAnimatorState(animator);
        }

        private void StopDoorExitBurstParticle(BlockRootView blockView)
        {
            StopDoorExitBurstCleanup(blockView);

            if (blockView == null)
            {
                return;
            }

            var burstParticle = blockView.DoorExitBurstParticle;
            if (burstParticle != null)
            {
                ResetDoorExitBurstParticleState(burstParticle, disableObject: true, disableRenderer: true);

                ReturnDoorExitBurstParticleToPool(burstParticle);
            }

            blockView.DoorExitBurstParticle = null;
            blockView.DoorExitBurstRenderer = null;
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
            _dragHighlightPresenter.SetDragHighlightActive(blockView, isActive, BuildDragHighlightSettings(),
                SetActiveIfChanged, TryResolveBlockCellMaterialColor);
        }

        private void RefreshDragHighlightBounds(BlockRootView blockView)
        {
            _dragHighlightPresenter.RefreshDragHighlightBounds(blockView, BuildDragHighlightSettings(),
                TryResolveBlockCellMaterialColor);
        }

        private DragHighlightPresenter.DragHighlightSettings BuildDragHighlightSettings()
        {
            return new DragHighlightPresenter.DragHighlightSettings(CellSize, dragOutlineBaseOffsetInCells,
                dragOutlineGapInCells, dragOutlineVerticalOffsetInCells, dragOutlineThicknessInCells, dragOutlineColor,
                dragOutlineSourceMaterial);
        }

        private void PlayBlockExitDisintegrateFx(BlockRootView blockView, Vector2Int exitDirection = default,
            bool scheduleAutoCleanup = true)
        {
            if (blockView == null)
            {
                return;
            }

            PlayDoorExitBurstParticleFx(blockView, exitDirection, scheduleAutoCleanup);
        }

        private void PlayDoorExitBurstParticleFx(BlockRootView blockView, Vector2Int exitDirection,
            bool scheduleAutoCleanup)
        {
            EnsureDoorExitBurstParticle(blockView);
            var burstParticle = blockView?.DoorExitBurstParticle;
            if (burstParticle == null)
            {
                return;
            }

            var burstObject = burstParticle.gameObject;
            if (!burstObject)
            {
                return;
            }

            var burstFlowDirection = ResolveDoorExitBurstFlowDirection(exitDirection);
            var burstOriginOffset = ResolveDoorExitBurstOriginOffset(blockView);
            var burstPosition = ResolveBlockCenterWorld(blockView) + (burstFlowDirection * burstOriginOffset);
            var burstRotation = Quaternion.LookRotation(burstFlowDirection, Vector3.forward);
            var burstTransform = burstParticle.transform;
            burstTransform.SetPositionAndRotation(burstPosition, burstRotation);
            burstTransform.localScale = ResolveBurstScale(blockView);

            SetActiveIfChanged(burstObject, true);
            ResetDoorExitBurstParticleState(burstParticle);
            ConfigureDoorExitBurstParticle(burstParticle, burstFlowDirection, blockView.DoorExitBurstRenderer);
            var burstColor = ResolveBlockBurstColor(blockView);
            var burstMain = burstParticle.main;
            burstMain.startColor = burstColor;
            ApplyDoorExitBurstRendererTint(blockView, burstColor);
            if (blockView.DoorExitBurstRenderer)
            {
                blockView.DoorExitBurstRenderer.enabled = true;
            }

            burstParticle.Play(true);

            if (!scheduleAutoCleanup)
            {
                return;
            }

            StopDoorExitBurstCleanup(blockView);
            blockView.DoorExitBurstCleanupRoutine =
                StartCoroutine(CleanupDoorExitBurstAfterDelay(blockView, DoorExitBurstCleanupDelay));
        }

        private void ApplyDoorExitBurstRendererTint(BlockRootView blockView, Color burstColor)
        {
            if (blockView == null)
            {
                return;
            }

            var particleRenderer = blockView.DoorExitBurstRenderer;
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

        private static void ConfigureDoorExitBurstParticle(ParticleSystem burstParticle, Vector3 burstFlowDirection,
            ParticleSystemRenderer particleRenderer)
        {
            if (burstParticle == null)
            {
                return;
            }

            var main = burstParticle.main;
            main.loop = false;
            main.useUnscaledTime = true;
            main.simulationSpeed = 0.95f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.15f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.145f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f, 0.035f);
            main.stopAction = ParticleSystemStopAction.None;

            var emission = burstParticle.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(DoorExitBurstPattern, DoorExitBurstPattern.Length);

            var shape = burstParticle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f;
            shape.radius = 0.14f;
            shape.radiusThickness = 0.62f;
            shape.length = 0.24f;
            shape.alignToDirection = true;
            shape.randomDirectionAmount = 0.12f;
            shape.randomPositionAmount = 0.08f;
            shape.sphericalDirectionAmount = 0f;

            var normalizedFlowDirection = burstFlowDirection.sqrMagnitude > Mathf.Epsilon
                ? burstFlowDirection.normalized
                : Vector3.right;
            var velocityOverLifetime = burstParticle.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(normalizedFlowDirection.x * 1.15f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(normalizedFlowDirection.y * 1.15f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(normalizedFlowDirection.z * 1.15f);

            var sizeOverLifetime = burstParticle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, DoorExitBurstSizeCurve);

            var colorOverLifetime = burstParticle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(DoorExitBurstAlphaGradient);

            var noise = burstParticle.noise;
            noise.enabled = false;

            var collision = burstParticle.collision;
            collision.enabled = false;

            var trails = burstParticle.trails;
            trails.enabled = false;

            ConfigureDoorExitBurstRenderer(particleRenderer);
        }

        private void EnsureDoorExitBurstParticle(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            if (blockView.DoorExitBurstParticle != null)
            {
                blockView.DoorExitBurstRenderer =
                    ResolveDoorExitBurstRendererFromCache(blockView.DoorExitBurstParticle);

                return;
            }

            var burstParticle = AcquireDoorExitBurstParticle();
            if (burstParticle == null)
            {
                return;
            }

            blockView.DoorExitBurstParticle = burstParticle;
            blockView.DoorExitBurstRenderer = ResolveDoorExitBurstRendererFromCache(burstParticle);
        }

        private void EnsureDoorExitBurstPoolCapacity(int requiredCount)
        {
            RebindDoorExitBurstPoolFromScene();

            if (requiredCount <= 0)
            {
                return;
            }

            if (_doorExitBurstParticlePool.Count < requiredCount)
            {
                ExpandDoorExitBurstPool(requiredCount);
            }
        }

        private void ExpandDoorExitBurstPool(int requiredCount)
        {
            var missingCount = Mathf.Max(0, requiredCount - _doorExitBurstParticlePool.Count);
            for (var i = 0; i < missingCount; i++)
            {
                var burstParticle = CreateDoorExitBurstParticle(_doorExitBurstParticlePool.Count);
                if (burstParticle == null)
                {
                    return;
                }

                ReturnDoorExitBurstParticleToPool(burstParticle);
            }
        }

        private ParticleSystem CreateDoorExitBurstParticle(int index)
        {
            ParticleSystem burstParticle;
            var template = ResolveDoorExitBurstTemplate();
            if (template)
            {
                burstParticle = Instantiate(template, transform);
                burstParticle.name = $"{RuntimeDoorExitBurstNamePrefix}{index:000}";
            }
            else
            {
                var burstObject = new GameObject($"{RuntimeDoorExitBurstNamePrefix}{index:000}");
                burstObject.transform.SetParent(transform, false);
                burstParticle = burstObject.AddComponent<ParticleSystem>();
            }

            burstParticle.TryGetComponent<ParticleSystemRenderer>(out var particleRenderer);
            _doorExitBurstParticlePool.Add(burstParticle);
            _doorExitBurstRendererByParticleId[burstParticle.GetInstanceID()] = particleRenderer;
            ResetDoorExitBurstParticleState(burstParticle, disableObject: true, disableRenderer: true);
            ConfigureDoorExitBurstParticle(burstParticle, Vector3.right, particleRenderer);
            return burstParticle;
        }

        private ParticleSystem ResolveDoorExitBurstTemplate()
        {
            for (var i = 0; i < _doorExitBurstParticlePool.Count; i++)
            {
                var pooledParticle = _doorExitBurstParticlePool[i];
                if (pooledParticle)
                {
                    return pooledParticle;
                }
            }

            if (doorExitBurstParticles == null)
            {
                return null;
            }

            for (var i = 0; i < doorExitBurstParticles.Count; i++)
            {
                var pooledParticle = doorExitBurstParticles[i];
                if (pooledParticle)
                {
                    return pooledParticle;
                }
            }

            return null;
        }

        private ParticleSystem AcquireDoorExitBurstParticle()
        {
            while (_availableDoorExitBurstParticles.Count > 0)
            {
                var pooledParticle = _availableDoorExitBurstParticles.Pop();
                if (pooledParticle == null)
                {
                    continue;
                }

                _availableDoorExitBurstParticleIds.Remove(pooledParticle.GetInstanceID());
                return pooledParticle;
            }

            return null;
        }

        private void ReturnDoorExitBurstParticleToPool(ParticleSystem burstParticle, bool allowReparent = true)
        {
            if (burstParticle == null)
            {
                return;
            }

            if (!_doorExitBurstParticlePool.Contains(burstParticle))
            {
                _doorExitBurstParticlePool.Add(burstParticle);
            }

            ResetDoorExitBurstParticleState(burstParticle);
            var burstObject = burstParticle.gameObject;
            if (burstObject != null)
            {
                var burstTransform = burstObject.transform;
                if (allowReparent && burstTransform.parent != transform)
                {
                    burstTransform.SetParent(transform, false);
                }

                SetActiveIfChanged(burstObject, false);
            }

            var burstInstanceId = burstParticle.GetInstanceID();
            if (_availableDoorExitBurstParticleIds.Add(burstInstanceId))
            {
                _availableDoorExitBurstParticles.Push(burstParticle);
            }
        }

        private static void ResetDoorExitBurstParticleState(ParticleSystem burstParticle, bool disableObject = false,
            bool disableRenderer = false)
        {
            if (burstParticle == null)
            {
                return;
            }

            burstParticle.Simulate(0f, false, true);
            burstParticle.Clear(true);
            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var burstRenderer = burstParticle.GetComponent<ParticleSystemRenderer>();
            if (disableRenderer && burstRenderer)
            {
                burstRenderer.enabled = false;
            }

            if (!disableObject)
            {
                return;
            }

            var burstObject = burstParticle.gameObject;
            if (burstObject != null)
            {
                SetActiveIfChanged(burstObject, false);
            }
        }

        private void RecycleAllDoorExitBurstParticles()
        {
            RebindDoorExitBurstPoolFromScene();
            _availableDoorExitBurstParticles.Clear();
            _availableDoorExitBurstParticleIds.Clear();

            for (var i = _doorExitBurstParticlePool.Count - 1; i >= 0; i--)
            {
                var pooledParticle = _doorExitBurstParticlePool[i];
                if (pooledParticle == null)
                {
                    _doorExitBurstParticlePool.RemoveAt(i);
                    continue;
                }

                ReturnDoorExitBurstParticleToPool(pooledParticle, allowReparent: false);
            }
        }

        private void RebindDoorExitBurstPoolFromScene()
        {
            _doorExitBurstParticlePool.Clear();
            _doorExitBurstRendererByParticleId.Clear();
            _availableDoorExitBurstParticles.Clear();
            _availableDoorExitBurstParticleIds.Clear();

            if (doorExitBurstParticles != null)
            {
                for (var i = 0; i < doorExitBurstParticles.Count; i++)
                {
                    var pooledParticle = doorExitBurstParticles[i];
                    AddDoorExitBurstParticleToRuntimePool(pooledParticle);
                }
            }

            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child || !child.name.StartsWith(RuntimeDoorExitBurstNamePrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                AddDoorExitBurstParticleToRuntimePool(child.GetComponent<ParticleSystem>());
            }
        }

        private void AddDoorExitBurstParticleToRuntimePool(ParticleSystem pooledParticle)
        {
            if (!pooledParticle)
            {
                return;
            }

            if (_doorExitBurstParticlePool.Contains(pooledParticle))
            {
                return;
            }

            _doorExitBurstParticlePool.Add(pooledParticle);
            pooledParticle.TryGetComponent<ParticleSystemRenderer>(out var particleRenderer);
            _doorExitBurstRendererByParticleId[pooledParticle.GetInstanceID()] = particleRenderer;
            ResetDoorExitBurstParticleState(pooledParticle, disableObject: true, disableRenderer: true);
            _availableDoorExitBurstParticleIds.Add(pooledParticle.GetInstanceID());
            _availableDoorExitBurstParticles.Push(pooledParticle);
        }

        private ParticleSystemRenderer ResolveDoorExitBurstRendererFromCache(ParticleSystem burstParticle)
        {
            if (!burstParticle)
            {
                return null;
            }

            _doorExitBurstRendererByParticleId.TryGetValue(burstParticle.GetInstanceID(), out var particleRenderer);
            return particleRenderer;
        }

        private static Vector3 ResolveBlockCenterWorld(BlockRootView blockView)
        {
            if (blockView == null || blockView.RootTransform == null)
            {
                return Vector3.zero;
            }

            if (!TryResolveActiveCellBoundsWorld(blockView, out var minWorld, out var maxWorld))
            {
                return blockView.RootTransform.position;
            }

            return (minWorld + maxWorld) * 0.5f;
        }

        private static Vector3 ResolveBurstScale(BlockRootView blockView)
        {
            if (!TryResolveActiveCellBoundsWorld(blockView, out var minWorld, out var maxWorld))
            {
                return Vector3.one * 0.5f;
            }

            var size = maxWorld - minWorld;
            var maxSize = Mathf.Max(size.x, size.y, size.z);
            var uniformScale = Mathf.Clamp(maxSize * 0.32f, 0.32f, 0.95f);
            return Vector3.one * uniformScale;
        }

        private IEnumerator AnimateBlockDoorExitSequence(BlockRootView blockView, DoorOpeningData matchedDoor,
            Vector2Int resolvedExitDirection)
        {
            PlayBlockDoorExitAnimation(blockView);
            yield return AnimateBlockDoorPassThrough(blockView, matchedDoor, resolvedExitDirection);
            yield return WaitForBlockDoorExitAnimationComplete(blockView?.Animator);
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
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection, false);
                ClearDoorPassThroughVisualOverrides(cellRenderers);
                yield break;
            }

            if (!TryResolveDoorPassThroughMotion(blockView, matchedDoor, resolvedExitDirection, out var motion))
            {
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection, false);
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
                    PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection, false);
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
                PlayBlockExitDisintegrateFx(blockView, resolvedExitDirection, false);
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
                cellRenderers.Add(cellObject.TryGetComponent<Renderer>(out var renderer) ? renderer : null);

                var scatterAxis = ResolveDoorPassThroughScatterDirection(blockView, cellTransform.localPosition,
                    scatterDirection, i);
                scatterDirections.Add(scatterAxis);
                scatterDelays.Add(ResolveDoorPassThroughScatterDelay(i, cellTransform.localPosition));
                scatterRotationTargets.Add(ResolveDoorPassThroughScatterRotation(i, cellTransform.localPosition));
            }
        }

        private static Vector3 ResolveDoorPassThroughScatterDirection(
            BlockRootView blockView,
            Vector3 localCellPosition,
            Vector3 exitDirection,
            int cellIndex)
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

            var seededOffset = ResolveDoorPassThroughScatterDirectionSeed(cellIndex, localCellPosition);
            var seededTangentOffset = Mathf.Lerp(-0.45f, 0.45f, seededOffset);
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

        private static float ResolveDoorPassThroughScatterDelay(int cellIndex, Vector3 localCellPosition)
        {
            var seed = ResolveDoorPassThroughScatterDirectionSeed(cellIndex, localCellPosition);
            return seed * 0.1f;
        }

        private static float ResolveDoorPassThroughScatterRotation(int cellIndex, Vector3 localCellPosition)
        {
            var seed = ResolveDoorPassThroughScatterDirectionSeed(cellIndex, localCellPosition);
            var normalizedRotationSeed = Mathf.Repeat(seed + Mathf.Repeat(localCellPosition.x * 0.17f, 1f), 1f);
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
            if (blockView == null)
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

            foreach (var renderer in cellRenderers)
            {
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

        private float ResolveDoorExitBurstOriginOffset(BlockRootView blockView)
        {
            if (!TryResolveActiveCellBoundsWorld(blockView, out var minWorld, out var maxWorld))
            {
                return CellSize * 0.16f;
            }

            var size = maxWorld - minWorld;
            var maxPlanarSize = Mathf.Max(size.x, size.y);
            var desiredOffset = maxPlanarSize * 0.32f;
            return Mathf.Clamp(desiredOffset, CellSize * 0.16f, CellSize * 0.58f);
        }

        private static void ConfigureDoorExitBurstRenderer(ParticleSystemRenderer particleRenderer)
        {
            if (!particleRenderer)
            {
                return;
            }

            var cubeMesh = ResolveDoorExitBurstCubeMesh();
            if (cubeMesh)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                particleRenderer.mesh = cubeMesh;
                particleRenderer.alignment = ParticleSystemRenderSpace.World;
            }
            else
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }

        private static Mesh ResolveDoorExitBurstCubeMesh()
        {
            if (_doorExitBurstCubeMesh)
            {
                return _doorExitBurstCubeMesh;
            }

            _doorExitBurstCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _doorExitBurstCubeMesh;
        }

        private static Gradient CreateDoorExitBurstAlphaGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private IEnumerator CleanupDoorExitBurstAfterDelay(BlockRootView blockView, float delay)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, delay));

            if (blockView == null)
            {
                yield break;
            }

            var burstParticle = blockView.DoorExitBurstParticle;
            var cleanupTimeoutAt = Time.unscaledTime + DoorExitBurstCleanupMaxWait;
            while (burstParticle && burstParticle.IsAlive(true) && Time.unscaledTime < cleanupTimeoutAt)
            {
                yield return null;
            }

            blockView.DoorExitBurstCleanupRoutine = null;
            StopDoorExitBurstParticle(blockView);
        }

        private void StopDoorExitBurstCleanup(BlockRootView blockView)
        {
            if (blockView?.DoorExitBurstCleanupRoutine == null)
            {
                return;
            }

            StopCoroutine(blockView.DoorExitBurstCleanupRoutine);
            blockView.DoorExitBurstCleanupRoutine = null;
        }

        private void CacheBlockOutlineGridLoop(BlockRootView blockView, Vector2Int[] localCells)
        {
            _dragHighlightPresenter.CacheBlockOutlineGridLoop(blockView, localCells);
        }

        private static bool TryResolveActiveCellBoundsWorld(BlockRootView blockView, out Vector3 minWorld,
            out Vector3 maxWorld)
        {
            minWorld = default;
            maxWorld = default;
            if (blockView == null || !blockView.HasCachedLocalBounds || blockView.RootTransform == null)
            {
                return false;
            }

            var worldA = blockView.RootTransform.TransformPoint(blockView.CachedLocalBoundsMin);
            var worldB = blockView.RootTransform.TransformPoint(blockView.CachedLocalBoundsMax);
            minWorld = Vector3.Min(worldA, worldB);
            maxWorld = Vector3.Max(worldA, worldB);
            return true;
        }
    }
}
