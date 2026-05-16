using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;
using System.Collections;

namespace Runtime.Controllers
{
    public class BlockSceneBuilder : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private BoardController boardController;

        [SerializeField] private LevelData sourceLevel;
        [SerializeField] private BlockVisualProfile visualProfile;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform blocksRoot;

        [Header("Prefab References")] [SerializeField]
        private GameObject gridCellPrefab;

        [SerializeField] private GameObject borderPrefab;
        [SerializeField] private GameObject backdropPrefab;
        [SerializeField] private GameObject doorPrefab;

        [Header("Board Layout")] [SerializeField]
        private float boardCellGap = 0.08f;

        [SerializeField, Min(0.01f)] private float boardCellDepthInCells = 0.05f;
        [SerializeField] private float boardCellsZOffset = 0.75f;
        [SerializeField] private float boardBackdropPaddingInCells = 0.4f;
        [SerializeField] private float boardBackdropZOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells = 0.48f;
        [SerializeField, Min(0f)] private float edgeFramePaddingInCells = 0.03f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells = 0.14f;
        [SerializeField, Min(0f)] private float doorInsetInCells = 0.02f;
        [SerializeField, Min(0f)] private float doorDepthBiasFromFrame = 0.02f;

        [Header("Block Layout")] [SerializeField, Range(0.75f, 1f)]
        private float blockCellVisualScale = 0.92f;

        [SerializeField, Min(0.01f)] private float blockLayerForwardOffsetFromGrid = 0.24f;

