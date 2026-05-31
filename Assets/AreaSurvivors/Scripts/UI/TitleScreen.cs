using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TitleScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Title UI", new Color(0.08f, 0.11f, 0.09f), "Generated/UI/TitleBackground");
            SimpleUi.Panel(ui.transform, "Title Panel", new Vector2(-330, 0), new Vector2(470, 470), new Color(0.03f, 0.06f, 0.05f, 0.72f));
            SimpleUi.Label(ui.transform, "エリアサバイバー", 52, new Vector2(-330, 130), new Vector2(430, 70));
            SimpleUi.Label(ui.transform, "塗り広げた領地で塔を守り抜け", 21, new Vector2(-330, 75), new Vector2(390, 36), "Subtitle", new Color(0.78f, 0.91f, 0.80f));

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "プレイ", new Vector2(-330, 0), nav.LoadLobby, new Vector2(300, 58), "Generated/Tower");
            SimpleUi.Button(ui.transform, "オプション", new Vector2(-330, -76), nav.LoadOptions, new Vector2(300, 58), "Generated/Orb");
            SimpleUi.Button(ui.transform, "ゲーム終了", new Vector2(-330, -152), nav.Quit, new Vector2(300, 58), "Generated/Slash_1");
        }
    }
}
