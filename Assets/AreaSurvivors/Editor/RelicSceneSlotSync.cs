using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class RelicSceneSlotSync
    {
        const string RelicScenePath = "Assets/AreaSurvivors/Scenes/09_Relics.unity";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";

        [MenuItem("Area Survivors/Relics/Sync Relic Book And HUD Slots")]
        public static void Sync()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            SyncRelicBookScene();
            SyncGameHudScene();
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Relic Book and HUD slots were synced with RelicCatalog.");
        }

        [MenuItem("Area Survivors/Validate/Relic Scene Slots")]
        public static void Validate()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var missing = new List<string>();
            CollectMissingRelicBookSlots(missing);
            CollectMissingHudSlots(missing);
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            if (missing.Count > 0)
            {
                Debug.LogError("Relic slot validation failed:\n" + string.Join("\n", missing));
                return;
            }

            Debug.Log("Relic slot validation passed.");
        }

        static void SyncRelicBookScene()
        {
            var scene = EditorSceneManager.OpenScene(RelicScenePath, OpenSceneMode.Single);
            var screen = Object.FindObjectOfType<RelicBookScreen>(true);
            if (screen == null)
            {
                Debug.LogError("RelicBookScreen was not found in " + RelicScenePath);
                return;
            }

            var entries = screen.GetComponentsInChildren<RelicBookEntryView>(true).ToList();
            if (entries.Count == 0)
            {
                Debug.LogError("No RelicBookEntryView template was found in " + RelicScenePath);
                return;
            }

            var template = entries[0];
            var parent = template.transform.parent;
            foreach (var definition in RelicCatalog.All)
            {
                if (definition == null || entries.Any(entry => entry != null && entry.relicType == definition.type)) continue;
                var clone = Object.Instantiate(template.gameObject, parent, false);
                clone.name = "Relic Card " + definition.type;
                var entry = clone.GetComponent<RelicBookEntryView>();
                ConfigureRelicBookEntry(entry, definition);
                entries.Add(entry);
                EditorUtility.SetDirty(clone);
            }

            entries = entries.Where(entry => entry != null).ToList();
            entries.Sort((a, b) => RelicCatalog.CompareDisplayOrder(a != null ? a.Definition : null, b != null ? b.Definition : null));
            screen.entries = entries.ToArray();
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SyncGameHudScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var panel = Object.FindObjectOfType<RelicHudPanel>(true);
            if (panel == null)
            {
                Debug.LogError("RelicHudPanel was not found in " + GameScenePath);
                return;
            }

            var slots = panel.GetComponentsInChildren<RelicHudSlot>(true).ToList();
            if (slots.Count == 0)
            {
                Debug.LogError("No RelicHudSlot template was found in " + GameScenePath);
                return;
            }

            var template = slots[0];
            var parent = template.transform.parent;
            foreach (var definition in RelicCatalog.All)
            {
                if (definition == null || slots.Any(slot => slot != null && slot.relicType == definition.type)) continue;
                var clone = Object.Instantiate(template.gameObject, parent, false);
                clone.name = "Relic Slot " + definition.type;
                var slot = clone.GetComponent<RelicHudSlot>();
                ConfigureRelicHudSlot(slot, definition);
                slots.Add(slot);
                EditorUtility.SetDirty(clone);
            }

            slots = slots.Where(slot => slot != null).ToList();
            slots.Sort((a, b) => RelicCatalog.CompareDisplayOrder(a != null ? a.Definition : null, b != null ? b.Definition : null));
            panel.slots = slots.ToArray();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureRelicBookEntry(RelicBookEntryView entry, RelicDefinition definition)
        {
            if (entry == null || definition == null) return;
            entry.relicType = definition.type;
            if (entry.button == null) entry.button = entry.GetComponent<Button>();
            if (entry.background == null) entry.background = entry.GetComponent<Image>();
            if (entry.icon == null) entry.icon = FindChildComponent<Image>(entry.transform, "Icon");
            if (entry.silhouetteOverlay == null) entry.silhouetteOverlay = FindChildComponent<Image>(entry.transform, "Silhouette Overlay");
            if (entry.nameText == null) entry.nameText = FindChildComponent<Text>(entry.transform, "Name Text");
            if (entry.nameText != null) entry.nameText.text = definition.displayName;
            if (entry.icon != null)
            {
                entry.icon.sprite = LoadRelicIcon(definition);
                entry.icon.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);
            }

            EditorUtility.SetDirty(entry);
        }

        static void ConfigureRelicHudSlot(RelicHudSlot slot, RelicDefinition definition)
        {
            if (slot == null || definition == null) return;
            slot.relicType = definition.type;
            if (slot.background == null) slot.background = slot.GetComponent<Image>();
            if (slot.icon == null) slot.icon = FindChildComponent<Image>(slot.transform, "Icon");
            if (slot.icon != null)
            {
                slot.icon.sprite = LoadRelicIcon(definition);
                slot.icon.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);
            }

            EditorUtility.SetDirty(slot);
        }

        static void CollectMissingRelicBookSlots(List<string> missing)
        {
            EditorSceneManager.OpenScene(RelicScenePath, OpenSceneMode.Single);
            var found = Object.FindObjectsOfType<RelicBookEntryView>(true).Select(entry => entry.relicType).ToHashSet();
            foreach (var definition in RelicCatalog.All)
            {
                if (definition != null && !found.Contains(definition.type)) missing.Add("RelicBook missing: " + definition.type);
            }
        }

        static void CollectMissingHudSlots(List<string> missing)
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var found = Object.FindObjectsOfType<RelicHudSlot>(true).Select(slot => slot.relicType).ToHashSet();
            foreach (var definition in RelicCatalog.All)
            {
                if (definition != null && !found.Contains(definition.type)) missing.Add("RelicHUD missing: " + definition.type);
            }
        }

        static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            var child = FindChild(root, name);
            return child != null ? child.GetComponent<T>() : null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static Sprite LoadRelicIcon(RelicDefinition definition)
        {
            if (definition == null) return null;
            var sprite = GeneratedSpriteLoader.Load(definition.iconPath);
            if (sprite != null) return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AreaSurvivors/Sprites/Generated/" + definition.iconPath + ".png");
        }
    }
}
