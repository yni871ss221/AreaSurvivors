using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class HudLayoutReporter
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";

        [MenuItem("Area Survivors/Reports/HUD Layout")]
        public static void LogHudLayout()
        {
            var report = BuildReport(false);
            Debug.Log(ReportOutputUtility.SaveAndSummarize("HUD layout report", report, "hud-layout"));
        }

        [MenuItem("Area Survivors/Reports/Construction Menu Layout")]
        public static void LogConstructionMenuLayout()
        {
            var report = BuildReport(true);
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Construction menu layout report", report, "construction-menu-layout"));
        }

        static string BuildReport(bool constructionMenuOnly)
        {
            var report = new StringBuilder(8192);
            report.AppendLine(constructionMenuOnly ? "AreaSurvivors Construction Menu Layout" : "AreaSurvivors HUD Layout");
            var scene = OpenGameScene(out var openedAdditive);
            if (!scene.IsValid())
            {
                report.AppendLine($"- missing scene: {GameScenePath}");
                return report.ToString();
            }

            var hud = FindInScene(scene, "HUD");
            if (hud == null)
            {
                report.AppendLine("- HUD not found.");
                CloseIfOpened(scene, openedAdditive);
                return report.ToString();
            }

            if (constructionMenuOnly)
            {
                AppendConstructionMenu(report, hud.transform);
            }
            else
            {
                AppendHudOverview(report, hud.transform);
                AppendPlayerHud(report, hud.transform);
                AppendConstructionMenu(report, hud.transform);
                AppendResourceHud(report, hud.transform);
                AppendWeaponHud(report, hud.transform);
            }

            CloseIfOpened(scene, openedAdditive);
            return report.ToString();
        }

        static Scene OpenGameScene(out bool openedAdditive)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == GameScenePath)
                {
                    openedAdditive = false;
                    return loaded;
                }
            }

            openedAdditive = true;
            return EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        static void CloseIfOpened(Scene scene, bool openedAdditive)
        {
            if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var found = FindDeep(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void AppendHudOverview(StringBuilder report, Transform hud)
        {
            report.AppendLine();
            report.AppendLine("[HUD Children]");
            for (int i = 0; i < hud.childCount; i++)
            {
                var child = hud.GetChild(i);
                var rect = child as RectTransform;
                report.Append("- ");
                report.Append(child.name);
                if (rect != null) AppendRect(report, rect);
                report.AppendLine($" active={child.gameObject.activeSelf}");
            }
        }

        static void AppendConstructionMenu(StringBuilder report, Transform hud)
        {
            report.AppendLine();
            report.AppendLine("[Construction Menu]");
            var menu = hud.Find("Construction Menu") as RectTransform;
            if (menu == null)
            {
                report.AppendLine("- missing");
                return;
            }

            report.Append("- root");
            AppendRect(report, menu);
            report.AppendLine($" active={menu.gameObject.activeSelf}");

            for (int i = 1; i <= 8; i++)
            {
                var slot = menu.Find("Build Slot " + i) as RectTransform;
                if (slot == null) continue;
                var button = slot.GetComponent<Button>();
                var icon = slot.Find("Icon")?.GetComponent<Image>();
                var key = slot.Find("Key")?.GetComponent<Text>();
                var stock = slot.Find("Stock")?.GetComponent<Text>();
                var iconName = icon != null ? SpriteName(icon.sprite) : "missing";
                var keyText = key != null ? key.text : "missing";
                var stockText = stock != null ? stock.text : "missing";
                report.Append($"- slot {i}");
                AppendRect(report, slot);
                report.Append($" button={(button != null)} icon={iconName}");
                report.Append($" key={keyText} stock={stockText}");
                report.AppendLine();
            }

            AppendNamedChild(report, menu, "Build Status Panel");
            AppendNamedChild(report, menu, "Build Status");
            AppendNamedChild(report, menu, "Test Add Wood Button");
            AppendNamedChild(report, menu, "Test Add Stone Button");
        }

        static void AppendResourceHud(StringBuilder report, Transform hud)
        {
            report.AppendLine();
            report.AppendLine("[Resource HUD]");
            AppendNamedChild(report, hud, "Wood Resource");
            AppendNamedChild(report, hud, "Wood Resource/Amount");
            AppendNamedChild(report, hud, "Wood Resource/Icon");
            AppendNamedChild(report, hud, "Stone Resource");
            AppendNamedChild(report, hud, "Stone Resource/Amount");
            AppendNamedChild(report, hud, "Stone Resource/Icon");
            AppendNamedChild(report, hud, "Token Resource");
            AppendNamedChild(report, hud, "Token Resource/Amount");
            AppendNamedChild(report, hud, "Token Resource/Icon");
        }

        static void AppendPlayerHud(StringBuilder report, Transform hud)
        {
            report.AppendLine();
            report.AppendLine("[Player HUD]");
            AppendNamedChild(report, hud, "Player Status");
            AppendPlayerStatRow(report, hud, "Speed Text Box");
            AppendPlayerStatRow(report, hud, "Paint Text Box");
            AppendPlayerStatRow(report, hud, "Revive Text Box");
            AppendPlayerStatRow(report, hud, "Defense Text Box");
            AppendPlayerStatRow(report, hud, "Xp Gain Text Box");
            AppendPlayerStatRow(report, hud, "Regen Text Box");
        }

        static void AppendPlayerStatRow(StringBuilder report, Transform hud, string rowName)
        {
            AppendNamedChild(report, hud, "Player Status/" + rowName);
            AppendNamedChild(report, hud, "Player Status/" + rowName + "/Icon");
            AppendNamedChild(report, hud, "Player Status/" + rowName + "/Name");
            AppendNamedChild(report, hud, "Player Status/" + rowName + "/Value");
        }

        static void AppendWeaponHud(StringBuilder report, Transform hud)
        {
            report.AppendLine();
            report.AppendLine("[Weapon HUD]");
            AppendWeaponPanel(report, hud, "Slash Weapon Status");
            AppendWeaponPanel(report, hud, "Arrow Weapon Status");
            AppendWeaponPanel(report, hud, "Fireball Weapon Status");
        }

        static void AppendWeaponPanel(StringBuilder report, Transform hud, string panelName)
        {
            AppendNamedChild(report, hud, panelName);
            AppendOptionalNamedChild(report, hud, panelName + "/Icon");
            AppendNamedChild(report, hud, panelName + "/Title");
            AppendOptionalNamedChild(report, hud, panelName + "/Attack Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Attack Row/Value");
            AppendOptionalNamedChild(report, hud, panelName + "/Cooldown Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Cooldown Row/Value");
            AppendOptionalNamedChild(report, hud, panelName + "/Knockback Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Knockback Row/Value");
            AppendOptionalNamedChild(report, hud, panelName + "/Projectile Count Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Projectile Count Row/Value");
            AppendOptionalNamedChild(report, hud, panelName + "/Explosion Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Explosion Row/Value");
            AppendOptionalNamedChild(report, hud, panelName + "/Range Row/Icon");
            AppendOptionalNamedChild(report, hud, panelName + "/Range Row/Value");
        }

        static void AppendOptionalNamedChild(StringBuilder report, Transform root, string path)
        {
            if (root == null || root.Find(path) == null) return;
            AppendNamedChild(report, root, path);
        }

        static void AppendNamedChild(StringBuilder report, Transform root, string path)
        {
            var target = root.Find(path);
            if (target == null)
            {
                report.AppendLine($"- {path}: missing");
                return;
            }

            report.Append($"- {path}");
            var rect = target as RectTransform;
            if (rect != null) AppendRect(report, rect);
            var image = target.GetComponent<Image>();
            var text = target.GetComponent<Text>();
            if (image != null) report.Append($" image={SpriteName(image.sprite)}");
            if (text != null) report.Append($" text=\"{text.text}\"");
            report.AppendLine($" active={target.gameObject.activeSelf}");
        }

        static void AppendRect(StringBuilder report, RectTransform rect)
        {
            report.Append($" pos={Vector(rect.anchoredPosition)} size={Vector(rect.sizeDelta)} anchorMin={Vector(rect.anchorMin)} anchorMax={Vector(rect.anchorMax)}");
        }

        static string Vector(Vector2 value)
        {
            return $"({value.x:0.#},{value.y:0.#})";
        }

        static string SpriteName(Sprite sprite)
        {
            return sprite != null ? sprite.name : "missing";
        }
    }
}
