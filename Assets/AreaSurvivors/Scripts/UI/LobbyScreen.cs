using UnityEngine;

namespace AreaSurvivors
{
    public sealed class LobbyScreen : MonoBehaviour
    {
        void Start()
        {
            var ui = SimpleUi.Root("Lobby UI", new Color(0.10f, 0.14f, 0.11f), "Generated/UI/LobbyBackground");
            SimpleUi.Panel(ui.transform, "Header Panel", new Vector2(0, 255), new Vector2(780, 92), new Color(0.03f, 0.06f, 0.05f, 0.68f));
            SimpleUi.Label(ui.transform, "ロビー", 40, new Vector2(0, 276), new Vector2(520, 48));
            SimpleUi.Label(ui.transform, $"トークン {ProgressionStore.Data.tokens}   累計撃破 {ProgressionStore.Data.totalKills}", 22, new Vector2(0, 235), new Vector2(620, 36), "TokenInfo", new Color(0.86f, 0.94f, 0.80f));

            SimpleUi.Panel(ui.transform, "Character Panel", new Vector2(0, -14), new Vector2(760, 270), new Color(0.03f, 0.06f, 0.05f, 0.62f));
            SimpleUi.Label(ui.transform, "出撃キャラクター", 24, new Vector2(0, 104), new Vector2(420, 38), "CharacterTitle", new Color(0.96f, 0.90f, 0.68f));
            SimpleUi.CharacterSelector(ui.transform);

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "ゲームスタート", new Vector2(-180, -236), nav.LoadGame, new Vector2(310, 58), "Generated/Arrow");
            SimpleUi.Button(ui.transform, "強化", new Vector2(150, -236), nav.LoadUpgrades, new Vector2(250, 58), "Generated/Orb");
            SimpleUi.Button(ui.transform, "タイトルへ", new Vector2(430, -236), nav.LoadTitle, new Vector2(210, 52), "Generated/Slash_0");
        }
    }
}
