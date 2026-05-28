using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TitleScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Title UI", new Color(0.08f, 0.11f, 0.09f));
            SimpleUi.Label(ui.transform, "エリアサバイバー（仮）", 44, new Vector2(0, 145), new Vector2(720, 80));
            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "プレイ", new Vector2(0, 40), nav.LoadLobby);
            SimpleUi.Button(ui.transform, "オプション", new Vector2(0, -35), nav.LoadOptions);
            SimpleUi.Button(ui.transform, "ゲーム終了", new Vector2(0, -110), nav.Quit);
        }
    }
}
