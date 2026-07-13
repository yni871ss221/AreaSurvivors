using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GeneralOptionsPanel : MonoBehaviour
    {
        public Dropdown languageDropdown;
        bool languageEventBound;

        public void Bind()
        {
            if (!languageEventBound)
            {
                LocalizationService.LanguageChanged += OnLanguageRefreshed;
                languageEventBound = true;
            }
            Refresh();
        }

        public void Refresh()
        {
            if (languageDropdown == null) return;

            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(LocalizationService.IsEnglish
                ? new List<string> { "Japanese", "English" }
                : new List<string> { "日本語", "English" });
            languageDropdown.SetValueWithoutNotify((int)LocalizationSettingsStore.Current);
            languageDropdown.RefreshShownValue();
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        void OnDestroy()
        {
            if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            if (languageEventBound) LocalizationService.LanguageChanged -= OnLanguageRefreshed;
        }

        void OnLanguageRefreshed()
        {
            Refresh();
        }

        static void OnLanguageChanged(int value)
        {
            LocalizationSettingsStore.Set(value == (int)GameLanguage.English
                ? GameLanguage.English
                : GameLanguage.Japanese);
        }
    }
}
