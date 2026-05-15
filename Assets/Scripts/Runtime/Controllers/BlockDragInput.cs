using Runtime.Domain.Enums;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Controllers
{
    public class BlockDragInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private BlockView blockView;
        [SerializeField, Min(1f)] private float dragThresholdPixels= 25f;
        [SerializeField] private bool allowContinuousStepDrag= true;

        private Vector2 _lastPointerPosition;
        private int _lastPointerEventFrame = -1;

        private void Awake()
        {
            if (blockView == null)
            {
                blockView = GetComponent<BlockView>();
            }
        }

        public void Configure(BoardController board, BlockView view)
        {
            boardController = board;
            blockView = view;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastPointerEventFrame = Time.frameCount;
            _lastPointerPosition = eventData.position;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastPointerEventFrame = Time.frameCount;
            _lastPointerPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _lastPointerEventFrame = Time.frameCount;
            TryMoveByDrag(eventData.position);
        }

        private void OnMouseDown()
        {
            if (_lastPointerEventFrame == Time.frameCount)
            {
                return;
            }

            _lastPointerPosition = Input.mousePosition;
        }

        private void OnMouseDrag()
        {
            if (_lastPointerEventFrame == Time.frameCount)
            {
                return;
            }

            TryMoveByDrag(Input.mousePosition);
        }

        private void TryMoveByDrag(Vector2 pointerPosition)
        {
            if (boardController == null || blockView == null)
            {
                return;
            }

            var delta = pointerPosition - _lastPointerPosition;
            var absoluteX = Mathf.Abs(delta.x);
            var absoluteY = Mathf.Abs(delta.y);
            var isHorizontalDrag = absoluteX >= absoluteY;
            var dominantDistance = isHorizontalDrag ? absoluteX : absoluteY;

            if (dominantDistance < dragThresholdPixels)
            {
                return;
            }

            var direction = isHorizontalDrag ? (delta.x > 0f ? Direction.Right : Direction.Left) : (delta.y > 0f ? Direction.Up : Direction.Down);

            var moveAttemptCount = allowContinuousStepDrag ? Mathf.Max(1, Mathf.FloorToInt(dominantDistance / dragThresholdPixels)) : 1;

            var movedAnyStep = false;
            for (var i = 0; i < moveAttemptCount; i++)
            {
                if (!boardController.TryMoveBlock(blockView.BlockId, direction))
                {
                    break;
                }

                movedAnyStep = true;
            }

            if (allowContinuousStepDrag && movedAnyStep)
            {
                var consumedDelta = isHorizontalDrag ? new Vector2(Mathf.Sign(delta.x) * (moveAttemptCount * dragThresholdPixels), 0f) : new Vector2(0f, Mathf.Sign(delta.y) * (moveAttemptCount * dragThresholdPixels));

                _lastPointerPosition += consumedDelta;
                return;
            }

            _lastPointerPosition = pointerPosition;
        }
    }
}