        [SerializeField, Min(0f)] private float blockMoveSmoothingSpeed = 18f;
        [SerializeField, Min(0.05f)] private float doorExitDuration = 0.32f;
        [SerializeField, Min(0.2f)] private float doorExitTravelInCells = 1.15f;
        [SerializeField] private AnimationCurve doorExitMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve doorExitScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField, Range(0f, 1f)] private float doorExitMinScaleMultiplier = 0.05f;

        private readonly Dictionary<Vector2Int, GameObject> _gridCellPoolByCell = new();

        private readonly List<GameObject> _borderPool = new();
        private readonly List<GameObject> _doorPool = new();
        private readonly List<GameObject> _blockRootPool = new();

        private readonly Dictionary<BlockColor, Material> _fallbackDoorMaterialByColor = new();
        private readonly Dictionary<BlockColor, Material> _fallbackBlockMaterialByColor = new();

        private readonly DoorOpeningMap _openingMap = new();
        private readonly Dictionary<int, GameObject> _activeBlockRootById = new();
        private readonly Dictionary<int, Vector3> _blockTargetPositionById = new();
        private readonly Dictionary<int, Coroutine> _blockExitRoutineById = new();
        private readonly List<int> _reachedTargetIds = new();
        private GameObject _backdropObject;
        private bool _baseGridInitialized;
        private Vector2Int _baseGridSize;

        private void OnEnable()
        {
            SubscribeBoardEvents();
        }

        private void OnDisable()
        {
            UnsubscribeBoardEvents();
            StopAllBlockRoutines();
        }

        private void LateUpdate()
        {
            if (_blockTargetPositionById.Count == 0 || blockMoveSmoothingSpeed <= 0f)
            {
                return;
            }

            _reachedTargetIds.Clear();
            var interpolationFactor = 1f - Mathf.Exp(-blockMoveSmoothingSpeed * Time.deltaTime);

            foreach (var pair in _blockTargetPositionById)
            {
                if (!_activeBlockRootById.TryGetValue(pair.Key, out var blockRoot) || blockRoot == null ||
                    !blockRoot.activeSelf)
                {
                    _reachedTargetIds.Add(pair.Key);
                    continue;
                }

                var targetPosition = pair.Value;
                var currentPosition = blockRoot.transform.position;
                if ((currentPosition - targetPosition).sqrMagnitude <= 0.0001f)
                {
                    blockRoot.transform.position = targetPosition;
                    _reachedTargetIds.Add(pair.Key);
                    continue;
                }

                blockRoot.transform.position = Vector3.Lerp(currentPosition, targetPosition, interpolationFactor);
            }

            foreach (var t in _reachedTargetIds)
            {
                _blockTargetPositionById.Remove(t);
            }
        }

        public void BuildForLevel(LevelData levelData)
        {
            sourceLevel = levelData;
            StopAllBlockRoutines();

            if (!_baseGridInitialized)
            {
                _baseGridSize = levelData.gridDimensions;
                _baseGridInitialized = true;
            }

            var pooledGridSize = new Vector2Int(
                Mathf.Max(_baseGridSize.x, levelData.gridDimensions.x),
                Mathf.Max(_baseGridSize.y, levelData.gridDimensions.y));

            EnsureBoardPool(pooledGridSize);
            ApplyBoardVisuals(levelData);
            EnsureBlockPool(levelData.blocks.Count);
            ApplyBlockVisuals(levelData);
            SubscribeBoardEvents();
        }

        [ContextMenu("Build Blocks From Level Data")]
        public void BuildBlocksFromLevelData()
        {
            BuildForLevel(sourceLevel);
        }

        private void EnsureBoardPool(Vector2Int gridSize)
        {
            var width = Mathf.Max(1, gridSize.x);
            var height = Mathf.Max(1, gridSize.y);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_gridCellPoolByCell.ContainsKey(cell))
                    {
                        continue;
                    }

                    var cellObject = CreateGridCellObject(boardRoot, cell);
                    _gridCellPoolByCell.Add(cell, cellObject);
                }
            }

            if (_backdropObject == null)
            {
                _backdropObject = CreateVisualObject(boardRoot, "BoardBackdrop", backdropPrefab);
            }

            while (_borderPool.Count < 4)
            {
                var borderObject = CreateVisualObject(boardRoot, $"Border_{_borderPool.Count}", borderPrefab);
                _borderPool.Add(borderObject);
            }
        }

        private void ApplyBoardVisuals(LevelData levelData)
        {
            var dims = levelData.gridDimensions;
            var boardOrigin = boardController.BoardOrigin;
            var cellSize = boardController.CellSize;
            var tileSize = Mathf.Max(0.01f, cellSize - boardCellGap);
            var tileDepth = Mathf.Max(0.01f, cellSize * boardCellDepthInCells);
            var tileZ = Mathf.Abs(boardCellsZOffset);

            foreach (var pair in _gridCellPoolByCell)
            {
                var cell = pair.Key;
                var isInsideLevel = cell.x < dims.x && cell.y < dims.y;
                pair.Value.SetActive(isInsideLevel);
                if (!isInsideLevel)
                {
                    continue;
                }

                var position = new Vector3(
                    boardOrigin.x + ((cell.x + 0.5f) * cellSize),
                    boardOrigin.y + ((cell.y + 0.5f) * cellSize),
                    tileZ);

                pair.Value.transform.position = position;
                pair.Value.transform.rotation = Quaternion.identity;
                pair.Value.transform.localScale = new Vector3(tileSize, tileSize, tileDepth);
            }

            ApplyBackdrop(dims, boardOrigin, cellSize, tileZ);
            ApplyBorders(dims, boardOrigin, cellSize);
            ApplyDoors(levelData, boardOrigin, cellSize);
        }

        private void ApplyBackdrop(Vector2Int dims, Vector2 boardOrigin, float cellSize, float tileZ)
        {
            var width = dims.x * cellSize;
            var height = dims.y * cellSize;
            var padding = Mathf.Max(0f, boardBackdropPaddingInCells * cellSize);
            var depth = Mathf.Max(0.02f, cellSize * 0.08f);

            _backdropObject.SetActive(true);
            _backdropObject.transform.position = new Vector3(
                boardOrigin.x + (width * 0.5f),
                boardOrigin.y + (height * 0.5f),
                tileZ + Mathf.Abs(boardBackdropZOffset));
            _backdropObject.transform.rotation = Quaternion.identity;
            _backdropObject.transform.localScale = new Vector3(width + (padding * 2f), height + (padding * 2f), depth);
        }

        private void ApplyBorders(Vector2Int dims, Vector2 boardOrigin, float cellSize)
        {
            if (_borderPool.Count < 4)
            {
                return;
            }

            var thickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var padding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var depth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var width = dims.x * cellSize;
            var height = dims.y * cellSize;
            var frameHeight = height + (2f * (thickness + padding));
            var frameWidth = width + (2f * (thickness + padding));

            var leftPosition = new Vector3(
                boardOrigin.x - padding - (thickness * 0.5f),
                boardOrigin.y + (height * 0.5f),
                Mathf.Abs(boardCellsZOffset) - 0.01f);

            var rightPosition = new Vector3(
                boardOrigin.x + width + padding + (thickness * 0.5f),
                boardOrigin.y + (height * 0.5f),
                Mathf.Abs(boardCellsZOffset) - 0.01f);

            var topPosition = new Vector3(
                boardOrigin.x + (width * 0.5f),
                boardOrigin.y + height + padding + (thickness * 0.5f),
                Mathf.Abs(boardCellsZOffset) - 0.01f);

            var bottomPosition = new Vector3(
                boardOrigin.x + (width * 0.5f),
                boardOrigin.y - padding - (thickness * 0.5f),
                Mathf.Abs(boardCellsZOffset) - 0.01f);

            ApplyBorderTransform(_borderPool[0], leftPosition, new Vector3(thickness, frameHeight, depth));
            ApplyBorderTransform(_borderPool[1], rightPosition, new Vector3(thickness, frameHeight, depth));
            ApplyBorderTransform(_borderPool[2], topPosition, new Vector3(frameWidth, thickness, depth));
            ApplyBorderTransform(_borderPool[3], bottomPosition, new Vector3(frameWidth, thickness, depth));
        }

        private void ApplyDoors(LevelData levelData, Vector2 boardOrigin, float cellSize)
        {
            _openingMap.Build(levelData.doors, levelData.gridDimensions);
            var openings = _openingMap.Openings;
            var requiredCount = openings.Count;

            while (_doorPool.Count < requiredCount)
            {
                var doorObject = CreateVisualObject(boardRoot == null ? transform : boardRoot,
                    $"Door_{_doorPool.Count}", doorPrefab);
                _doorPool.Add(doorObject);
            }

            var frameThickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var framePadding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var doorOffset = (0.5f * cellSize) + framePadding + (frameThickness * 0.5f) - (doorInsetInCells * cellSize);
            var doorDepth = frameDepth;
            var doorZ = Mathf.Abs(boardCellsZOffset) - Mathf.Max(0.001f, doorDepthBiasFromFrame);

            for (var i = 0; i < _doorPool.Count; i++)
            {
                var isActive = i < requiredCount;
                var doorObject = _doorPool[i];
                doorObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                var opening = openings[i];
                if (!opening.edgeSide.TryGetNormal(out var normal))
                {
                    doorObject.SetActive(false);
                    continue;
                }

                var openingWidth = Mathf.Max(1, opening.OpeningWidth);
                var span = Mathf.Max(0.01f, (openingWidth * cellSize) - boardCellGap);
                var centerX = (opening.minCell.x + opening.maxCell.x + 1) * 0.5f;
                var centerY = (opening.minCell.y + opening.maxCell.y + 1) * 0.5f;

                var cellCenter = new Vector2(
                    boardOrigin.x + (centerX * cellSize),
                    boardOrigin.y + (centerY * cellSize));

                var position = new Vector3(
                    cellCenter.x + (normal.x * doorOffset),
                    cellCenter.y + (normal.y * doorOffset),
                    doorZ);

                var isHorizontal = opening.edgeSide.IsHorizontal();
                var scale = isHorizontal
                    ? new Vector3(span, frameThickness, doorDepth)
                    : new Vector3(frameThickness, span, doorDepth);

                doorObject.transform.position = position;
                doorObject.transform.rotation = Quaternion.identity;
                doorObject.transform.localScale = scale;

                var renderer = doorObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = GetDoorMaterial(opening.colorType);
                }
            }
        }

        private void EnsureBlockPool(int requiredBlockCount)
        {
            var blockParent = blocksRoot == null ? transform : blocksRoot;

            while (_blockRootPool.Count < requiredBlockCount)
            {
                var rootObject = new GameObject($"BLK_{_blockRootPool.Count}");
                rootObject.transform.SetParent(blockParent, false);
                rootObject.transform.localRotation = Quaternion.identity;
                _blockRootPool.Add(rootObject);
            }
        }

        private void ApplyBlockVisuals(LevelData levelData)
        {
            _activeBlockRootById.Clear();

            for (var i = 0; i < _blockRootPool.Count; i++)
            {
                var isActive = i < levelData.blocks.Count;
                var rootObject = _blockRootPool[i];
                StopBlockMovement(i);
                StopBlockExit(i);
                rootObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                var blockData = levelData.blocks[i];

                ApplyBlockCells(rootObject.transform, blockData);
                rootObject.transform.position = ToWorldPosition(blockData.position);
                rootObject.transform.localScale = Vector3.one;
                _activeBlockRootById[i] = rootObject;
                _blockTargetPositionById[i] = rootObject.transform.position;
            }
        }

        private void SubscribeBoardEvents()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockMoved += HandleBlockMoved;

            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockCleared += HandleBlockCleared;
        }

        private void UnsubscribeBoardEvents()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
        }

        private void HandleBlockMoved(int blockId, Vector2Int newPosition)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockRoot) || blockRoot == null)
            {
                return;
            }

            StopBlockExit(blockId);

            var targetPosition = ToWorldPosition(newPosition);
            if (blockMoveSmoothingSpeed <= 0f)
            {
                StopBlockMovement(blockId);
                blockRoot.transform.position = targetPosition;
                return;
            }

            _blockTargetPositionById[blockId] = targetPosition;
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockRoot) || blockRoot == null)
            {
                return;
            }

            StopBlockMovement(blockId);
            StopBlockExit(blockId);
            blockRoot.transform.position = ToWorldPosition(clearedPosition);

            if (exitDirection == Vector2Int.zero)
            {
                blockRoot.SetActive(false);
                _activeBlockRootById.Remove(blockId);
                return;
            }

            _blockExitRoutineById[blockId] =
                StartCoroutine(ExitBlockRoutine(blockId, blockRoot.transform, exitDirection));
        }

        private IEnumerator ExitBlockRoutine(int blockId, Transform blockTransform, Vector2Int exitDirection)
        {
            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var distance = Mathf.Max(0.2f, doorExitTravelInCells) * boardController.CellSize;
            var targetPosition = startPosition + new Vector3(exitDirection.x, exitDirection.y, 0f) * distance;
            var duration = Mathf.Max(0.05f, doorExitDuration);
            var elapsed = 0f;
            var minScale = startScale * Mathf.Clamp01(doorExitMinScaleMultiplier);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var moveLerp = doorExitMoveCurve != null
                    ? Mathf.Clamp01(doorExitMoveCurve.Evaluate(normalized))
                    : normalized;
                var scaleLerp = doorExitScaleCurve != null
                    ? Mathf.Clamp01(doorExitScaleCurve.Evaluate(normalized))
                    : 1f - normalized;

                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, moveLerp);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, startScale, scaleLerp);
                yield return null;
            }

            blockTransform.position = targetPosition;
            blockTransform.localScale = startScale;
            blockTransform.gameObject.SetActive(false);

            _activeBlockRootById.Remove(blockId);
            _blockExitRoutineById.Remove(blockId);
        }

        private void StopBlockMovement(int blockId)
        {
            _blockTargetPositionById.Remove(blockId);
        }

        private void StopBlockExit(int blockId)
        {
            if (!_blockExitRoutineById.TryGetValue(blockId, out var routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            _blockExitRoutineById.Remove(blockId);
        }

        private void StopAllBlockRoutines()
        {
            foreach (var pair in _blockExitRoutineById)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockTargetPositionById.Clear();
            _blockExitRoutineById.Clear();
        }

        private Vector3 ToWorldPosition(Vector2Int gridPosition)
        {
            var cellSize = boardController.CellSize;
            var boardOrigin = boardController.BoardOrigin;
            var gridZ = Mathf.Abs(boardCellsZOffset);
            var blockZ = gridZ - Mathf.Max(0.01f, blockLayerForwardOffsetFromGrid);
            return new Vector3(
                boardOrigin.x + (gridPosition.x * cellSize),
                boardOrigin.y + (gridPosition.y * cellSize),
                blockZ);
        }

        private void ApplyBlockCells(Transform root, BlockData blockData)
        {
            var localCells = blockData.GetLocalCells();
            var cellSize = boardController.CellSize;
            var scaledCellSize = Mathf.Max(0.01f, cellSize * blockCellVisualScale);

            EnsureBlockCellCount(root, localCells.Length);

            for (var i = 0; i < root.childCount; i++)
            {
                var cellObject = root.GetChild(i).gameObject;
                var isActive = i < localCells.Length;
                cellObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                var localCell = localCells[i];
                cellObject.name = $"Cell_{localCell.x}_{localCell.y}";
                cellObject.transform.localPosition = new Vector3(
                    (localCell.x + 0.5f) * cellSize,
                    (localCell.y + 0.5f) * cellSize,
                    0f);
                cellObject.transform.localRotation = Quaternion.identity;
                cellObject.transform.localScale = Vector3.one * scaledCellSize;

                var renderer = cellObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var material = visualProfile != null ? visualProfile.GetMaterial(blockData.colorType) : null;
                    renderer.sharedMaterial = material ?? GetBlockFallbackMaterial(blockData.colorType);
                }
            }
        }

        private void EnsureBlockCellCount(Transform blockRoot, int requiredCellCount)
        {
            while (blockRoot.childCount < requiredCellCount)
            {
                var cellObject = CreateBlockCellObject(blockRoot);
                cellObject.SetActive(true);
            }
        }

        private GameObject CreateBlockCellObject(Transform parent)
        {
            var prefab = visualProfile != null ? visualProfile.defaultBlockPrefab : null;
            GameObject cellObject;

            if (prefab != null)
            {
                cellObject = Instantiate(prefab, parent);
            }
            else
            {
                cellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cellObject.transform.SetParent(parent, false);
            }

            RemoveCollider(cellObject);
            return cellObject;
        }

        private GameObject CreateGridCellObject(Transform parent, Vector2Int cell)
        {
            var cellObject = CreateVisualObject(parent, $"GridCell_{cell.x}_{cell.y}", gridCellPrefab);
            RemoveCollider(cellObject);
            return cellObject;
        }

        private GameObject CreateVisualObject(Transform parent, string objectName, GameObject prefab)
        {
            var visualObject = Instantiate(prefab, parent);
            visualObject.name = objectName;

            visualObject.transform.localRotation = Quaternion.identity;
            RemoveCollider(visualObject);
            return visualObject;
        }

        private void ApplyBorderTransform(GameObject borderObject, Vector3 position, Vector3 scale)
        {
            borderObject.SetActive(true);
            borderObject.transform.position = position;
            borderObject.transform.rotation = Quaternion.identity;
            borderObject.transform.localScale = scale;
        }

        private Material GetDoorMaterial(BlockColor colorType)
        {
            if (_fallbackDoorMaterialByColor.TryGetValue(colorType, out var existingMaterial) &&
                existingMaterial != null)
            {
                return existingMaterial;
            }

            var doorColor = BlockColorUtility.GetColor(colorType);

            var material = CreateColorMaterial(doorColor, $"MAT_Runtime_Door_{colorType}");
            _fallbackDoorMaterialByColor[colorType] = material;
            return material;
        }

        private Material GetBlockFallbackMaterial(BlockColor colorType)
        {
            if (_fallbackBlockMaterialByColor.TryGetValue(colorType, out var existingMaterial) &&
                existingMaterial != null)
            {
                return existingMaterial;
            }

            var material = CreateColorMaterial(BlockColorUtility.GetColor(colorType), $"MAT_Runtime_Block_{colorType}");
            _fallbackBlockMaterialByColor[colorType] = material;
            return material;
        }

        private static Material CreateColorMaterial(Color color, string materialName)
        {
            var shader = Shader.Find("Unlit/Color");
            if (!shader)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            return material;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(collider);
                return;
            }
#endif
            Destroy(collider);
        }

        private void OnDestroy()
        {
            _fallbackDoorMaterialByColor.Clear();
            _fallbackBlockMaterialByColor.Clear();
        }
    }
}