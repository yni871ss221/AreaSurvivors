using UnityEngine;

namespace AreaSurvivors
{
    public sealed class LobbyScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Lobby UI", new Color(0.10f, 0.14f, 0.11f));
            SimpleUi.Label(ui.transform, "ロビー", 38, new Vector2(0, 160), new Vector2(480, 70));
            SimpleUi.Label(ui.transform, $"トークン {ProgressionStore.Data.tokens} / 累計撃破 {ProgressionStore.Data.totalKills}", 22, new Vector2(0, 100), new Vector2(620, 48));
            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.CharacterSelector(ui.transform);
            SimpleUi.Button(ui.transform, "ゲームスタート", new Vector2(0, -55), nav.LoadGame);
            SimpleUi.Button(ui.transform, "強化", new Vector2(0, -130), nav.LoadUpgrades);
            SimpleUi.Button(ui.transform, "タイトルへ", new Vector2(0, -205), nav.LoadTitle);
        }
    }
}
