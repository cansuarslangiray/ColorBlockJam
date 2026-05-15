using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    public class BlockSceneBuilder : MonoBehaviour
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private LevelData sourceLevel;
        [SerializeField] private BlockVisualProfile visualProfile;
        [SerializeField] private Transform blocksRoot;
        [SerializeField] private bool clearExistingBeforeBuild= true;

        [Header("Board Cells Visual")]
        [SerializeField] private bool buildBoardCells= true;
        [SerializeField] private float boardCellsZOffset= 0.75f;
        [SerializeField] private float boardCellGap= 0.08f;
        [SerializeField, Min(0.01f)] private float boardCellDepthInCells= 0.05f;
        [SerializeField] private Color openCellColor= new Color(0.74f, 0.74f, 0.79f, 1f);
        [SerializeField] private Color blockedCellColor= new Color(0.37f, 0.37f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float doorColorBlendWithOpenCell= 0.75f;
        [SerializeField] private bool showDoorColorInsideGrid;
        [SerializeField] private bool buildBoardBackdrop= true;
        [SerializeField] private float boardBackdropPaddingInCells= 0.4f;
        [SerializeField] private float boardBackdropZOffset= 0.95f;
        [SerializeField] private Color boardBackdropColor= new Color(0.2f, 0.24f, 0.32f, 1f);

        [Header("Board Frame")]
        [SerializeField] private bool buildBoardFrame= true;
        [SerializeField, Min(0.01f)] private float edgeFrameThicknessInCells= 0.48f;
        [SerializeField, Min(0f)] private float edgeFramePaddingInCells= 0.03f;
        [SerializeField, Min(0.01f)] private float edgeFrameDepthInCells= 0.14f;
        [SerializeField] private Color edgeFrameColor= new Color(0.17f, 0.2f, 0.29f, 1f);
        [SerializeField] private float edgeFrameZOffset= 0.34f;

        [Header("Door Edge Visual")]
        [SerializeField] private bool buildDoorEdges= true;
        [SerializeField, Min(0f)] private float doorEdgeInsetInCells= 0.02f;
        [SerializeField, Range(0f, 1f)] private float doorEdgeColorBlend= 0.98f;
        [SerializeField] private float doorEdgeZOffset= 0.22f;
        [SerializeField, Min(0f)] private float doorEdgeDepthBiasFromFrame= 0.02f;
        

        [Header("Block Visual")]
        [SerializeField, Range(0.75f, 1f)] private float blockCellVisualScale= 0.92f;
        [SerializeField, Range(0.6f, 1f)] private float blockColliderScale= 0.9f;
        [SerializeField, Min(0f)] private float blockMoveSmoothingSpeed= 18f;

        private Material _openCellMaterial;
        private Material _blockedCellMaterial;
        private Material _boardBackdropMaterial;
        private Material _edgeFrameMaterial;
        private readonly Dictionary<BlockColor, Material> _doorCellMaterialByColor = new Dictionary<BlockColor, Material>();
        private readonly Dictionary<BlockColor, Material> _doorEdgeMaterialByColor = new Dictionary<BlockColor, Material>();
        private readonly Dictionary<BlockColor, Material> _fallbackBlockMaterialByColor = new Dictionary<BlockColor, Material>();

        public void BuildForLevel(LevelData levelData)
        {
            if (levelData != null)
            {
                sourceLevel = levelData;
            }

            BuildBlocksFromLevelData();
        }

        [ContextMenu("Build Blocks From Level Data")]
        public void BuildBlocksFromLevelData()
        {
            if (boardController == null || sourceLevel == null)
            {
                Debug.LogWarning("BlockSceneBuilder: BoardController ve SourceLevel atanmali.");
                return;
            }

            var parent = blocksRoot == null ? transform : blocksRoot;
            if (clearExistingBeforeBuild)
            {
                ClearChildren(parent);
            }

            if (buildBoardCells)
            {
                BuildBoardCells(parent);
            }

            var views = new List<BlockView>(sourceLevel.blocks.Count);

            for (var i = 0; i < sourceLevel.blocks.Count; i++)
            {
                var blockData = sourceLevel.blocks[i];
                var rootObject = new GameObject($"BLK_{i}_{blockData.colorType}");
                rootObject.transform.SetParent(parent, false);

                var blockView = rootObject.AddComponent<BlockView>();
                blockView.SetBlockId(i);
                blockView.ConfigureMovementSmoothing(blockMoveSmoothingSpeed);

                var input = rootObject.AddComponent<BlockDragInput>();
                input.Configure(boardController, blockView);

                BuildShapeVisual(rootObject.transform, blockData);
                EnsureRootCollider(rootObject, blockData.GetSize(), boardController.CellSize);
                ApplyTransform(rootObject.transform, blockData);
                views.Add(blockView);
            }

            boardController.SetBlockViews(views.ToArray());

            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(boardController);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void BuildBoardCells(Transform parent)
        {
            var blockedCells = sourceLevel.blockedCells ?? new List<Vector2Int>();
            var blockedCellSet = new HashSet<Vector2Int>(blockedCells);
            var doorColorsByCell = BuildDoorColorMap();

            var boardCellsRoot = new GameObject("BoardCellsRoot").transform;
            boardCellsRoot.SetParent(parent, false);

            var cellSize = boardController.CellSize;
            var boardOrigin = boardController.BoardOrigin;
            var tileScale = Mathf.Max(0.01f, cellSize - boardCellGap);
            var tileDepth = Mathf.Max(0.01f, cellSize * boardCellDepthInCells);
            var cellsZ = Mathf.Abs(boardCellsZOffset);

            EnsureBoardMaterials();

            if (buildBoardBackdrop)
            {
                BuildBoardBackdrop(boardCellsRoot, boardOrigin, cellSize, cellsZ);
            }

            if (buildBoardFrame)
            {
                BuildBoardFrame(boardCellsRoot, boardOrigin, cellSize);
            }

            if (buildDoorEdges)
            {
                BuildDoorEdges(boardCellsRoot, boardOrigin, cellSize, doorColorsByCell);
            }

            for (var y = 0; y < sourceLevel.gridDimensions.y; y++)
            {
                for (var x = 0; x < sourceLevel.gridDimensions.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var isBlocked = blockedCellSet.Contains(cell);

                    var tileObject = CreateBoardPrimitive(
                        boardCellsRoot,
                        isBlocked ? $"Cell_{x}_{y}_Blocked" : $"Cell_{x}_{y}_Open",
                        new Vector3(tileScale, tileScale, tileDepth),
                        new Vector3(
                            boardOrigin.x + ((x + 0.5f) * cellSize),
                            boardOrigin.y + ((y + 0.5f) * cellSize),
                            cellsZ));

                    var tileRenderer = tileObject.GetComponent<Renderer>();
                    if (tileRenderer == null)
                    {
                        continue;
                    }

                    if (isBlocked)
                    {
                        tileRenderer.sharedMaterial = _blockedCellMaterial;
                        continue;
                    }

                    if (showDoorColorInsideGrid && doorColorsByCell.TryGetValue(cell, out var doorColor))
                    {
                        tileRenderer.sharedMaterial = GetDoorCellMaterial(doorColor);
                        continue;
                    }

                    tileRenderer.sharedMaterial = _openCellMaterial;
                }
            }
        }

        private Dictionary<Vector2Int, BlockColor> BuildDoorColorMap()
        {
            var colorMap = new Dictionary<Vector2Int, BlockColor>();
            var doors = sourceLevel.doors;
            if (doors == null)
            {
                return colorMap;
            }

            var doorCells = new List<Vector2Int>(8);
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (!DoorOpeningMap.TryCollectDoorCells(door, sourceLevel.gridDimensions, doorCells))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < doorCells.Count; cellIndex++)
                {
                    colorMap[doorCells[cellIndex]] = door.colorType;
                }
            }

            return colorMap;
        }

        private void BuildBoardBackdrop(Transform parent, Vector2 boardOrigin, float cellSize, float cellsZ)
        {
            EnsureBoardMaterials();

            var gridWidth = sourceLevel.gridDimensions.x * cellSize;
            var gridHeight = sourceLevel.gridDimensions.y * cellSize;
            var padding = Mathf.Max(0f, boardBackdropPaddingInCells * cellSize);
            var backdropZ = cellsZ + Mathf.Abs(boardBackdropZOffset);

            var backdropObject = CreateBoardPrimitive(
                parent,
                "BoardBackdrop",
                new Vector3(
                    gridWidth + (padding * 2f),
                    gridHeight + (padding * 2f),
                    Mathf.Max(0.02f, cellSize * 0.08f)),
                new Vector3(
                    boardOrigin.x + (gridWidth * 0.5f),
                    boardOrigin.y + (gridHeight * 0.5f),
                    backdropZ));

            var backdropRenderer = backdropObject.GetComponent<Renderer>();
            if (backdropRenderer != null)
            {
                backdropRenderer.sharedMaterial = _boardBackdropMaterial;
            }
        }

        private void BuildBoardFrame(Transform parent, Vector2 boardOrigin, float cellSize)
        {
            EnsureBoardMaterials();

            var frameThickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var framePadding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var frameZ = Mathf.Abs(edgeFrameZOffset);

            var gridWidth = sourceLevel.gridDimensions.x * cellSize;
            var gridHeight = sourceLevel.gridDimensions.y * cellSize;
            var frameHeight = gridHeight + (2f * (frameThickness + framePadding));
            var frameWidth = gridWidth + (2f * (frameThickness + framePadding));

            var leftFrame = CreateBoardPrimitive(
                parent,
                "Frame_Left",
                new Vector3(frameThickness, frameHeight, frameDepth),
                new Vector3(
                    boardOrigin.x - framePadding - (frameThickness * 0.5f),
                    boardOrigin.y + (gridHeight * 0.5f),
                    frameZ));

            var rightFrame = CreateBoardPrimitive(
                parent,
                "Frame_Right",
                new Vector3(frameThickness, frameHeight, frameDepth),
                new Vector3(
                    boardOrigin.x + gridWidth + framePadding + (frameThickness * 0.5f),
                    boardOrigin.y + (gridHeight * 0.5f),
                    frameZ));

            var topFrame = CreateBoardPrimitive(
                parent,
                "Frame_Top",
                new Vector3(frameWidth, frameThickness, frameDepth),
                new Vector3(
                    boardOrigin.x + (gridWidth * 0.5f),
                    boardOrigin.y + gridHeight + framePadding + (frameThickness * 0.5f),
                    frameZ));

            var bottomFrame = CreateBoardPrimitive(
                parent,
                "Frame_Bottom",
                new Vector3(frameWidth, frameThickness, frameDepth),
                new Vector3(
                    boardOrigin.x + (gridWidth * 0.5f),
                    boardOrigin.y - framePadding - (frameThickness * 0.5f),
                    frameZ));

            ApplyMaterial(leftFrame, _edgeFrameMaterial);
            ApplyMaterial(rightFrame, _edgeFrameMaterial);
            ApplyMaterial(topFrame, _edgeFrameMaterial);
            ApplyMaterial(bottomFrame, _edgeFrameMaterial);
        }

        private void BuildDoorEdges(
            Transform parent,
            Vector2 boardOrigin,
            float cellSize,
            Dictionary<Vector2Int, BlockColor> doorColorsByCell)
        {
            if (doorColorsByCell == null || doorColorsByCell.Count == 0)
            {
                return;
            }

            EnsureBoardMaterials();
            var doorThickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var doorDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var edgeOffsetInCells = 0.5f + edgeFramePaddingInCells + (edgeFrameThicknessInCells * 0.5f) - doorEdgeInsetInCells;
            var desiredDoorZ = Mathf.Abs(edgeFrameZOffset) - Mathf.Max(0f, doorEdgeDepthBiasFromFrame);
            var doorZ = Mathf.Max(0.01f, Mathf.Min(Mathf.Abs(doorEdgeZOffset), desiredDoorZ));

            var openingMap = new DoorOpeningMap();
            openingMap.Build(sourceLevel.doors, sourceLevel.gridDimensions);
            var openings = openingMap.Openings;

            for (int i = 0; i < openings.Count; i++)
            {
                var opening = openings[i];
                if (!TryGetDoorNormalFromSide(opening.edgeSide, out Vector2Int normal))
                {
                    continue;
                }

                var openingWidthInCells = Mathf.Max(1, opening.OpeningWidth);
                var alongAxisSpan = Mathf.Max(0.01f, (openingWidthInCells * cellSize) - boardCellGap);
                var centerCellX = (opening.minCell.x + opening.maxCell.x + 1) * 0.5f;
                var centerCellY = (opening.minCell.y + opening.maxCell.y + 1) * 0.5f;

                var cellCenter = new Vector2(
                    boardOrigin.x + (centerCellX * cellSize),
                    boardOrigin.y + (centerCellY * cellSize));

                var doorCenter = new Vector3(
                    cellCenter.x + (normal.x * edgeOffsetInCells * cellSize),
                    cellCenter.y + (normal.y * edgeOffsetInCells * cellSize),
                    doorZ);

                var isHorizontalDoorStrip = opening.edgeSide == 2 || opening.edgeSide == 3;
                var fillSize = isHorizontalDoorStrip
                    ? new Vector3(alongAxisSpan, doorThickness, doorDepth)
                    : new Vector3(doorThickness, alongAxisSpan, doorDepth);

                var fillObject = CreateBoardPrimitive(
                    parent,
                    $"DoorFill_{opening.edgeSide}_{i}_{opening.colorType}",
                    fillSize,
                    doorCenter);

                ApplyMaterial(fillObject, GetDoorEdgeMaterial(opening.colorType));
            }
        }

        private void EnsureBoardMaterials()
        {
            _openCellMaterial = GetOrCreateMaterial(_openCellMaterial, openCellColor, "MAT_Runtime_OpenCell");
            _blockedCellMaterial = GetOrCreateMaterial(_blockedCellMaterial, blockedCellColor, "MAT_Runtime_BlockedCell");
            _boardBackdropMaterial = GetOrCreateMaterial(_boardBackdropMaterial, boardBackdropColor, "MAT_Runtime_BoardBackdrop");
            _edgeFrameMaterial = GetOrCreateMaterial(_edgeFrameMaterial, edgeFrameColor, "MAT_Runtime_EdgeFrame");
        }

        private Material GetDoorCellMaterial(BlockColor color)
        {
            if (_doorCellMaterialByColor.TryGetValue(color, out var existing) && existing != null)
            {
                return existing;
            }

            var mixedColor = Color.Lerp(openCellColor, BlockColorUtility.GetColor(color), Mathf.Clamp01(doorColorBlendWithOpenCell));
            var doorMaterial = CreateColorMaterial(mixedColor, $"MAT_Runtime_DoorCell_{color}");
            _doorCellMaterialByColor[color] = doorMaterial;
            return doorMaterial;
        }

        private Material GetDoorEdgeMaterial(BlockColor color)
        {
            if (_doorEdgeMaterialByColor.TryGetValue(color, out var existing) && existing != null)
            {
                return existing;
            }

            var mixedColor = Color.Lerp(edgeFrameColor, BlockColorUtility.GetColor(color), Mathf.Clamp01(doorEdgeColorBlend));
            var doorMaterial = CreateColorMaterial(mixedColor, $"MAT_Runtime_DoorEdge_{color}");
            _doorEdgeMaterialByColor[color] = doorMaterial;
            return doorMaterial;
        }

        private Material GetOrCreateMaterial(Material current, Color color, string materialName)
        {
            if (current == null)
            {
                return CreateColorMaterial(color, materialName);
            }

            current.color = color;
            return current;
        }

        private Material CreateColorMaterial(Color color, string materialName)
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader)
            {
                name = materialName
            };
            material.color = color;
            material.enableInstancing = true;
            return material;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_openCellMaterial);
            DestroyMaterial(_blockedCellMaterial);
            DestroyMaterial(_boardBackdropMaterial);
            DestroyMaterial(_edgeFrameMaterial);

            foreach (var pair in _doorCellMaterialByColor)
            {
                DestroyMaterial(pair.Value);
            }

            foreach (var pair in _doorEdgeMaterialByColor)
            {
                DestroyMaterial(pair.Value);
            }

            foreach (var pair in _fallbackBlockMaterialByColor)
            {
                DestroyMaterial(pair.Value);
            }

            _doorCellMaterialByColor.Clear();
            _doorEdgeMaterialByColor.Clear();
            _fallbackBlockMaterialByColor.Clear();
        }

        private void BuildShapeVisual(Transform root, BlockData blockData)
        {
            var localCells = blockData.GetLocalCells();
            var cellSize = boardController.CellSize;
            var scaledCellSize = Mathf.Max(0.01f, cellSize * blockCellVisualScale);

            foreach (var cell in localCells)
            {
                var cellObject = CreateVisualCellObject(root);
                cellObject.name = $"Cell_{cell.x}_{cell.y}";
                cellObject.transform.localPosition = new Vector3(
                    (cell.x + 0.5f) * cellSize,
                    (cell.y + 0.5f) * cellSize,
                    0f);
                cellObject.transform.localScale = Vector3.one * scaledCellSize;

                ApplyVisuals(cellObject, blockData);
            }
        }

        private GameObject CreateVisualCellObject(Transform parent)
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

            RemoveBehaviourIfExists<BlockView>(cellObject);
            RemoveBehaviourIfExists<BlockDragInput>(cellObject);

            var childCollider = cellObject.GetComponent<Collider>();
            if (childCollider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(childCollider);
                }
                else
