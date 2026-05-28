using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class OptionsScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Options UI", new Color(0.09f, 0.10f, 0.13f));
            SimpleUi.Label(ui.transform, "オプション", 38, new Vector2(0, 165), new Vector2(480, 70));
            SimpleUi.Label(ui.transform, "音量", 22, new Vector2(-175, 80), new Vector2(140, 40));
            var slider = SimpleUi.Slider(ui.transform, new Vector2(45, 80));
            slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(v => AudioListener.volume = v);
            SimpleUi.Label(ui.transform, "移動: WASD / 矢印キー", 22, new Vector2(0, 15), new Vector2(520, 45));
            SimpleUi.Label(ui.transform, "攻撃: 自動", 22, new Vector2(0, -35), new Vector2(520, 45));
            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "戻る", new Vector2(0, -145), nav.LoadTitle);
        }
    }
}
