using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class LobbyCharacterSelectionMigration
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";
        const string KnightSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Characters/Knight.png";
        const string ArcherSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Characters/Archer.png";
        const string MageSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Characters/Mage.png";
        const string MigrationMarkerPath = "Library/AreaSafeUnity/lobby-character-selection-migration.success";
        const string ValidatorMarkerPath = "Library/AreaSafeUnity/lobby-character-selection-validator.success";

        static readonly Color PanelColor = new Color(0.12f, 0.20f, 0.16f, 0.96f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.90f);

        [MenuItem("Area Survivors/Migrate/Lobby Character Selection")]
        public static void Migrate()
        {
            DeleteMarker(MigrationMarkerPath);
            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                var screen = FindInScene<LobbyScreen>(scene);
                var lobbyUi = FindInScene<Canvas>(scene, "Lobby UI");
                var characterCard = FindInScene<Transform>(scene, "Character Knight");
                if (screen == null || lobbyUi == null || characterCard == null)
                {
                    throw new InvalidOperationException("03_Lobby is missing LobbyScreen, Lobby UI, or Character Knight.");
                }

                var knightSprite = LoadSprite(KnightSpritePath);
                var archerSprite = LoadSprite(ArcherSpritePath);
                var mageSprite = LoadSprite(MageSpritePath);

                var mainButton = characterCard.GetComponent<Button>();
                var knightIconTransform = FindDirect(characterCard, "Knight Display Icon") ?? FindDirect(characterCard, "Icon");
                var knightIcon = knightIconTransform?.GetComponent<Image>();
                var title = FindDirect(characterCard, "Title")?.GetComponent<Text>();
                var description = FindDirect(characterCard, "Description")?.GetComponent<Text>();
                if (mainButton == null || knightIcon == null || title == null || description == null)
                {
                    throw new InvalidOperationException("Character Knight is missing its authored Button/Icon/Title/Description.");
                }

                mainButton.interactable = true;
                mainButton.transition = Selectable.Transition.None;
                var pointer = characterCard.GetComponent<SelectOnPointerEnter>();
                if (pointer == null) pointer = characterCard.gameObject.AddComponent<SelectOnPointerEnter>();
                pointer.enabled = true;
                var oldHighlight = characterCard.GetComponent<UiSelectionHighlight>();
                if (oldHighlight != null) oldHighlight.enabled = false;
                var selectedHighlight = characterCard.GetComponent<CharacterSelectionHighlight>();
                if (selectedHighlight != null) selectedHighlight.enabled = false;

                var archerDisplayIcon = EnsureDisplayIcon(characterCard, knightIcon, "Archer Display Icon", archerSprite);
                var mageDisplayIcon = EnsureDisplayIcon(characterCard, knightIcon, "Mage Display Icon", mageSprite);
                knightIcon.gameObject.name = "Knight Display Icon";
                EnsureFocusOutline(characterCard.gameObject, new Vector2(210f, 190f), 6f);

                var modal = EnsureRect(lobbyUi.transform, "Character Selection Modal", out bool modalCreated);
                if (modalCreated)
                {
                    Stretch(modal);
                    modal.SetAsLastSibling();
                }

                var dimmer = EnsureRect(modal, "Character Selection Dimmer", out bool dimmerCreated);
                var dimmerImage = dimmer.GetComponent<Image>();
                if (dimmerCreated)
                {
                    Stretch(dimmer);
                    dimmerImage = dimmer.gameObject.AddComponent<Image>();
                    dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);
                }
                if (dimmerImage == null) throw new InvalidOperationException("Character Selection Dimmer is missing Image.");
                dimmerImage.raycastTarget = true;
                var dimmerButton = dimmer.GetComponent<Button>();
                if (dimmerButton == null) dimmerButton = dimmer.gameObject.AddComponent<Button>();
                dimmerButton.targetGraphic = dimmerImage;
                dimmerButton.transition = Selectable.Transition.None;
                dimmerButton.interactable = true;
                var dimmerNavigation = dimmerButton.navigation;
                dimmerNavigation.mode = Navigation.Mode.None;
                dimmerButton.navigation = dimmerNavigation;

                var knightButton = EnsureChoiceButton(modal, "Character Choice Knight", knightSprite, new Vector2(-154f, -92f));
                var archerButton = EnsureChoiceButton(modal, "Character Choice Archer", archerSprite, new Vector2(-154f, -156f));
                var mageButton = EnsureChoiceButton(modal, "Character Choice Mage", mageSprite, new Vector2(-154f, -220f));
                var knightChoiceIcon = FindDirect(knightButton.transform, "Icon")?.GetComponent<Image>();
                var archerChoiceIcon = FindDirect(archerButton.transform, "Icon")?.GetComponent<Image>();
                var mageChoiceIcon = FindDirect(mageButton.transform, "Icon")?.GetComponent<Image>();
                if (knightChoiceIcon == null || archerChoiceIcon == null || mageChoiceIcon == null)
                {
                    throw new InvalidOperationException("Character choice icon references are missing.");
                }
                ConfigureNavigation(knightButton, knightButton, archerButton);
                ConfigureNavigation(archerButton, knightButton, mageButton);
                ConfigureNavigation(mageButton, archerButton, mageButton);

                screen.characterPanelButton = mainButton;
                screen.characterSelectionModal = modal.gameObject;
                screen.characterSelectionCancelButton = dimmerButton;
                screen.knightCharacterButton = knightButton;
                screen.archerCharacterButton = archerButton;
                screen.mageCharacterButton = mageButton;
                screen.knightCharacterChoiceIcon = knightChoiceIcon;
                screen.archerCharacterChoiceIcon = archerChoiceIcon;
                screen.mageCharacterChoiceIcon = mageChoiceIcon;
                screen.knightDisplayIcon = knightIcon;
                screen.archerDisplayIcon = archerDisplayIcon;
                screen.mageDisplayIcon = mageDisplayIcon;
                screen.characterNameText = title;
                screen.characterDescriptionText = description;

                knightIcon.gameObject.SetActive(true);
                archerDisplayIcon.gameObject.SetActive(false);
                mageDisplayIcon.gameObject.SetActive(false);
                modal.gameObject.SetActive(false);

                EditorUtility.SetDirty(screen);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save 03_Lobby.");
                WriteMarker(MigrationMarkerPath);
                Debug.Log("Lobby character selection migration completed.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Area Survivors/Validate/Lobby Character Selection")]
        public static void Validate()
        {
            DeleteMarker(ValidatorMarkerPath);
            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                int errors = 0;
                var screen = FindInScene<LobbyScreen>(scene);
                if (screen == null)
                {
                    Error("LobbyScreen is missing.", ref errors);
                }
                else
                {
                    Require(screen.characterPanelButton, "characterPanelButton", ref errors);
                    Require(screen.characterSelectionModal, "characterSelectionModal", ref errors);
                    Require(screen.characterSelectionCancelButton, "characterSelectionCancelButton", ref errors);
                    Require(screen.knightCharacterButton, "knightCharacterButton", ref errors);
                    Require(screen.archerCharacterButton, "archerCharacterButton", ref errors);
                    Require(screen.mageCharacterButton, "mageCharacterButton", ref errors);
                    Require(screen.knightCharacterChoiceIcon, "knightCharacterChoiceIcon", ref errors);
                    Require(screen.archerCharacterChoiceIcon, "archerCharacterChoiceIcon", ref errors);
                    Require(screen.mageCharacterChoiceIcon, "mageCharacterChoiceIcon", ref errors);
                    Require(screen.knightDisplayIcon, "knightDisplayIcon", ref errors);
                    Require(screen.archerDisplayIcon, "archerDisplayIcon", ref errors);
                    Require(screen.mageDisplayIcon, "mageDisplayIcon", ref errors);
                    Require(screen.characterNameText, "characterNameText", ref errors);
                    Require(screen.characterDescriptionText, "characterDescriptionText", ref errors);

                    ValidateSprite(screen.knightDisplayIcon, KnightSpritePath, "Knight display", ref errors);
                    ValidateSprite(screen.archerDisplayIcon, ArcherSpritePath, "Archer display", ref errors);
                    ValidateSprite(screen.mageDisplayIcon, MageSpritePath, "Mage display", ref errors);
                    ValidateChoice(screen.knightCharacterButton, KnightSpritePath, ref errors);
                    ValidateChoice(screen.archerCharacterButton, ArcherSpritePath, ref errors);
                    ValidateChoice(screen.mageCharacterButton, MageSpritePath, ref errors);
                    ValidateSprite(screen.knightCharacterChoiceIcon, KnightSpritePath, "Knight choice", ref errors);
                    ValidateSprite(screen.archerCharacterChoiceIcon, ArcherSpritePath, "Archer choice", ref errors);
                    ValidateSprite(screen.mageCharacterChoiceIcon, MageSpritePath, "Mage choice", ref errors);

                    if (screen.characterSelectionModal != null)
                    {
                        if (screen.characterSelectionModal.activeSelf) Error("Character Selection Modal must be saved inactive.", ref errors);
                        var dimmerRoot = FindDirect(screen.characterSelectionModal.transform, "Character Selection Dimmer");
                        var dimmerImage = dimmerRoot?.GetComponent<Image>();
                        var dimmerButton = dimmerRoot?.GetComponent<Button>();
                        if (dimmerImage == null || !dimmerImage.raycastTarget) Error("Character Selection Dimmer must block raycasts.", ref errors);
                        if (dimmerButton == null || screen.characterSelectionCancelButton != dimmerButton)
                        {
                            Error("Character Selection Dimmer must be assigned as the cancel button.", ref errors);
                        }
                        else if (dimmerButton.navigation.mode != Navigation.Mode.None)
                        {
                            Error("Character Selection Dimmer cancel button must not participate in navigation.", ref errors);
                        }
                    }

                    ValidateNavigation(screen.knightCharacterButton, screen.knightCharacterButton, screen.archerCharacterButton, ref errors);
                    ValidateNavigation(screen.archerCharacterButton, screen.knightCharacterButton, screen.mageCharacterButton, ref errors);
                    ValidateNavigation(screen.mageCharacterButton, screen.archerCharacterButton, screen.mageCharacterButton, ref errors);
                }

                var runtimeSource = File.ReadAllText("Assets/AreaSurvivors/Scripts/UI/LobbyScreen.cs");
                if (runtimeSource.Contains("new GameObject")) Error("LobbyScreen must not generate UI at runtime.", ref errors);
                if (errors > 0) throw new InvalidOperationException($"Lobby character selection validation failed with {errors} error(s).");

                WriteMarker(ValidatorMarkerPath);
                Debug.Log("Lobby character selection validation passed.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Image EnsureDisplayIcon(Transform parent, Image source, string name, Sprite sprite)
        {
            var existing = FindDirect(parent, name)?.GetComponent<Image>();
            if (existing != null) return existing;
            var clone = UnityEngine.Object.Instantiate(source.gameObject, parent).GetComponent<Image>();
            clone.gameObject.name = name;
            clone.sprite = sprite;
            clone.raycastTarget = false;
            return clone;
        }

        static Button EnsureChoiceButton(Transform parent, string name, Sprite sprite, Vector2 position)
        {
            var existing = FindDirect(parent, name)?.GetComponent<Button>();
            if (existing != null) return existing;

            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(UiBoxOutline), typeof(SelectOnPointerEnter), typeof(SceneAuthoredSelectionOutline));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(82f, 58f);

            var background = root.GetComponent<Image>();
            background.color = PanelColor;
            var button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.interactable = true;
            UiBoxOutline.Apply(root.transform, EdgeColor, 2f);

            var iconRoot = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconRoot.transform.SetParent(root.transform, false);
            var iconRect = iconRoot.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(48f, 48f);
            var icon = iconRoot.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            EnsureFocusOutline(root, rect.sizeDelta, 5f);
            return button;
        }

        static void EnsureFocusOutline(GameObject target, Vector2 size, float padding)
        {
            var focus = target.GetComponent<SceneAuthoredSelectionOutline>();
            if (focus == null) focus = target.AddComponent<SceneAuthoredSelectionOutline>();
            var existing = FindDirect(target.transform, "Focus Outline");
            if (existing != null)
            {
                focus.outlineRoot = existing.gameObject;
                return;
            }

            var root = new GameObject("Focus Outline", typeof(RectTransform));
            root.transform.SetParent(target.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size + Vector2.one * padding * 2f;
            CreateEdge(rect, "Top", new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(rect.sizeDelta.x, 4f));
            CreateEdge(rect, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(rect.sizeDelta.x, 4f));
            CreateEdge(rect, "Left", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, rect.sizeDelta.y));
            CreateEdge(rect, "Right", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(4f, rect.sizeDelta.y));
            focus.outlineRoot = root;
            root.SetActive(false);
        }

        static void CreateEdge(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edge.transform.SetParent(parent, false);
            var rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = edge.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        static RectTransform EnsureRect(Transform parent, string name, out bool created)
        {
            var existing = FindDirect(parent, name) as RectTransform;
            if (existing != null)
            {
                created = false;
                return existing;
            }
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            created = true;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void ConfigureNavigation(Button button, Selectable up, Selectable down)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = button;
            navigation.selectOnRight = button;
            button.navigation = navigation;
        }

        static void ValidateChoice(Button button, string spritePath, ref int errors)
        {
            if (button == null) return;
            if (button.GetComponent<SelectOnPointerEnter>() == null) Error(button.name + " lacks SelectOnPointerEnter.", ref errors);
            var focus = button.GetComponent<SceneAuthoredSelectionOutline>();
            if (focus == null || focus.outlineRoot == null) Error(button.name + " lacks a Scene-authored focus outline.", ref errors);
            var icon = FindDirect(button.transform, "Icon")?.GetComponent<Image>();
            ValidateSprite(icon, spritePath, button.name, ref errors);
        }

        static void ValidateSprite(Image image, string expectedPath, string label, ref int errors)
        {
            var expected = AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath);
            if (image == null || image.sprite != expected) Error(label + " has an unexpected sprite.", ref errors);
        }

        static void ValidateNavigation(Button button, Selectable expectedUp, Selectable expectedDown, ref int errors)
        {
            if (button == null) return;
            var navigation = button.navigation;
            if (navigation.mode != Navigation.Mode.Explicit || navigation.selectOnUp != expectedUp || navigation.selectOnDown != expectedDown || navigation.selectOnLeft != button || navigation.selectOnRight != button)
            {
                Error(button.name + " navigation can escape the character choices.", ref errors);
            }
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Sprite was not found: " + path);
            return sprite;
        }

        static Scene OpenScene(out bool openedHere)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.IsValid() || !scene.isLoaded;
            return openedHere ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : scene;
        }

        static T FindInScene<T>(Scene scene, string objectName = null) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var components = root.GetComponentsInChildren<T>(true);
                foreach (var component in components)
                {
                    if (component != null && (string.IsNullOrEmpty(objectName) || component.name == objectName)) return component;
                }
            }
            return null;
        }

        static Transform FindDirect(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        static void Require(UnityEngine.Object value, string label, ref int errors)
        {
            if (value == null) Error(label + " is not assigned.", ref errors);
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError("[Lobby Character Selection] " + message);
        }

        static void DeleteMarker(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        static void WriteMarker(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        }
    }
}
