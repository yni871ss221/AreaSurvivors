using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class OptionsScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Options UI", new Color(0.09f, 0.10f, 0.13f), "Generated/UI/UpgradeBackground");
            SimpleUi.Panel(ui.transform, "Options Panel", new Vector2(0, 28), new Vector2(620, 430), new Color(0.035f, 0.04f, 0.05f, 0.78f));
            SimpleUi.Label(ui.transform, "オプション", 40, new Vector2(0, 186), new Vector2(420, 52));

            SimpleUi.Label(ui.transform, "音量", 24, new Vector2(-214, 86), new Vector2(120, 40), "VolumeLabel", new Color(0.96f, 0.90f, 0.68f));
            var slider = SimpleUi.Slider(ui.transform, new Vector2(70, 86));
            slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(v => AudioListener.volume = v);

            SimpleUi.Label(ui.transform, "移動", 23, new Vector2(-205, 10), new Vector2(130, 36), "MoveTitle", new Color(0.96f, 0.90f, 0.68f));
            SimpleUi.Label(ui.transform, "WASD / 矢印キー", 23, new Vector2(80, 10), new Vector2(360, 36));
            SimpleUi.Label(ui.transform, "攻撃", 23, new Vector2(-205, -52), new Vector2(130, 36), "AttackTitle", new Color(0.96f, 0.90f, 0.68f));
            SimpleUi.Label(ui.transform, "自動攻撃", 23, new Vector2(80, -52), new Vector2(360, 36));

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "戻る", new Vector2(0, -162), nav.LoadTitle, new Vector2(230, 54), "Generated/Slash_0");
        }
    }
}
