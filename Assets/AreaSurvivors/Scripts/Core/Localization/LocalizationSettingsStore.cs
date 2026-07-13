using UnityEngine;

namespace AreaSurvivors
{
    public enum GameLanguage
    {
        Japanese = 0,
        English = 1
    }

    public static class LocalizationSettingsStore
    {
        const string LanguageKey = "AreaSurvivors.Language.v1";

        public static GameLanguage Current
        {
            get
            {
                int value = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Japanese);
                return value == (int)GameLanguage.English ? GameLanguage.English : GameLanguage.Japanese;
            }
        }

        public static void Set(GameLanguage language)
        {
            if (Current == language)
            {
                LocalizationService.RefreshAllTexts();
                return;
            }

            PlayerPrefs.SetInt(LanguageKey, (int)language);
            PlayerPrefs.Save();
            LocalizationService.NotifyLanguageChanged();
        }

        public static void ResetDefaults()
        {
            Set(GameLanguage.Japanese);
        }
    }
}
