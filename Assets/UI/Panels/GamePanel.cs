using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Localization;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public abstract class GamePanel : MonoBehaviour
    {
        private const float CompactShortEdgeThresholdPixels = 280f;
        private const float LogScaleBase = 2f;
        private const string HiddenClassName = "is-hidden";
        private static readonly Vector2Int PortraitReferenceResolution = new(1080, 1920);
        private readonly List<LocalizedTextBinding> _localizedTextBindings = new();
        private bool _isLocalizationEventsRegistered;
        private UIManager _statePublisher;
       
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
            CacheLocalizedTextBindingsFromUxml();
            RefreshLocalization();
            RegisterLocalizationEvents();
        }

        protected abstract void CacheElements();

        public virtual void RefreshLocalization() => ApplyLocalizedTextBindings();

        public void SubscribeToState(UIManager uiManager)
        {
            if (_statePublisher == uiManager)
            {
                return;
            }

            UnsubscribeFromState();
            _statePublisher = uiManager;
            if (_statePublisher != null)
            {
                _statePublisher.GameStateChanged += HandlePublishedGameStateChanged;
            }
        }

        public void UnsubscribeFromState()
        {
            if (_statePublisher == null)
            {
                return;
            }

            _statePublisher.GameStateChanged -= HandlePublishedGameStateChanged;
            _statePublisher = null;
        }

        protected virtual void OnEnable()
        {
            RegisterLocalizationEvents();
            RefreshLocalization();
        }

        protected virtual void OnDisable()
        {
            UnregisterLocalizationEvents();
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeFromState();
            UnregisterLocalizationEvents();
        }

        protected virtual void OnGameStateChanged(GameState state)
        {
        }

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

        private void CacheLocalizedTextBindingsFromUxml()
        {
            _localizedTextBindings.Clear();

            if (Root == null)
            {
                return;
            }

            Root.Query<TextElement>().ForEach(TrackLocalizedTextElement);
        }

        private void TrackLocalizedTextElement(TextElement element)
        {
            if (element == null || string.IsNullOrWhiteSpace(element.viewDataKey))
            {
                return;
            }

            _localizedTextBindings.Add(new LocalizedTextBinding(element, element.viewDataKey));
        }

        private void ApplyLocalizedTextBindings()
        {
            if (_localizedTextBindings.Count <= 0)
            {
                return;
            }

            for (var i = 0; i < _localizedTextBindings.Count; i++)
            {
                var binding = _localizedTextBindings[i];
                if (binding.Element == null)
                {
                    continue;
                }

                binding.Element.text = LocalizeKey(binding.Key);
            }
        }

        private void RegisterLocalizationEvents()
        {
            if (_isLocalizationEventsRegistered)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            _isLocalizationEventsRegistered = true;
        }

        private void UnregisterLocalizationEvents()
        {
            if (!_isLocalizationEventsRegistered)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            _isLocalizationEventsRegistered = false;
        }

        protected static string LocalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (!LocalizationSettings.HasSettings || LocalizationSettings.StringDatabase == null)
            {
                return key;
            }

            var localized = LocalizationSettings.StringDatabase.GetLocalizedString(LocalizationKeys.TableName, key);
            return string.IsNullOrEmpty(localized) ? key : localized;
        }

        private void HandlePublishedGameStateChanged(GameState state) => OnGameStateChanged(state);

        private void HandleLocaleChanged(Locale _) => RefreshLocalization();
    }
}
