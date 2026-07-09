using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GeneralOptionsPanel : MonoBehaviour
    {
        public Dropdown languageDropdown;

        public void Bind()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (languageDropdown == null) return;

            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string> { "日本語" });
            languageDropdown.SetValueWithoutNotify(0);
            languageDropdown.RefreshShownValue();
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        void OnDestroy()
        {
            if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        }

        static void OnLanguageChanged(int value)
        {
            // 現時点では日本語のみ。将来の多言語化時に保存処理をここへ追加する。
        }
    }
}
