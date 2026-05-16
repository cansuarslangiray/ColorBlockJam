using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public abstract class GamePanel : MonoBehaviour
    {
        private static readonly Vector2Int PortraitReferenceResolution = new (1080, 1920);
        [SerializeField] private UIDocument uiDocument;

        protected VisualElement Root { get; private set; }

        protected virtual void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError($"{GetType().Name} requires a UIDocument reference.", this);
                enabled = false;
                return;
            }

            Root = uiDocument.rootVisualElement;
            if (Root == null)
            {
                Debug.LogError($"{GetType().Name} could not access UIDocument rootVisualElement.", this);
                enabled = false;
                return;
            }

            ConfigurePanelSettings(uiDocument.panelSettings);
            Root.style.flexGrow = 1f;
            Root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ApplySafeAreaPadding();
            RefreshResponsiveClasses();
            CacheElements();
        }

        protected abstract void CacheElements();

        private static void ConfigurePanelSettings(PanelSettings panelSettings)
        {
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = PortraitReferenceResolution;
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
        }

        private void HandleGeometryChanged(GeometryChangedEvent _)
        {
            ApplySafeAreaPadding();
            RefreshResponsiveClasses();
        }

        private void ApplySafeAreaPadding()
        {
            if (Root == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);

            Root.style.paddingLeft = safeArea.xMin;
            Root.style.paddingRight = screenWidth - safeArea.xMax;
            Root.style.paddingTop = screenHeight - safeArea.yMax;
            Root.style.paddingBottom = safeArea.yMin;
        }

        private void RefreshResponsiveClasses()
        {
            if (Root == null)
            {
                return;
            }

            var shortEdge = Mathf.Min(Screen.width, Screen.height);
            if (shortEdge <= 720)
            {
                Root.AddToClassList("compact");
            }
            else
            {
                Root.RemoveFromClassList("compact");
            }
        }

        public virtual void Show()
        {
            if (Root == null)
            {
                return;
            }

            Root.style.display = DisplayStyle.Flex;
        }

        public virtual void Hide()
        {
            if (Root == null)
            {
                return;
            }

            Root.style.display = DisplayStyle.None;
        }

    }
}
