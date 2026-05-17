using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public abstract class GamePanel : MonoBehaviour
    {
        private const float CompactShortEdgeThresholdPixels = 280f;
        private const float LogScaleBase = 2f;
        private const string HiddenClassName = "is-hidden";
        private static readonly Vector2Int PortraitReferenceResolution = new(1080, 1920);
       
        [SerializeField] private UIDocument uiDocument;

        protected VisualElement Root { get; private set; }
        private VisualElement PanelRoot { get; set; }
        protected virtual bool UseSafeAreaPadding => true;

        protected virtual void Awake()
        {
            Root = uiDocument.rootVisualElement;
            if (Root == null)
            {
                Debug.LogError($"{GetType().Name} could not access UIDocument rootVisualElement.", this);
                enabled = false;
                return;
            }

            ConfigurePanelSettings(uiDocument.panelSettings);
            Root.style.flexGrow = 1f;
            PanelRoot = ResolvePanelRoot();
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

            if (!UseSafeAreaPadding)
            {
                Root.style.paddingLeft = 0f;
                Root.style.paddingRight = 0f;
                Root.style.paddingTop = 0f;
                Root.style.paddingBottom = 0f;
                return;
            }

            var safeArea = Screen.safeArea;
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var panelScale = Mathf.Max(0.0001f, ResolvePanelScale(uiDocument.panelSettings));
            Root.style.paddingLeft = safeArea.xMin / panelScale;
            Root.style.paddingRight = (screenWidth - safeArea.xMax) / panelScale;
            Root.style.paddingTop = (screenHeight - safeArea.yMax) / panelScale;
            Root.style.paddingBottom = safeArea.yMin / panelScale;
        }

        private void RefreshResponsiveClasses()
        {
            if (Root == null)
            {
                return;
            }

            var shortEdgePixels = Mathf.Min(Screen.width, Screen.height);
            if (shortEdgePixels <= CompactShortEdgeThresholdPixels)
            {
                Root.AddToClassList("compact");
            }
            else
            {
                Root.RemoveFromClassList("compact");
            }
        }

        private static float ResolvePanelScale(PanelSettings panelSettings)
        {
            if (panelSettings == null)
            {
                return 1f;
            }

            if (panelSettings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
            {
                return Mathf.Max(0.0001f, panelSettings.scale);
            }

            var referenceResolution = panelSettings.referenceResolution;
            var referenceWidth = Mathf.Max(1f, referenceResolution.x);
            var referenceHeight = Mathf.Max(1f, referenceResolution.y);
            var widthScale = Mathf.Max(0.0001f, Screen.width / referenceWidth);
            var heightScale = Mathf.Max(0.0001f, Screen.height / referenceHeight);

            return panelSettings.screenMatchMode switch
            {
                PanelScreenMatchMode.MatchWidthOrHeight => Mathf.Pow(LogScaleBase,
                    Mathf.Lerp(Mathf.Log(widthScale, LogScaleBase), Mathf.Log(heightScale, LogScaleBase), panelSettings.match)),
                PanelScreenMatchMode.Expand => Mathf.Min(widthScale, heightScale),
                PanelScreenMatchMode.Shrink => Mathf.Max(widthScale, heightScale),
                _ => Mathf.Max(0.0001f, panelSettings.scale)
            };
        }

        public virtual void Show()
        {
            if (PanelRoot == null)
            {
                return;
            }

            PanelRoot.RemoveFromClassList(HiddenClassName);
        }

        public virtual void Hide()
        {
            if (PanelRoot == null)
            {
                return;
            }

            PanelRoot.AddToClassList(HiddenClassName);
        }

        private VisualElement ResolvePanelRoot()
        {
            if (Root == null || Root.childCount <= 0)
            {
                return Root;
            }

            return Root.ElementAt(0) ?? Root;
        }

    }
}
