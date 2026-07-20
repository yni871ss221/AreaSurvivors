using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class PauseWeaponConditionPanelValidator
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string SuccessMarkerPath = "TokenReports/Validation/pause-weapon-condition-panel-validator.success";
        const string DetailsName = "Pause Condition Details";
        static readonly Vector2 ExpectedPanelSize = new Vector2(180f, 126f);
        static readonly Vector2 ExpectedDetailsSize = new Vector2(198f, 126f);
        static readonly WeaponType[] SpecialEffectWeapons =
        {
            WeaponType.Slash,
            WeaponType.Arrow,
            WeaponType.Fireball,
            WeaponType.Shield,
            WeaponType.Flag,
            WeaponType.BoomerangSword,
            WeaponType.AuraSword,
            WeaponType.ArrowRain,
            WeaponType.Gun,
            WeaponType.Frost,
            WeaponType.ThunderBall
        };

        static readonly PanelExpectation[] Panels =
        {
            new PanelExpectation("Slash Weapon Status", new Vector2(-576f, 35f)),
            new PanelExpectation("Arrow Weapon Status", new Vector2(-576f, -92f)),
            new PanelExpectation("Fireball Weapon Status", new Vector2(-576f, -221f))
        };

        [MenuItem("Area Survivors/Validate/Pause Weapon Condition Panels")]
        public static void ValidateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Pause weapon condition panel validation requires Edit Mode because it may open 05_Game.unity additively.");
            }

            DeleteSuccessMarker();
            var scene = OpenGameScene(out var openedAdditive);
            try
            {
                var failures = new List<string>();
                var hud = FindInScene(scene, "HUD");
                var weaponStatus = hud != null ? hud.transform.Find("Weapon Status") : null;
                if (weaponStatus == null)
                {
                    failures.Add("HUD/Weapon Status is missing.");
                }
                else
                {
                    for (int i = 0; i < Panels.Length; i++) ValidatePanel(weaponStatus, Panels[i], failures);
                }
                ValidateSpecialEffectDescriptions(failures);

                if (failures.Count > 0) throw new InvalidOperationException("Pause weapon condition panel validation failed:\n- " + string.Join("\n- ", failures));
                WriteSuccessMarker();
                Debug.Log("Pause weapon condition panel validation passed. Three panels and all text bindings are present; original panel positions and sizes are unchanged.");
            }
            finally
            {
                if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidatePanel(Transform weaponStatus, PanelExpectation expectation, List<string> failures)
        {
            var panel = weaponStatus.Find(expectation.name) as RectTransform;
            if (panel == null)
            {
                failures.Add(expectation.name + " is missing.");
                return;
            }

            ExpectVector(expectation.name + " position", panel.anchoredPosition, expectation.position, failures);
            ExpectVector(expectation.name + " size", panel.sizeDelta, ExpectedPanelSize, failures);

            var details = panel.Find(DetailsName) as RectTransform;
            if (details == null)
            {
                failures.Add(expectation.name + "/" + DetailsName + " is missing.");
                return;
            }

            ExpectVector(expectation.name + " details anchorMin", details.anchorMin, new Vector2(1f, 0.5f), failures);
            ExpectVector(expectation.name + " details anchorMax", details.anchorMax, new Vector2(1f, 0.5f), failures);
            ExpectVector(expectation.name + " details pivot", details.pivot, new Vector2(0f, 0.5f), failures);
            ExpectVector(expectation.name + " details position", details.anchoredPosition, new Vector2(4f, 0f), failures);
            ExpectVector(expectation.name + " details size", details.sizeDelta, ExpectedDetailsSize, failures);
            if (details.GetComponent<Image>() == null) failures.Add(expectation.name + " details Image is missing.");
            if (details.GetComponent<UiBoxOutline>() == null) failures.Add(expectation.name + " details UiBoxOutline is missing.");

            ExpectText(details, "Special Effect Label", "特殊効果", failures);
            ExpectText(details, "Special Effect Text", null, failures);
            ExpectText(details, "Evolution Condition Label", "進化条件", failures);
            ExpectText(details, "Evolution Condition Text", null, failures);
        }

        static void ExpectText(Transform parent, string name, string expectedText, List<string> failures)
        {
            var target = parent.Find(name);
            var text = target != null ? target.GetComponent<Text>() : null;
            if (text == null)
            {
                failures.Add(parent.parent.name + "/" + DetailsName + "/" + name + " Text is missing.");
                return;
            }

            if (expectedText != null && text.text != expectedText) failures.Add(name + " source text does not match: " + text.text);
            if (!text.supportRichText) failures.Add(name + " must support rich text.");
        }

        static void ValidateSpecialEffectDescriptions(List<string> failures)
        {
            for (int i = 0; i < SpecialEffectWeapons.Length; i++)
            {
                var type = SpecialEffectWeapons[i];
                string description = WeaponCatalog.AreaControlSpecialEffectDescriptionSource(type);
                if (string.IsNullOrEmpty(description))
                {
                    failures.Add(type + " special-effect description is missing.");
                    continue;
                }

                if (description.Contains("ます") || description.EndsWith("。", StringComparison.Ordinal))
                {
                    failures.Add(type + " special-effect description must be a concise non-polite phrase without a Japanese period: " + description);
                }
            }
        }

        static void ExpectVector(string label, Vector2 actual, Vector2 expected, List<string> failures)
        {
            if ((actual - expected).sqrMagnitude > 0.01f) failures.Add(label + " expected " + expected + " but was " + actual + ".");
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

        readonly struct PanelExpectation
        {
            public readonly string name;
            public readonly Vector2 position;

            public PanelExpectation(string name, Vector2 position)
            {
                this.name = name;
                this.position = position;
            }
        }
    }
}
