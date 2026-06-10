using System.Collections.Generic;
using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class HudSceneNormalizer
    {
        static readonly string[] FadePanels =
        {
            "Player", "Player Status", "Construction Menu", "Tower Status",
            "Timer Panel", "Kill Panel", "Wood Resource", "Stone Resource",
            "Token Resource", "Boss Status", "Announcement"
        };

        static readonly string[] NeverFadePanels = { "Level Up Panel" };

        static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>
        {
            { "Attack Text Box", "攻撃" },
            { "Cooldown Text Box", "間隔" },
            { "Speed Text Box", "速度" },
            { "Paint Text Box", "塗り" },
            { "Revive Text Box", "復活" },
            { "Projectile Text Box", "弾速" },
            { "Range Text Box", "範囲" },
            { "Knockback Text Box", "ノック" },
            { "Defense Text Box", "防御" },
            { "Xp Gain Text Box", "経験" },
            { "Regen Text Box", "回復" },
            { "Work Text Box", "作業" },
            { "Resource Text Box", "資源" }
        };

        [MenuItem("AreaSurvivors/HUD/Normalize Transparency And Stat Table")]
        public static void NormalizeCurrentScene()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null)
            {
                Debug.LogWarning("HUD was not found in the active scene.");
                return;
            }

            foreach (string panelName in FadePanels)
            {
                var panel = hud.transform.Find(panelName);
                if (panel == null || panel.GetComponent<RectTransform>() == null) continue;
                if (panel.GetComponent<CanvasGroup>() == null) panel.gameObject.AddComponent<CanvasGroup>();
                var fader = panel.GetComponent<HudOverlapFader>();
                if (fader == null) fader = panel.gameObject.AddComponent<HudOverlapFader>();
                fader.backgroundAlpha = 0.5f;
                fader.overlapAlpha = 0.2f;
                EditorUtility.SetDirty(panel.gameObject);
            }

            foreach (string panelName in NeverFadePanels)
            {
                var panel = hud.transform.Find(panelName);
                if (panel == null) continue;
                var fader = panel.GetComponent<HudOverlapFader>();
                if (fader != null) Object.DestroyImmediate(fader);
                var group = panel.GetComponent<CanvasGroup>();
                if (group != null) Object.DestroyImmediate(group);
                EditorUtility.SetDirty(panel.gameObject);
            }

            var stats = hud.transform.Find("Player Status");
            if (stats != null)
            {
                foreach (var entry in StatLabels)
                {
                    var box = stats.Find(entry.Key) as RectTransform;
                    if (box == null) continue;
                    NormalizeStatRow(box, entry.Value);
                }
            }

            EditorSceneManager.MarkSceneDirty(hud.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("HUD transparency and stat table were normalized.");
        }

        static void NormalizeStatRow(RectTransform box, string labelValue)
        {
            var labelTransform = box.Find("Name") ?? box.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label == null) return;
            label.gameObject.name = "Name";
            label.text = labelValue;
            label.alignment = TextAnchor.MiddleLeft;
            SetColumns(label.rectTransform, 0f, 0.62f, 5f, -2f);

            var valueTransform = box.Find("Value");
            var value = valueTransform != null ? valueTransform.GetComponent<Text>() : null;
            if (value == null)
            {
                var valueObject = new GameObject("Value");
                valueObject.transform.SetParent(box, false);
                value = valueObject.AddComponent<Text>();
                value.font = label.font;
                value.fontSize = label.fontSize;
                value.color = label.color;
                value.raycastTarget = false;
            }
            value.alignment = TextAnchor.MiddleRight;
            value.text = "-";
            SetColumns(value.rectTransform, 0.62f, 1f, 2f, -5f);

            var divider = box.Find("Divider");
            if (divider == null)
            {
                var dividerObject = new GameObject("Divider");
                dividerObject.transform.SetParent(box, false);
                var image = dividerObject.AddComponent<Image>();
                image.color = new Color(0.58f, 0.68f, 0.40f, 0.65f);
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.62f, 0.15f);
                rect.anchorMax = new Vector2(0.62f, 0.85f);
                rect.sizeDelta = new Vector2(1f, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        static void SetColumns(RectTransform rect, float minX, float maxX, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(right, 0f);
        }
    }
}
