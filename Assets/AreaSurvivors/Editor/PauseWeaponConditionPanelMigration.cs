using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class PauseWeaponConditionPanelMigration
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string SuccessMarkerPath = "TokenReports/Validation/pause-weapon-condition-panel-migration.success";
        const string DetailsName = "Pause Condition Details";
        static readonly Vector2 DetailsSize = new Vector2(198f, 126f);
        static readonly Color HeaderColor = new Color(1f, 0.84f, 0.18f, 1f);

        static readonly string[] PanelNames =
        {
            "Slash Weapon Status",
            "Arrow Weapon Status",
            "Fireball Weapon Status"
        };

        [MenuItem("Area Survivors/Migrate/Add Pause Weapon Condition Panels")]
        public static void MigrateFromMenu()
        {
            DeleteSuccessMarker();
            var scene = OpenGameScene(out var openedAdditive);
            try
            {
                var hud = FindInScene(scene, "HUD");
                var weaponStatus = hud != null ? hud.transform.Find("Weapon Status") : null;
                if (weaponStatus == null) throw new InvalidOperationException("HUD/Weapon Status was not found in 05_Game.unity.");

                bool changed = false;
                for (int i = 0; i < PanelNames.Length; i++)
                {
                    var panel = weaponStatus.Find(PanelNames[i]) as RectTransform;
                    if (panel == null) throw new InvalidOperationException("Missing weapon panel: " + PanelNames[i]);
                    if (panel.Find(DetailsName) != null) continue;
                    CreateConditionDetails(panel);
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException("Failed to save 05_Game.unity after adding pause weapon condition panels.");
                    }
                }

                WriteSuccessMarker();
                Debug.Log("Pause weapon condition panel migration completed. Existing weapon panel RectTransforms were not modified.");
            }
            finally
            {
                if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void CreateConditionDetails(RectTransform panel)
        {
            var detailsObject = new GameObject(DetailsName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var details = detailsObject.GetComponent<RectTransform>();
            details.SetParent(panel, false);
            details.anchorMin = new Vector2(1f, 0.5f);
            details.anchorMax = new Vector2(1f, 0.5f);
            details.pivot = new Vector2(0f, 0.5f);
            details.anchoredPosition = new Vector2(4f, 0f);
            details.sizeDelta = DetailsSize;

            var sourceImage = panel.GetComponent<Image>();
            var image = detailsObject.GetComponent<Image>();
            image.raycastTarget = false;
            if (sourceImage != null)
            {
                image.sprite = sourceImage.sprite;
                image.type = sourceImage.type;
                image.color = sourceImage.color;
                image.material = sourceImage.material;
            }

            var sourceOutline = panel.GetComponent<UiBoxOutline>();
            UiBoxOutline.Apply(details, sourceOutline != null ? sourceOutline.color : new Color(0.58f, 0.68f, 0.40f, 0.9f), sourceOutline != null ? sourceOutline.thickness : 2f);

            var titleTemplate = panel.Find("Title")?.GetComponent<Text>();
            var valueTemplate = panel.Find("Attack Row/Value")?.GetComponent<Text>();
            CreateText(details, "Special Effect Label", "特殊効果", 4f, 18f, 13, HeaderColor, FontStyle.Bold, titleTemplate);
            CreateText(details, "Special Effect Text", "-", 22f, 38f, 11, valueTemplate != null ? valueTemplate.color : Color.white, FontStyle.Normal, valueTemplate);
            CreateText(details, "Evolution Condition Label", "進化条件", 64f, 18f, 13, HeaderColor, FontStyle.Bold, titleTemplate);
            CreateText(details, "Evolution Condition Text", "-", 82f, 40f, 11, valueTemplate != null ? valueTemplate.color : Color.white, FontStyle.Normal, valueTemplate);
        }

        static void CreateText(RectTransform parent, string name, string value, float top, float height, int fontSize, Color color, FontStyle fontStyle, Text template)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-12f, height);

            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = template != null ? template.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.material = template != null ? template.material : text.material;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 0.9f;
            text.supportRichText = true;
            text.raycastTarget = false;
        }

        static Scene OpenGameScene(out bool openedAdditive)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path != GameScenePath) continue;
                openedAdditive = false;
                return loaded;
            }

            openedAdditive = true;
            return EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            if (!scene.IsValid()) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name) return roots[i];
            }

            return null;
        }

        static void DeleteSuccessMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteSuccessMarker()
        {
            var directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
        }
    }
}
