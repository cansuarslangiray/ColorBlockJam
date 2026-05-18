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
        private static readonly int ZTestPropertyId = Shader.PropertyToID("_ZTest");
        private static readonly int ZWritePropertyId = Shader.PropertyToID("_ZWrite");
        private const int CompareFunctionAlways = 8;
        private const float DoorExitBurstCleanupDelay = 0.22f;
        private const float DoorExitBurstCleanupMaxWait = 0.28f;
        private const int DragOutlineCornerVertices = 4;
        private const int DragOutlineCapVertices = 2;
        private static readonly ParticleSystem.Burst[] DoorExitBurstPattern =
        {
            new ParticleSystem.Burst(0f, (short)24, (short)34, 1, 0f),
            new ParticleSystem.Burst(0.055f, (short)8, (short)14, 1, 0f)
        };
        private static readonly AnimationCurve DoorExitBurstSizeCurve = new(
            new Keyframe(0f, 1f, 0f, -1.25f),
            new Keyframe(0.52f, 0.46f, -0.65f, -0.35f),
            new Keyframe(1f, 0.04f, -0.12f, 0f));
        private static readonly Gradient DoorExitBurstAlphaGradient = CreateDoorExitBurstAlphaGradient();
        private static Mesh _doorExitBurstCubeMesh;

        private readonly struct DirectedGridEdge
        {
            public DirectedGridEdge(Vector2Int start, Vector2Int end)
            {
                Start = start;
                End = end;
            }

            public Vector2Int Start { get; }
            public Vector2Int End { get; }
        }

        private readonly struct UndirectedGridEdgeKey : System.IEquatable<UndirectedGridEdgeKey>
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
        }

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

            if (!_dragOutlineMaterial)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_dragOutlineMaterial);
            }
            else
            {
                DestroyImmediate(_dragOutlineMaterial);
            }

            _dragOutlineMaterial = null;
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
            if (blockView == null || blockView.RootTransform == null)
            {
                return;
            }

            if (isActive)
            {
                EnsureDragOutlineFrame(blockView);
            }

            var outlineRenderer = blockView.DragOutlineRenderer;
            var outlineObject = outlineRenderer ? outlineRenderer.gameObject : null;

            if (!isActive)
            {
                SetActiveIfChanged(outlineObject, false);
                return;
            }

            var outlineColor = ResolveDragOutlineColor(blockView);
            var outlineMaterial = ResolveDragOutlineMaterial(outlineRenderer ? outlineRenderer.sharedMaterial : null);
            ApplyDragOutlineColor(outlineMaterial, outlineColor);
            if (outlineRenderer != null && outlineMaterial)
            {
                outlineRenderer.sharedMaterial = outlineMaterial;
            }

            RefreshDragHighlightBounds(blockView);
            if (outlineRenderer != null)
            {
                SetActiveIfChanged(outlineObject, true);
            }
        }

        private void EnsureDragOutlineFrame(BlockRootView blockView)
        {
            if (blockView.DragOutlineRenderer != null)
            {
                return;
            }

            var outlineObject = new GameObject("BlockDragOutline");
            var outlineTransform = outlineObject.transform;
            outlineTransform.SetParent(blockView.RootTransform, false);
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;

            var outlineRenderer = outlineObject.AddComponent<LineRenderer>();
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.positionCount = 0;
            outlineRenderer.alignment = LineAlignment.View;
            outlineRenderer.numCornerVertices = DragOutlineCornerVertices;
            outlineRenderer.numCapVertices = DragOutlineCapVertices;
            outlineRenderer.textureMode = LineTextureMode.Stretch;
            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness();
            outlineRenderer.startColor = dragOutlineColor;
            outlineRenderer.endColor = dragOutlineColor;
            outlineRenderer.sortingOrder = 12000;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.sharedMaterial = ResolveDragOutlineMaterial(outlineRenderer.sharedMaterial);

            blockView.DragOutlineRenderer = outlineRenderer;
            SetActiveIfChanged(outlineObject, false);
        }

        private Material ResolveDragOutlineMaterial(Material fallbackSourceMaterial)
        {
            if (_dragOutlineMaterial)
            {
                ApplyDragOutlineColor(_dragOutlineMaterial, dragOutlineColor);
                ApplyDragOutlineRenderOverrides(_dragOutlineMaterial);
                return _dragOutlineMaterial;
            }

            var sourceMaterial = dragOutlineSourceMaterial ? dragOutlineSourceMaterial : fallbackSourceMaterial;
            if (!sourceMaterial)
            {
                return null;
            }

            _dragOutlineMaterial = new Material(sourceMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Runtime_BlockDragOutline_Mat"
            };

            ApplyDragOutlineColor(_dragOutlineMaterial, dragOutlineColor);
            ApplyDragOutlineRenderOverrides(_dragOutlineMaterial);
            return _dragOutlineMaterial;
        }

        private static void ApplyDragOutlineColor(Material material, Color outlineColor)
        {
            if (!material)
            {
                return;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", outlineColor);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", outlineColor);
            }
        }

        private static void ApplyDragOutlineRenderOverrides(Material material)
        {
            if (!material)
            {
                return;
            }

            if (material.HasProperty(ZTestPropertyId))
            {
                material.SetInt(ZTestPropertyId, CompareFunctionAlways);
            }

            if (material.HasProperty(ZWritePropertyId))
            {
                material.SetInt(ZWritePropertyId, 0);
            }

            material.renderQueue = Mathf.Max(material.renderQueue, 4000);
        }

        private void RefreshDragHighlightBounds(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            if (!TryResolveActiveCellBoundsLocal(blockView, out var minLocal, out var maxLocal))
            {
                return;
            }

            var outlineHalfThickness = ResolveDragOutlineThickness() * 0.5f;
            var insetGap = Mathf.Max(0f, CellSize * dragOutlineGapInCells);
            var totalInsetX = Mathf.Max(0f, Mathf.Min(insetGap, (maxLocal.x - minLocal.x) * 0.5f - Mathf.Epsilon));
            var totalInsetY = Mathf.Max(0f, Mathf.Min(insetGap, (maxLocal.y - minLocal.y) * 0.5f - Mathf.Epsilon));
            if (totalInsetX > 0f)
            {
                minLocal.x += totalInsetX;
                maxLocal.x -= totalInsetX;
            }

            if (totalInsetY > 0f)
            {
                minLocal.y += totalInsetY;
                maxLocal.y -= totalInsetY;
            }

            var outlineExpansion = Mathf.Max(0f, outlineHalfThickness - Mathf.Min(totalInsetX, totalInsetY));

            var baseInset = Mathf.Max(0f, CellSize * dragOutlineBaseOffsetInCells);
            var forwardBias = Mathf.Max(baseInset, CellSize * 0.02f);
            var outlineBaseZ = minLocal.z - forwardBias;
            var outlineColor = ResolveDragOutlineColor(blockView);

            if (TryResolveActiveCellOutlineVerticesLocal(blockView, outlineExpansion, out var outlineVertices))
            {
                RefreshDragOutlineFrame(blockView, outlineVertices, outlineBaseZ, outlineColor);
                return;
            }

            RefreshDragOutlineFrameRectangle(blockView, minLocal, maxLocal, outlineBaseZ, outlineColor);
        }

        private void RefreshDragOutlineFrame(BlockRootView blockView, IReadOnlyList<Vector2> outlineVertices, float topZ,
            Color outlineColor)
        {
            var outlineRenderer = blockView.DragOutlineRenderer;
            if (outlineRenderer == null)
            {
                return;
            }

            if (outlineVertices == null || outlineVertices.Count < 3)
            {
                outlineRenderer.positionCount = 0;
                return;
            }

            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness();
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.positionCount = outlineVertices.Count;
            var verticalOffset = CellSize * dragOutlineVerticalOffsetInCells;

            for (var i = 0; i < outlineVertices.Count; i++)
            {
                var point = outlineVertices[i];
                outlineRenderer.SetPosition(i, new Vector3(point.x, point.y + verticalOffset, topZ));
            }
        }

        private void RefreshDragOutlineFrameRectangle(BlockRootView blockView, Vector3 minLocal, Vector3 maxLocal,
            float topZ, Color outlineColor)
        {
            var outlineRenderer = blockView.DragOutlineRenderer;
            if (outlineRenderer == null)
            {
                return;
            }

            outlineRenderer.widthMultiplier = ResolveDragOutlineThickness();
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.positionCount = 4;
            var verticalOffset = CellSize * dragOutlineVerticalOffsetInCells;
            outlineRenderer.SetPosition(0, new Vector3(minLocal.x, minLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(1, new Vector3(maxLocal.x, minLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(2, new Vector3(maxLocal.x, maxLocal.y + verticalOffset, topZ));
            outlineRenderer.SetPosition(3, new Vector3(minLocal.x, maxLocal.y + verticalOffset, topZ));
        }

        private void PlayBlockExitDisintegrateFx(BlockRootView blockView, Vector2Int exitDirection = default)
        {
            if (blockView == null)
            {
                return;
            }

            PlayDoorExitBurstParticleFx(blockView, exitDirection);
        }

        private void PlayDoorExitBurstParticleFx(BlockRootView blockView, Vector2Int exitDirection)
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
            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ConfigureDoorExitBurstParticle(burstParticle, burstFlowDirection, blockView.DoorExitBurstRenderer);
            var burstColor = ResolveBlockBurstColor(blockView);
            var burstMain = burstParticle.main;
            burstMain.startColor = burstColor;
            ApplyDoorExitBurstRendererTint(blockView, burstColor);
            burstParticle.Play(true);

            StopDoorExitBurstCleanup(blockView);
            blockView.DoorExitBurstCleanupRoutine =
                StartCoroutine(CleanupDoorExitBurstAfterDelay(blockView, DoorExitBurstCleanupDelay));
        }

        private float ResolveDragOutlineThickness()
        {
            var configuredThickness = Mathf.Max(0.005f, CellSize * dragOutlineThicknessInCells);
            var visualMinimumThickness = Mathf.Max(0.0045f, CellSize * 0.058f);
            return Mathf.Max(configuredThickness, visualMinimumThickness);
        }

        private Color ResolveDragOutlineColor(BlockRootView blockView)
        {
            if (!TryResolveBlockCellMaterialColor(blockView, out var blockColor))
            {
                return dragOutlineColor;
            }

            var liftedColor = Color.Lerp(blockColor, Color.white, 0.18f);
            liftedColor.a = dragOutlineColor.a;
            return liftedColor;
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
            main.simulationSpeed = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
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
                blockView.DoorExitBurstRenderer = ResolveDoorExitBurstRendererFromCache(blockView.DoorExitBurstParticle);

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
            if (requiredCount <= 0 || !doorExitBurstParticlePrefab)
            {
                return;
            }

            while (_doorExitBurstParticlePool.Count < requiredCount)
            {
                var createdParticle = CreateDoorExitBurstPoolParticle();
                if (createdParticle == null)
                {
                    break;
                }

                ReturnDoorExitBurstParticleToPool(createdParticle);
            }
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

            return CreateDoorExitBurstPoolParticle();
        }

        private ParticleSystem CreateDoorExitBurstPoolParticle()
        {
            if (!doorExitBurstParticlePrefab)
            {
                return null;
            }

            var burstParticle = Instantiate(doorExitBurstParticlePrefab, transform);
            if (burstParticle == null)
            {
                return null;
            }

            burstParticle.gameObject.name = "Pooled_DoorExitBurst_" + _doorExitBurstParticlePool.Count;
            burstParticle.TryGetComponent<ParticleSystemRenderer>(out var particleRenderer);
            _doorExitBurstRendererByParticleId[burstParticle.GetInstanceID()] = particleRenderer;
            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _doorExitBurstParticlePool.Add(burstParticle);
            return burstParticle;
        }

        private void ReturnDoorExitBurstParticleToPool(ParticleSystem burstParticle)
        {
            if (burstParticle == null)
            {
                return;
            }

            if (!_doorExitBurstParticlePool.Contains(burstParticle))
            {
                _doorExitBurstParticlePool.Add(burstParticle);
            }

            burstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var burstObject = burstParticle.gameObject;
            if (burstObject != null)
            {
                var burstTransform = burstObject.transform;
                if (burstTransform.parent != transform)
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

        private void RecycleAllDoorExitBurstParticles()
        {
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

                ReturnDoorExitBurstParticleToPool(pooledParticle);
            }
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
            var cellRenderers = blockView.DoorPassThroughCellRendererBuffer;
            CollectActiveDoorPassThroughCells(blockView, cellTransforms, initialScales, cellRenderers);
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

            while (elapsed < motion.TravelDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / motion.TravelDuration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                motion.PlacementTransform.position = Vector3.Lerp(motion.StartPosition, motion.EndPosition, easedProgress);

                var collapseProgress =
                    ResolveDoorPassThroughCollapseProgress(elapsed, motion.CollapseStartAt, collapseDurationReciprocal);
                var scaleFactor = Mathf.Lerp(1f, 0.08f, collapseProgress);
                ApplyDoorPassThroughScale(cellTransforms, initialScales, scaleFactor);
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
            List<Renderer> cellRenderers)
        {
            cellTransforms.Clear();
            initialScales.Clear();
            cellRenderers.Clear();

            var blockCells = blockView.Cells;
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
                cellRenderers.Add(cellObject.TryGetComponent<Renderer>(out var renderer) ? renderer : null);
            }
        }

        private bool TryResolveDoorPassThroughMotion(
            BlockRootView blockView,
            DoorOpeningData matchedDoor,
            Vector2Int resolvedExitDirection,
            out DoorPassThroughMotion motion)
        {
            motion = default;

            var placementTransform = blockView.PlacementTransform ? blockView.PlacementTransform : blockView.RootTransform;
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
            var burstAt = travelDuration * 0.84f;

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

        private bool TryResolveActiveCellOutlineVerticesLocal(BlockRootView blockView, float outlineOffset,
            out List<Vector2> outlineVertices)
        {
            outlineVertices = null;
            if (blockView == null)
            {
                return false;
            }

            var gridLoop = blockView.CachedOutlineGridLoop;
            if (gridLoop == null || gridLoop.Count < 4)
            {
                return false;
            }

            var isClockwise = IsClockwiseLoop(gridLoop);
            var cellSize = CellSize;
            var offsetInWorld = outlineOffset;
            _outlineVerticesBuffer.Clear();
            for (var i = 0; i < gridLoop.Count; i++)
            {
                var gridVertex = gridLoop[i];
                var worldVertex = new Vector2(gridVertex.x * cellSize, gridVertex.y * cellSize);
                var previousVertex = gridLoop[(i - 1 + gridLoop.Count) % gridLoop.Count];
                var nextVertex = gridLoop[(i + 1) % gridLoop.Count];
                var expandedVertex = ExpandOrthogonalCorner(gridVertex, previousVertex, nextVertex, offsetInWorld,
                    worldVertex, cellSize, isClockwise);
                _outlineVerticesBuffer.Add(expandedVertex);
            }

            outlineVertices = _outlineVerticesBuffer;
            return outlineVertices.Count >= 4;
        }

        private void CacheBlockOutlineGridLoop(BlockRootView blockView, Vector2Int[] localCells)
        {
            if (blockView == null)
            {
                return;
            }

            var cachedLoop = blockView.CachedOutlineGridLoop;
            cachedLoop.Clear();

            if (!TryCacheOutlineOccupiedCells(localCells))
            {
                return;
            }

            CacheOutlineBoundaryEdges();
            if (_outlineBoundaryEdgesBuffer.Count < 4)
            {
                return;
            }

            if (!TryCacheOutlineOutgoingEdges())
            {
                cachedLoop.Clear();
                return;
            }

            if (!TryResolveOutlineLoopStartVertex(out var startVertex))
            {
                cachedLoop.Clear();
                return;
            }

            if (!TryBuildOutlineLoop(cachedLoop, startVertex))
            {
                cachedLoop.Clear();
                return;
            }

            if (cachedLoop.Count < 4 || cachedLoop.Count != _outlineOutgoingEdgesBuffer.Count)
            {
                cachedLoop.Clear();
            }
        }

        private bool TryCacheOutlineOccupiedCells(Vector2Int[] localCells)
        {
            _outlineOccupiedCellsBuffer.Clear();
            if (localCells == null || localCells.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < localCells.Length; i++)
            {
                _outlineOccupiedCellsBuffer.Add(localCells[i]);
            }

            return _outlineOccupiedCellsBuffer.Count > 0;
        }

        private void CacheOutlineBoundaryEdges()
        {
            _outlineBoundaryEdgesBuffer.Clear();
            foreach (var cell in _outlineOccupiedCellsBuffer)
            {
                var bottomLeft = new Vector2Int(cell.x, cell.y);
                var bottomRight = new Vector2Int(cell.x + 1, cell.y);
                var topRight = new Vector2Int(cell.x + 1, cell.y + 1);
                var topLeft = new Vector2Int(cell.x, cell.y + 1);

                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, bottomLeft, bottomRight);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, bottomRight, topRight);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, topRight, topLeft);
                ToggleBoundaryEdge(_outlineBoundaryEdgesBuffer, topLeft, bottomLeft);
            }
        }

        private bool TryCacheOutlineOutgoingEdges()
        {
            _outlineOutgoingEdgesBuffer.Clear();
            foreach (var edge in _outlineBoundaryEdgesBuffer.Values)
            {
                if (_outlineOutgoingEdgesBuffer.ContainsKey(edge.Start))
                {
                    return false;
                }

                _outlineOutgoingEdgesBuffer.Add(edge.Start, edge.End);
            }

            return _outlineOutgoingEdgesBuffer.Count >= 4;
        }

        private bool TryResolveOutlineLoopStartVertex(out Vector2Int startVertex)
        {
            startVertex = Vector2Int.zero;
            var hasStart = false;
            foreach (var vertex in _outlineOutgoingEdgesBuffer.Keys)
            {
                if (!hasStart || IsLexicographicallyBefore(vertex, startVertex))
                {
                    startVertex = vertex;
                    hasStart = true;
                }
            }

            return hasStart;
        }

        private bool TryBuildOutlineLoop(List<Vector2Int> cachedLoop, Vector2Int startVertex)
        {
            var currentVertex = startVertex;
            for (var guard = 0; guard <= _outlineOutgoingEdgesBuffer.Count; guard++)
            {
                cachedLoop.Add(currentVertex);
                if (!_outlineOutgoingEdgesBuffer.TryGetValue(currentVertex, out var nextVertex))
                {
                    return false;
                }

                if (nextVertex == startVertex)
                {
                    return true;
                }

                currentVertex = nextVertex;
            }

            return false;
        }

        private static void ToggleBoundaryEdge(Dictionary<UndirectedGridEdgeKey, DirectedGridEdge> boundaryEdgesByKey,
            Vector2Int start, Vector2Int end)
        {
            var edgeKey = new UndirectedGridEdgeKey(start, end);
            if (boundaryEdgesByKey.Remove(edgeKey))
            {
                return;
            }

            boundaryEdgesByKey.Add(edgeKey, new DirectedGridEdge(start, end));
        }

        private static Vector2 ExpandOrthogonalCorner(Vector2Int currentVertex, Vector2Int previousVertex,
            Vector2Int nextVertex, float outlineOffset, Vector2 fallbackVertex, float cellSize, bool isClockwise)
        {
            if (Mathf.Approximately(outlineOffset, 0f))
            {
                return fallbackVertex;
            }

            var incoming = currentVertex - previousVertex;
            var outgoing = nextVertex - currentVertex;
            if (incoming == Vector2Int.zero || outgoing == Vector2Int.zero)
            {
                return fallbackVertex;
            }

            var incomingDirection = new Vector2Int(ClampToSign(incoming.x), ClampToSign(incoming.y));
            var outgoingDirection = new Vector2Int(ClampToSign(outgoing.x), ClampToSign(outgoing.y));
            if (incomingDirection == -outgoingDirection)
            {
                return fallbackVertex;
            }

            var incomingOutwardNormal = isClockwise
                ? new Vector2(-incomingDirection.y, incomingDirection.x)
                : new Vector2(incomingDirection.y, -incomingDirection.x);
            var outgoingOutwardNormal = isClockwise
                ? new Vector2(-outgoingDirection.y, outgoingDirection.x)
                : new Vector2(outgoingDirection.y, -outgoingDirection.x);
            var offsetDirection = incomingOutwardNormal + outgoingOutwardNormal;
            if (offsetDirection.sqrMagnitude < 0.000001f)
            {
                return fallbackVertex;
            }

            offsetDirection.Normalize();
            var maxOffset = Mathf.Max(0.0001f, cellSize * 0.49f);
            var clampedOffset = Mathf.Min(Mathf.Abs(outlineOffset), maxOffset);
            var signedOffset = outlineOffset >= 0f ? clampedOffset : -clampedOffset;
            return fallbackVertex + (offsetDirection * signedOffset);
        }

        private static bool IsClockwiseLoop(IReadOnlyList<Vector2Int> loopVertices)
        {
            if (loopVertices == null || loopVertices.Count < 3)
            {
                return true;
            }

            long doubledArea = 0;
            for (var i = 0; i < loopVertices.Count; i++)
            {
                var current = loopVertices[i];
                var next = loopVertices[(i + 1) % loopVertices.Count];
                doubledArea += ((long)current.x * next.y) - ((long)next.x * current.y);
            }

            return doubledArea < 0;
        }

        private static int ClampToSign(int value)
        {
            if (value > 0)
            {
                return 1;
            }

            if (value < 0)
            {
                return -1;
            }

            return 0;
        }

        private static bool IsLexicographicallyBefore(Vector2Int a, Vector2Int b)
        {
            if (a.y != b.y)
            {
                return a.y < b.y;
            }

            return a.x < b.x;
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

        private static bool TryResolveActiveCellBoundsLocal(BlockRootView blockView, out Vector3 minLocal,
            out Vector3 maxLocal)
        {
            minLocal = default;
            maxLocal = default;
            if (blockView == null || !blockView.HasCachedLocalBounds)
            {
                return false;
            }

            minLocal = blockView.CachedLocalBoundsMin;
            maxLocal = blockView.CachedLocalBoundsMax;
            return true;
        }
    }
}
