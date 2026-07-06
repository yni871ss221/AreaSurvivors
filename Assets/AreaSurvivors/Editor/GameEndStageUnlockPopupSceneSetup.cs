using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class GameEndStageUnlockPopupSceneSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/06_GameEnd.unity";
        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.58f);
        static readonly Color PanelColor = new Color(0.035f, 0.06f, 0.05f, 0.96f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.96f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.95f);
        static readonly Color AccentText = new Color(0.96f, 0.90f, 0.68f, 1f);
        static readonly Color BodyText = new Color(0.86f, 0.93f, 0.88f, 1f);

        [MenuItem("AreaSurvivors/Setup/Add GameEnd Stage Unlock Popup")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var canvas = FindSceneTransform("Game Over UI") as RectTransform;
            var controller = FindSceneTransform("06_GameEnd Controller");
            if (canvas == null || controller == null)
            {
                Debug.LogError("GameEnd stage unlock popup setup failed: required scene objects were not found.");
                return;
            }

            var root = EnsureRect(canvas, "Stage Unlock Popup");
            Stretch(root);
            root.SetAsLastSibling();
            var rootGroup = EnsureComponent<CanvasGroup>(root.gameObject);
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            var dim = EnsureImage(root, "Dim Panel", DimColor);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var panel = EnsureImage(root, "Popup Panel", PanelColor);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(620f, 300f));
            panel.raycastTarget = true;
            UiBoxOutline.Apply(panel.transform, EdgeColor, 3f);

            var header = EnsureText(panel.rectTransform, "Header", "NEW STAGE", 26, new Vector2(0f, 112f), new Vector2(520f, 34f), AccentText);
            header.gameObject.SetActive(true);
            var message = EnsureText(panel.rectTransform, "Message", "ステージ2が解放されました", 30, new Vector2(0f, -36f), new Vector2(540f, 46f), Color.white);
            message.fontStyle = FontStyle.Bold;
            message.gameObject.SetActive(true);
            var missionComplete = EnsureText(panel.rectTransform, "Mission Complete Text", "MISSION COMPLETE", 42, new Vector2(0f, 8f), new Vector2(560f, 78f), new Color(1f, 0.86f, 0.18f, 1f));
            missionComplete.fontStyle = FontStyle.Bold;
            missionComplete.gameObject.SetActive(false);

            var stage2Icon = EnsureBossIcon(panel.rectTransform, "Stage 2 Boss Icon", "Assets/AreaSurvivors/Sprites/Generated/Walk/EnemyGoblinLord/Down_1.png");
            var stage3Icon = EnsureBossIcon(panel.rectTransform, "Stage 3 Boss Icon", "Assets/AreaSurvivors/Sprites/Generated/Walk/EnemyLich/Down_1.png");
            var stage4Icon = EnsureBossIcon(panel.rectTransform, "Stage 4 Boss Icon", "Assets/AreaSurvivors/Sprites/Generated/Walk/EnemyDragon/Down_1.png");

            var okButton = EnsureButton(panel.rectTransform, "OK Button", "OK", new Vector2(0f, -112f), new Vector2(180f, 52f));
            root.gameObject.SetActive(false);

            var screen = controller.GetComponent<GameOverScreen>();
            var animator = controller.GetComponent<GameOverIntroAnimator>();
            if (screen != null)
            {
                screen.stageUnlockPopupRoot = root.gameObject;
                screen.stageUnlockHeaderText = header;
                screen.stageUnlockMessageText = message;
                screen.missionCompleteText = missionComplete;
                screen.stageUnlockOkButton = okButton;
                screen.stageUnlockBossIcons = new[]
                {
                    new GameOverScreen.StageUnlockBossIconBinding { stage = 2, icon = stage2Icon.gameObject },
                    new GameOverScreen.StageUnlockBossIconBinding { stage = 3, icon = stage3Icon.gameObject },
                    new GameOverScreen.StageUnlockBossIconBinding { stage = 4, icon = stage4Icon.gameObject },
                };
                EditorUtility.SetDirty(screen);
            }

            if (animator != null)
            {
                animator.stageUnlockPopupItem = new GameOverIntroAnimator.AnimatedItem
                {
                    rect = root,
                    group = rootGroup,
                    extraGroups = new CanvasGroup[0],
                    extraRects = new RectTransform[0]
                };
                animator.stageUnlockOkButton = okButton;
                animator.missionCompleteTextRect = missionComplete.rectTransform;
                animator.stageUnlockPopupDuration = 0.45f;
                EditorUtility.SetDirty(animator);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("GameEnd stage unlock popup setup complete.");
        }

        static Image EnsureBossIcon(RectTransform parent, string name, string spritePath)
        {
            var icon = EnsureImage(parent, name, Color.white);
            SetRect(icon.rectTransform, new Vector2(0f, 50f), new Vector2(108f, 108f));
            icon.preserveAspect = true;
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            icon.gameObject.SetActive(false);
            return icon;
        }

        static Button EnsureButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var rect = EnsureRect(parent, name);
            SetRect(rect, position, size);
            var image = EnsureComponent<Image>(rect.gameObject);
            image.color = ButtonColor;
            image.raycastTarget = true;
            var button = EnsureComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            UiBoxOutline.Apply(rect, EdgeColor, 2f);
            EnsureText(rect, "Label", label, 23, Vector2.zero, size, Color.white);
            return button;
        }

        static Text EnsureText(RectTransform parent, string name, string value, int fontSize, Vector2 position, Vector2 size, Color color)
        {
            var rect = EnsureRect(parent, name);
            SetRect(rect, position, size);
            var text = EnsureComponent<Text>(rect.gameObject);
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static Image EnsureImage(RectTransform parent, string name, Color color)
        {
            var rect = EnsureRect(parent, name);
            var image = EnsureComponent<Image>(rect.gameObject);
            image.color = color;
            return image;
        }

        static RectTransform EnsureRect(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var rect = new GameObject(name).AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static Transform FindSceneTransform(string name)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null || transform.name != name) continue;
                if (!transform.gameObject.scene.IsValid() || transform.gameObject.scene.path != ScenePath) continue;
                return transform;
            }

            return null;
        }
    }
}
