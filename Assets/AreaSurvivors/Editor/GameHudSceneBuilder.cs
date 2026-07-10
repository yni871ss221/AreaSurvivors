using AreaSurvivors.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class GameHudSceneBuilder
    {
        const string LeftStatusHudGroup = "LeftStatusHud";
        const string TopCenterHudGroup = "TopCenterHud";
        const string RightStatusHudGroup = "RightStatusHud";
        const float HudOverlapPadding = 96f;
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);

        public static void ConfigureHudOverlapGroupsMenu()
        {
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            ConfigureHudOverlapGroups(canvas.transform);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("HUD overlap groups were configured in the scene.");
        }

        [MenuItem("AreaSurvivors/Config/Normalize Enemy Spawn Defaults")]
        public static void NormalizeEnemySpawnDefaults()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            if (config == null)
            {
                Debug.LogError("GameConfig.asset was not found.");
                return;
            }

            config.EnsureEnemySpawnDefaults();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Enemy spawn defaults were normalized in GameConfig.asset.");
        }

        public static void ConvertUiFramesToOutlineComponents()
        {
            int outlined = 0;
            int removed = 0;
            var canvases = Object.FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                var images = canvas.GetComponentsInChildren<Image>(true);
                foreach (var image in images)
                {
                    if (image == null) continue;
                    bool hadLegacyEdges =
                        image.transform.Find("Top Edge") != null ||
                        image.transform.Find("Bottom Edge") != null ||
                        image.transform.Find("Left Edge") != null ||
                        image.transform.Find("Right Edge") != null;
                    if (hadLegacyEdges)
                    {
                        UiBoxOutline.Apply(image.transform, EdgeColor, 2f);
                        outlined++;
                    }

                    removed += DestroyChild(image.transform, "Top Edge");
                    removed += DestroyChild(image.transform, "Bottom Edge");
                    removed += DestroyChild(image.transform, "Left Edge");
                    removed += DestroyChild(image.transform, "Right Edge");
                }
            }

            if (canvases.Length > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log($"Converted UI frames to UiBoxOutline. outlined={outlined}, removed={removed}");
        }

        static int DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) return 0;
            Object.DestroyImmediate(child.gameObject);
            return 1;
        }

        static Canvas FindHudCanvas()
        {
            var hud = GameObject.Find("HUD");
            var canvas = hud != null ? hud.GetComponent<Canvas>() : null;
            if (canvas != null) return canvas;
            var canvases = Object.FindObjectsOfType<Canvas>(true);
            foreach (var candidate in canvases)
            {
                if (candidate != null && candidate.name == "HUD") return candidate;
            }

            return null;
        }

        static void ConfigureHudOverlapGroups(Transform hud)
        {
            ConfigureGroupPanel(hud, "Player", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Player Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Slash Weapon Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Arrow Weapon Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Fireball Weapon Status", LeftStatusHudGroup);

            ConfigureGroupPanel(hud, "Area Control Panel", TopCenterHudGroup);
            ConfigureGroupPanel(hud, "Stage Panel", TopCenterHudGroup);
            ConfigureGroupPanel(hud, "Timer Panel", TopCenterHudGroup);

            ConfigureGroupPanel(hud, "Tower Status", RightStatusHudGroup);
            ConfigureGroupPanel(hud, "Token Resource", RightStatusHudGroup);
            ConfigureGroupPanel(hud, "Kill Panel", RightStatusHudGroup);
        }

        static void ConfigureGroupPanel(Transform hud, string name, string groupId)
        {
            var rect = hud != null ? hud.Find(name) as RectTransform : null;
            if (rect == null) return;
            ConfigureOverlapFader(rect, groupId);
        }

        static void ConfigureOverlapFader(RectTransform panel, string groupId)
        {
            var group = EnsureComponent<CanvasGroup>(panel.gameObject);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            var fader = EnsureComponent<global::AreaSurvivors.HudOverlapFader>(panel.gameObject);
            fader.backgroundAlpha = 0.5f;
            fader.overlapAlpha = 0.2f;
            fader.padding = HudOverlapPadding;
            fader.fadeSpeed = 10f;
            fader.groupId = groupId;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(fader);
        }

        static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>();
            return component;
        }
    }
}
