using Runtime.Domain.Enums;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardInputController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, ICancelHandler, IEndDragHandler
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private BoxCollider inputAreaCollider;
        [SerializeField] private Camera inputCamera;
        [SerializeField, Range(0f, 1f)] private float dragAxisActivationRatio = 0.1f;
        [SerializeField, Min(0.01f)] private float autoFitColliderThickness = 0.5f;

        private int _activeBlockId = -1;
        private Vector2 _dragStartWorldPoint;
        private Vector2Int _activeDragAxis = Vector2Int.zero;
        private int _dragAxisCellOffset;
        private Vector2Int _freeDragStepOffset;
        private bool _activeBlockIsFree;
        private Plane _boardPlane = new (Vector3.forward, Vector3.zero);
        private Camera _activePointerCamera;

        private bool IsBoardInitialized => boardController != null && boardController.IsInitialized;
        private bool IsBoardReadyForInput => IsBoardInitialized && StateManager.Instance.CurrentState == GameState.Playing;

        private void Awake()
        {
            RefreshBoardPlane();
            TryFitInputArea();
        }

        private void OnValidate()
        {
            RefreshBoardPlane();
            TryFitInputArea();
        }

        private void OnDisable()
        {
            ResetDragState();
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _activePointerCamera = ResolvePointerCamera(eventData);

            if (_activePointerCamera == null)
            {
                return;
            }

            HandlePointerDown(eventData.position, _activePointerCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            HandlePointerDrag(eventData.position, _activePointerCamera);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            HandlePointerUp();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            HandlePointerUp();
        }

        public void OnCancel(BaseEventData eventData)
        {
            HandlePointerUp();
        }

        private void HandlePointerDown(Vector2 pointerPosition, Camera cameraToUse)
        {
            ResetDragState();
            if (!IsBoardReadyForInput)
            {
                return;
            }

            TryFitInputArea();

            if (!TryGetActiveBlock(pointerPosition, cameraToUse, out var blockId, out var worldPos, out var movementConstraint))
            {
                return;
            }

            _activeBlockId = blockId;
            _dragStartWorldPoint = worldPos;
            _activeBlockIsFree = movementConstraint == BlockMovementConstraint.Free;
        }

        private void HandlePointerDrag(Vector2 pointerPosition, Camera cameraToUse)
        {
            if (_activeBlockId < 0 || !IsBoardReadyForInput)
            {
                return;
            }

            if (!TryResolveBoardPoint(pointerPosition, cameraToUse, out var worldPos))
            {
                return;
            }

            if (_activeBlockIsFree)
            {
                HandleFreeDrag(worldPos);
                return;
            }

            var dragVector = worldPos - _dragStartWorldPoint;
            if (_activeDragAxis == Vector2Int.zero)
            {
                _activeDragAxis = ResolveDragAxis(dragVector);
                if (_activeDragAxis == Vector2Int.zero)
                {
                    return;
                }
            }

            var dragAxisDistance = _activeDragAxis.x != 0 ? dragVector.x : dragVector.y;
            var targetStepOffset = ConvertAxisDistanceToCellSteps(dragAxisDistance, boardController.CellSize);
            var requestStepDelta = targetStepOffset - _dragAxisCellOffset;
            if (requestStepDelta == 0)
            {
                return;
            }

            var requestDirection = ResolveDirectionFromAxisAndSign(_activeDragAxis, requestStepDelta > 0);
            var requestCount = Mathf.Abs(requestStepDelta);
            var movedCount = 0;

            for (var i = 0; i < requestCount; i++)
            {
                if (!boardController.TryMoveBlockByStep(_activeBlockId, requestDirection))
                {
                    break;
                }

                movedCount++;
            }

            if (movedCount <= 0)
            {
                return;
            }

            _dragAxisCellOffset += requestStepDelta > 0 ? movedCount : -movedCount;
        }

        private void HandleFreeDrag(Vector2 worldPos)
        {
            if (!IsBoardReadyForInput)
            {
                return;
            }

            var cellSize = boardController.CellSize;
            if (cellSize <= 0f)
            {
                return;
            }

            var dragVector = worldPos - _dragStartWorldPoint;
            var targetStepOffset = new Vector2Int(
                ConvertAxisDistanceToCellSteps(dragVector.x, cellSize),
                ConvertAxisDistanceToCellSteps(dragVector.y, cellSize));

            var requestDelta = targetStepOffset - _freeDragStepOffset;
            if (requestDelta == Vector2Int.zero)
            {
                return;
            }

            while (requestDelta != Vector2Int.zero)
            {
                var moveOnX = requestDelta.x != 0 &&
                               (requestDelta.y == 0 || Mathf.Abs(requestDelta.x) >= Mathf.Abs(requestDelta.y));

                if (moveOnX)
                {
                    if (!boardController.TryMoveBlockByStep(_activeBlockId, requestDelta.x > 0 ? Direction.Right : Direction.Left))
                    {
                        requestDelta.x = 0;
                        continue;
                    }

                    var stepSign = requestDelta.x > 0 ? 1 : -1;
                    requestDelta.x -= stepSign;
                    _freeDragStepOffset.x += stepSign;
                }
                else
                {
                    if (!boardController.TryMoveBlockByStep(_activeBlockId, requestDelta.y > 0 ? Direction.Up : Direction.Down))
                    {
                        requestDelta.y = 0;
                        continue;
                    }

                    var stepSign = requestDelta.y > 0 ? 1 : -1;
                    requestDelta.y -= stepSign;
                    _freeDragStepOffset.y += stepSign;
                }
            }
        }

        private void HandlePointerUp()
        {
            _activePointerCamera = null;
            ResetDragState();
        }

        private bool TryResolveBoardPoint(Vector2 screenPosition, Camera pointerCamera, out Vector2 boardWorldPoint)
        {
            boardWorldPoint = default;
            if (!pointerCamera)
            {
                return false;
            }

            var ray = pointerCamera.ScreenPointToRay(screenPosition);
            if (inputAreaCollider.Raycast(ray, out var colliderHitInfo, float.MaxValue))
            {
                var colliderHit = ray.GetPoint(colliderHitInfo.distance);
                if (IsPointInsideBoard(colliderHit))
                {
                    boardWorldPoint = new Vector2(colliderHit.x, colliderHit.y);
                    return true;
                }
            }

            if (!_boardPlane.Raycast(ray, out var hitDistance))
            {
                return false;
            }

            var hitPoint = ray.GetPoint(hitDistance);
            if (!IsPointInsideBoard(hitPoint))
            {
                return false;
            }

            boardWorldPoint = new Vector2(hitPoint.x, hitPoint.y);
            return true;
        }

        private bool TryGetActiveBlock(Vector2 pointerPosition, Camera pointerCamera, out int blockId, out Vector2 boardWorldPoint, out BlockMovementConstraint movementConstraint)
        {
            blockId = -1;
            boardWorldPoint = default;
            movementConstraint = default;

            if (!IsBoardReadyForInput || !TryResolveBoardPoint(pointerPosition, pointerCamera, out boardWorldPoint))
            {
                return false;
            }

            if (!boardController.TryWorldToCell(boardWorldPoint, out var touchedCell) || !boardController.TryGetBlockAtCell(touchedCell, out blockId))
            {
                return false;
            }

            return boardController.TryGetBlockMovementConstraint(blockId, out movementConstraint);
        }

        private void RefreshBoardPlane()
        {
            var planeZ = inputAreaCollider.transform.position.z;
            _boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        }

        private void TryFitInputArea()
        {
            if (!IsBoardInitialized)
            {
                return;
            }

            var gridDimensions = boardController.GridDimensions;
            var cellSize = boardController.CellSize;
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0 || cellSize <= 0f)
            {
                return;
            }

            var width = gridDimensions.x * cellSize;
            var height = gridDimensions.y * cellSize;
            var boardCenterWorld = new Vector3(boardController.BoardOrigin.x + (width * 0.5f), boardController.BoardOrigin.y + (height * 0.5f), inputAreaCollider.transform.position.z);

            var scale = inputAreaCollider.transform.lossyScale;
            var sizeX = width / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
            var sizeY = height / Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            var sizeZ = autoFitColliderThickness / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
            inputAreaCollider.size = new Vector3(sizeX, sizeY, sizeZ);
            inputAreaCollider.center = inputAreaCollider.transform.InverseTransformPoint(boardCenterWorld);

            RefreshBoardPlane();
        }

        private void ResetDragState()
        {
            _activeBlockId = -1;
            _dragStartWorldPoint = Vector2.zero;
            _activeDragAxis = Vector2Int.zero;
            _dragAxisCellOffset = 0;
            _freeDragStepOffset = Vector2Int.zero;
            _activeBlockIsFree = false;
        }
        
        private bool IsPointInsideBoard(Vector3 boardWorldPoint)
        {
            if (!IsBoardInitialized)
            {
                return false;
            }

            var boardOrigin = boardController.BoardOrigin;
            var gridDimensions = boardController.GridDimensions;
            var cellSize = boardController.CellSize;
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0 || cellSize <= 0f)
            {
                return false;
            }

            var relativeX = boardWorldPoint.x - boardOrigin.x;
            var relativeY = boardWorldPoint.y - boardOrigin.y;
            var width = gridDimensions.x * cellSize;
            var height = gridDimensions.y * cellSize;
            return relativeX >= 0f && relativeX < width && relativeY >= 0f && relativeY < height;
        }

        private Vector2Int ResolveDragAxis(Vector2 dragVector)
        {
            if (!IsBoardReadyForInput)
            {
                return Vector2Int.zero;
            }

            var cellSize = boardController.CellSize;
            var minAxisDistance = Mathf.Max(0.05f, cellSize * dragAxisActivationRatio);
            var horizontalDistance = Mathf.Abs(dragVector.x);
            var verticalDistance = Mathf.Abs(dragVector.y);

            if (horizontalDistance < minAxisDistance && verticalDistance < minAxisDistance)
            {
                return Vector2Int.zero;
            }

            return horizontalDistance >= verticalDistance ? Vector2Int.right : Vector2Int.up;
        }

        private static Direction ResolveDirectionFromAxisAndSign(Vector2Int axis, bool positive)
        {
            if (axis == Vector2Int.right)
            {
                return positive ? Direction.Right : Direction.Left;
            }

            return positive ? Direction.Up : Direction.Down;
        }

        private static int ConvertAxisDistanceToCellSteps(float axisDistance, float cellSize)
        {
            if (cellSize <= 0f)
                return 0;
            
            return axisDistance >= 0f ? Mathf.FloorToInt(axisDistance / cellSize) : -Mathf.FloorToInt(Mathf.Abs(axisDistance) / cellSize);
        }

        public void ForceFitInputArea()
        {
            TryFitInputArea();
        }

        private Camera ResolvePointerCamera(PointerEventData eventData)
        {
            if (eventData.pressEventCamera)
            {
                return eventData.pressEventCamera;
            }

            return eventData.enterEventCamera ? eventData.enterEventCamera : inputCamera;
        }
    }
}