#endif
                {
                    Destroy(childCollider);
                }
            }

            return cellObject;
        }

        private void ApplyVisuals(GameObject blockCellObject, BlockData blockData)
        {
            var renderer = blockCellObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var material = visualProfile != null ? visualProfile.GetMaterial(blockData.colorType) : null;
            renderer.sharedMaterial = material ?? GetFallbackBlockMaterial(blockData.colorType);
        }

        private Material GetFallbackBlockMaterial(BlockColor color)
        {
            if (_fallbackBlockMaterialByColor.TryGetValue(color, out var existing) && existing != null)
            {
                return existing;
            }

            var fallback = CreateColorMaterial(BlockColorUtility.GetColor(color), $"MAT_Runtime_Block_{color}");
            _fallbackBlockMaterialByColor[color] = fallback;
            return fallback;
        }

        private void EnsureRootCollider(GameObject rootObject, Vector2Int size, float cellSize)
        {
            var boxCollider = rootObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = rootObject.AddComponent<BoxCollider>();
            }

            var scaledWidth = size.x * cellSize * blockColliderScale;
            var scaledHeight = size.y * cellSize * blockColliderScale;
            boxCollider.center = new Vector3(size.x * cellSize * 0.5f, size.y * cellSize * 0.5f, 0f);
            boxCollider.size = new Vector3(scaledWidth, scaledHeight, cellSize);
        }

        private void ApplyTransform(Transform target, BlockData blockData)
        {
            var view = target.GetComponent<BlockView>();
            if (view != null)
            {
                view.SetGridPosition(blockData.position, boardController.CellSize, boardController.BoardOrigin);
            }
        }

        private GameObject CreateBoardPrimitive(Transform parent, string objectName, Vector3 scale, Vector3 position)
        {
            var primitiveObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitiveObject.name = objectName;
            primitiveObject.transform.SetParent(parent, false);
            primitiveObject.transform.rotation = Quaternion.identity;
            primitiveObject.transform.localScale = scale;
            primitiveObject.transform.position = position;

            var collider = primitiveObject.GetComponent<Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(collider);
                }
                else
#endif
                {
                    Destroy(collider);
                }
            }

            return primitiveObject;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static bool TryGetDoorNormalFromSide(int edgeSide, out Vector2Int normal)
        {
            if (edgeSide == 0)
            {
                normal = Vector2Int.left;
                return true;
            }

            if (edgeSide == 1)
            {
                normal = Vector2Int.right;
                return true;
            }

            if (edgeSide == 2)
            {
                normal = Vector2Int.down;
                return true;
            }

            if (edgeSide == 3)
            {
                normal = Vector2Int.up;
                return true;
            }

            normal = Vector2Int.zero;
            return false;
        }

        private static void DestroyMaterial(Object materialObject)
        {
            if (materialObject == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }
#endif
            Destroy(materialObject);
        }

        private static void DestroyComponent(Object componentObject)
        {
            if (componentObject == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(componentObject);
                return;
            }
#endif
            Destroy(componentObject);
        }

        private void RemoveBehaviourIfExists<T>(GameObject target) where T : Behaviour
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                return;
            }

            DestroyComponent(component);
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Destroy(child.gameObject);
            }
        }
    }
}
