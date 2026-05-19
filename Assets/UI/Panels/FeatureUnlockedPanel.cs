using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class FeatureUnlockedPanel : GamePanel
    {
        private const int MaxVisibleFeatures = 2;
        private const string IconHiddenClassName = "feature-unlocked-icon-hidden";
        private const string IconRowHiddenClassName = "feature-unlocked-icon-row-hidden";
        private const string FeatureItemHiddenClassName = "feature-unlocked-item-hidden";

        [SerializeField] private AudioManager audioManager;

        private Button _nextButton;
        private VisualElement _iconRow;
        private readonly List<BlockFeatureDefinition> _activeFeatureDefinitions = new(MaxVisibleFeatures);
        private readonly Label[] _featureTitleLabels = new Label[MaxVisibleFeatures];
        private readonly Label[] _featureDescriptionLabels = new Label[MaxVisibleFeatures];
        private readonly VisualElement[] _featureIcons = new VisualElement[MaxVisibleFeatures];
        private readonly VisualElement[] _featureItems = new VisualElement[MaxVisibleFeatures];

        public event Action NextRequested;

        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _nextButton = Root.Q<Button>("feature-unlocked-next");
            _iconRow = Root.Q<VisualElement>("feature-unlocked-icon-row");
            _featureIcons[0] = Root.Q<VisualElement>("feature-unlocked-feature-icon-1");
            _featureIcons[1] = Root.Q<VisualElement>("feature-unlocked-feature-icon-2");
            _featureItems[0] = Root.Q<VisualElement>("feature-unlocked-item-1");
            _featureItems[1] = Root.Q<VisualElement>("feature-unlocked-item-2");
            _featureTitleLabels[0] = Root.Q<Label>("feature-unlocked-feature-title-1");
            _featureTitleLabels[1] = Root.Q<Label>("feature-unlocked-feature-title-2");
            _featureDescriptionLabels[0] = Root.Q<Label>("feature-unlocked-feature-description-1");
            _featureDescriptionLabels[1] = Root.Q<Label>("feature-unlocked-feature-description-2");

            _nextButton.clicked += HandleNextClicked;
            Hide();
        }

        public void Configure(IReadOnlyList<BlockFeatureDefinition> definitions)
        {
            _activeFeatureDefinitions.Clear();
            if (definitions != null)
            {
                for (var i = 0; i < definitions.Count && _activeFeatureDefinitions.Count < MaxVisibleFeatures; i++)
                {
                    if (definitions[i] == null)
                    {
                        continue;
                    }

                    _activeFeatureDefinitions.Add(definitions[i]);
                }
            }

            ApplyFeatureContent();
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            ApplyFeatureContent();
        }

        protected override void OnGameStateChanged(GameState state)
        {
            if (state == GameState.FeatureUnlocked)
            {
                Show();
                return;
            }

            Hide();
        }

        protected override void OnDestroy()
        {
            if (_nextButton != null)
            {
                _nextButton.clicked -= HandleNextClicked;
            }

            base.OnDestroy();
        }

        private void ApplyFeatureContent()
        {
            var hasAnyIcon = false;
            for (var slotIndex = 0; slotIndex < MaxVisibleFeatures; slotIndex++)
            {
                var hasFeature = slotIndex < _activeFeatureDefinitions.Count;
                var definition = hasFeature ? _activeFeatureDefinitions[slotIndex] : null;
                ApplyFeatureSlot(slotIndex, definition, hasFeature);
                hasAnyIcon |= definition != null && definition.icon != null;
            }

            if (_iconRow != null)
            {
                _iconRow.EnableInClassList(IconRowHiddenClassName, !hasAnyIcon);
            }
        }

        private void ApplyFeatureSlot(int slotIndex, BlockFeatureDefinition definition, bool visible)
        {
            if (slotIndex < 0 || slotIndex >= MaxVisibleFeatures)
            {
                return;
            }

            var item = _featureItems[slotIndex];
            if (item != null)
            {
                item.EnableInClassList(FeatureItemHiddenClassName, !visible);
            }

            var titleLabel = _featureTitleLabels[slotIndex];
            if (titleLabel != null)
            {
                titleLabel.text = visible && definition != null
                    ? definition.ResolveLocalizedDisplayName()
                    : string.Empty;
            }

            var descriptionLabel = _featureDescriptionLabels[slotIndex];
            if (descriptionLabel != null)
            {
                descriptionLabel.text = visible && definition != null
                    ? definition.ResolveLocalizedDescription()
                    : string.Empty;
            }

            ApplyFeatureIcon(slotIndex, visible && definition != null ? definition.icon : null);
        }

        private void ApplyFeatureIcon(int slotIndex, Sprite icon)
        {
            if (slotIndex < 0 || slotIndex >= MaxVisibleFeatures)
            {
                return;
            }

            var featureIcon = _featureIcons[slotIndex];
            if (featureIcon == null)
            {
                return;
            }

            var hasIcon = icon != null;
            featureIcon.EnableInClassList(IconHiddenClassName, !hasIcon);
            featureIcon.style.backgroundImage = hasIcon
                ? new StyleBackground(icon)
                : new StyleBackground((Sprite)null);
        }

        private void HandleNextClicked()
        {
            if (audioManager != null)
            {
                audioManager.PlayButtonClick();
            }

            NextRequested?.Invoke();
        }
    }
}
