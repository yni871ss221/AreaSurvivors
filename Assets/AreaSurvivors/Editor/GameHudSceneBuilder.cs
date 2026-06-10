using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class GameHudSceneBuilder
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.045f, 0.72f);
        static readonly Color SlotColor = new Color(0.09f, 0.16f, 0.12f, 0.92f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        static readonly Color HpBlue = new Color(0.22f, 0.62f, 1f, 0.96f);
        static readonly Color HpGreen = new Color(0.36f, 0.88f, 0.36f, 0.98f);

        [MenuItem("AreaSurvivors/HUD/Create Player Status Panel")]
        public static void CreatePlayerStatusPanel()
        {
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            var splitPlayer = canvas.transform.Find("Player") as RectTransform;
            var root = splitPlayer != null
                ? splitPlayer
                : EnsurePanel(canvas.transform, "Player Status", new Vector2(14f, -12f), new Vector2(390f, 318f), Vector2.up);
            EnsureFrame(root, root.sizeDelta);

            var characterFrame = EnsurePanel(root, "Character Frame", new Vector2(18f, -36f), new Vector2(70f, 70f), Vector2.up, SlotColor);
            EnsureFrame(characterFrame, characterFrame.sizeDelta);
            var characterImage = EnsureImage(characterFrame, "Character Image", new Vector2(58f, 58f));
            characterImage.sprite = Resources.Load<Sprite>("Generated/Knight");
            characterImage.preserveAspect = true;

            var weaponFrame = EnsurePanel(root, "Weapon Frame", new Vector2(96f, -42f), new Vector2(58f, 58f), Vector2.up, SlotColor);
            EnsureFrame(weaponFrame, weaponFrame.sizeDelta);
            var weaponImage = EnsureImage(weaponFrame, "Weapon Image", new Vector2(48f, 48f));
            weaponImage.sprite = Resources.Load<Sprite>("Generated/Slash_0");
            weaponImage.preserveAspect = true;

            EnsureBar(root, "Player HP Bar", new Vector2(174f, -36f), new Vector2(190f, 24f), HpGreen, "45/45");
            EnsureBar(root, "Player XP Bar", new Vector2(174f, -72f), new Vector2(190f, 20f), HpBlue, "Lv.1");

            var statsRoot = splitPlayer != null
                ? canvas.transform.Find("Player Status") as RectTransform
                : root;
            if (statsRoot != null)
            {
                ConfigurePlayerStatColumn(statsRoot, splitPlayer != null);
                EnsureAdvancedStatBoxes(statsRoot, splitPlayer != null);
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Player Status HUD panel was created in the scene.");
        }

        [MenuItem("AreaSurvivors/HUD/Normalize Game Player Status Panel")]
        public static void NormalizeGamePlayerStatusPanel()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath);
            CreatePlayerStatusPanel();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Game Player Status HUD panel was normalized in 05_Game.");
        }

        [MenuItem("AreaSurvivors/HUD/Create Run Resource Panels")]
        public static void CreateRunResourcePanels()
        {
            ImportGeneratedSprite("WoodIcon");
            ImportGeneratedSprite("StoneIcon");
            ImportGeneratedSprite("Pickaxe");
            ImportGeneratedSprite("Axe");
            ImportGeneratedSprite("Hammer");
            ImportGeneratedSprite("Token");
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            var timer = EnsurePanel(canvas.transform, "Timer Panel", new Vector2(-78f, -28f), new Vector2(142f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(timer, new Vector2(-78f, -28f), new Vector2(142f, 34f), new Vector2(0.5f, 1f));
            EnsureFrame(timer, timer.sizeDelta);
            var timerText = EnsureText(timer, "Label", "00:00", 20, TextAnchor.MiddleCenter);
            Stretch(timerText.rectTransform);

            var kills = EnsurePanel(canvas.transform, "Kill Panel", new Vector2(82f, -28f), new Vector2(154f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(kills, new Vector2(82f, -28f), new Vector2(154f, 34f), new Vector2(0.5f, 1f));
            EnsureFrame(kills, kills.sizeDelta);
            var killText = EnsureText(kills, "Label", "撃破 0", 20, TextAnchor.MiddleCenter);
            Stretch(killText.rectTransform);

            var wood = EnsureResourcePanel(canvas.transform, "Wood Resource", new Vector2(222f, -28f), Resources.Load<Sprite>("Generated/WoodIcon"), "100");
            var stone = EnsureResourcePanel(canvas.transform, "Stone Resource", new Vector2(332f, -28f), Resources.Load<Sprite>("Generated/StoneIcon"), "100");
            var token = EnsureResourcePanel(canvas.transform, "Token Resource", new Vector2(442f, -28f), Resources.Load<Sprite>("Generated/Token"), "0");
            EnsureFrame(wood, wood.sizeDelta);
            EnsureFrame(stone, stone.sizeDelta);
            EnsureFrame(token, token.sizeDelta);
            EnsureBossHud(canvas.transform);

            var oldBackplate = canvas.transform.Find("Run Stats Backplate");
            if (oldBackplate != null) oldBackplate.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Run stats and resource HUD panels were created in the scene.");
        }

        [MenuItem("AreaSurvivors/HUD/Normalize Enemy Spawn HUD")]
        public static void NormalizeEnemySpawnHud()
        {
            ImportGeneratedSprite("Token");
            var scene = EditorSceneManager.OpenScene(GameScenePath);
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            var token = EnsureResourcePanel(canvas.transform, "Token Resource", new Vector2(442f, -28f), Resources.Load<Sprite>("Generated/Token"), "0");
            EnsureFrame(token, token.sizeDelta);
            EnsureBossHud(canvas.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Enemy spawn HUD widgets were normalized in 05_Game.");
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

        [MenuItem("AreaSurvivors/HUD/Convert UI Frames To Outline Components")]
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

        static RectTransform EnsureResourcePanel(Transform parent, string name, Vector2 position, Sprite iconSprite, string amount)
        {
            var root = EnsurePanel(parent, name, position, new Vector2(98f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(root, position, new Vector2(98f, 34f), new Vector2(0.5f, 1f));
            var icon = EnsureImage(root, "Icon", new Vector2(24f, 24f));
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            AddIconOutline(icon);
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(18f, 0f);
            var text = EnsureText(root, "Amount", amount, 18, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(36f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);
            return root;
        }

        static void EnsureBossHud(Transform parent)
        {
            var boss = EnsurePanel(parent, "Boss Status", new Vector2(0f, -72f), new Vector2(520f, 58f), new Vector2(0.5f, 1f), new Color(0.035f, 0.03f, 0.035f, 0.78f));
            SetAnchored(boss, new Vector2(0f, -72f), new Vector2(520f, 58f), new Vector2(0.5f, 1f));
            EnsureFrame(boss, boss.sizeDelta);
            var name = EnsureText(boss, "Boss Name", "オークキング", 20, TextAnchor.MiddleCenter);
            name.color = new Color(1f, 0.78f, 0.62f, 1f);
            name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            name.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 13f);
            name.rectTransform.sizeDelta = new Vector2(500f, 24f);

            var bar = EnsurePanel(boss, "Boss HP Bar", new Vector2(0f, -12f), new Vector2(470f, 20f), new Vector2(0.5f, 0.5f), new Color(0.02f, 0.015f, 0.015f, 0.9f));
            var fill = EnsureImage(bar, "Fill", bar.sizeDelta);
            fill.color = new Color(0.86f, 0.08f, 0.06f, 0.98f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var label = EnsureText(bar, "Label", "0/0", 12, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            boss.gameObject.SetActive(false);

            var announcement = EnsurePanel(parent, "Announcement", new Vector2(0f, -136f), new Vector2(520f, 48f), new Vector2(0.5f, 1f), new Color(0.03f, 0.02f, 0.02f, 0.78f));
            SetAnchored(announcement, new Vector2(0f, -136f), new Vector2(520f, 48f), new Vector2(0.5f, 1f));
            EnsureFrame(announcement, announcement.sizeDelta);
            var announcementLabel = EnsureText(announcement, "Label", "", 28, TextAnchor.MiddleCenter);
            announcementLabel.color = new Color(1f, 0.84f, 0.55f, 1f);
            Stretch(announcementLabel.rectTransform);
            announcement.gameObject.SetActive(false);
        }

        static void ConfigurePlayerStatColumn(RectTransform root, bool splitLayout)
        {
            if (splitLayout)
            {
                root.sizeDelta = new Vector2(Mathf.Max(root.sizeDelta.x, 112f), Mathf.Max(root.sizeDelta.y, 412f));
                EnsureFrame(root, root.sizeDelta);
                EnsureStatBox(root, "Attack Text", new Vector2(56f, -18f), "攻撃: 10");
                EnsureStatBox(root, "Cooldown Text", new Vector2(56f, -48f), "間隔: 0.9s");
                EnsureStatBox(root, "Speed Text", new Vector2(56f, -78f), "速度: 2.5");
                EnsureStatBox(root, "Paint Text", new Vector2(56f, -108f), "塗り: 3");
                EnsureStatBox(root, "Revive Text", new Vector2(56f, -138f), "復活: 6.0s");
                EnsureStatBox(root, "Projectile Text", new Vector2(56f, -168f), "弾速: 11.5");
                EnsureStatBox(root, "Range Text", new Vector2(56f, -198f), "範囲: 1.1");
                return;
            }

            EnsureStatBox(root, "Attack Text", new Vector2(58f, -102f), "攻撃: 10");
            EnsureStatBox(root, "Cooldown Text", new Vector2(58f, -132f), "間隔: 0.9s");
            EnsureStatBox(root, "Speed Text", new Vector2(58f, -162f), "速度: 2.5");
            EnsureStatBox(root, "Paint Text", new Vector2(58f, -192f), "塗り: 3");
            EnsureStatBox(root, "Revive Text", new Vector2(58f, -222f), "復活: 6.0s");
            EnsureStatBox(root, "Projectile Text", new Vector2(58f, -252f), "弾速: 11.5");
            EnsureStatBox(root, "Range Text", new Vector2(58f, -282f), "範囲: 1.1");
        }

        static void EnsureAdvancedStatBoxes(RectTransform root, bool splitLayout)
        {
            if (splitLayout)
            {
                EnsureStatBox(root, "Knockback Text", new Vector2(56f, -228f), "ノック: 1");
                EnsureStatBox(root, "Defense Text", new Vector2(56f, -258f), "防御: 0");
                EnsureStatBox(root, "Xp Gain Text", new Vector2(56f, -288f), "経験: 1.0x");
                EnsureStatBox(root, "Regen Text", new Vector2(56f, -318f), "回復: 0");
                EnsureStatBox(root, "Work Text", new Vector2(56f, -348f), "作業: 1.0x");
                EnsureStatBox(root, "Resource Text", new Vector2(56f, -378f), "資源: +0");
                return;
            }

            EnsureStatBox(root, "Knockback Text", new Vector2(58f, -312f), "ノック: 1");
            EnsureStatBox(root, "Defense Text", new Vector2(58f, -342f), "防御: 0");
            EnsureStatBox(root, "Xp Gain Text", new Vector2(58f, -372f), "経験: 1.0x");
            EnsureStatBox(root, "Regen Text", new Vector2(58f, -402f), "回復: 0");
            EnsureStatBox(root, "Work Text", new Vector2(58f, -432f), "作業: 1.0x");
            EnsureStatBox(root, "Resource Text", new Vector2(58f, -462f), "資源: +0");
        }

        static RectTransform EnsurePanel(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor, Color? color = null)
        {
            var existing = parent.Find(name);
            var rect = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                var image = new GameObject(name).AddComponent<Image>();
                image.transform.SetParent(parent, false);
                image.color = color ?? PanelColor;
                image.raycastTarget = false;
                rect = image.rectTransform;
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = anchor;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            return rect;
        }

        static Image EnsureImage(Transform parent, string name, Vector2 size)
        {
            var existing = parent.Find(name);
            var image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                image = new GameObject(name).AddComponent<Image>();
                image.transform.SetParent(parent, false);
            }
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        static void EnsureBar(RectTransform parent, string name, Vector2 position, Vector2 size, Color fillColor, string label)
        {
            var root = EnsurePanel(parent, name, position, size, Vector2.up, new Color(0.02f, 0.025f, 0.025f, 0.88f));
            var fill = EnsureImage(root, "Fill", size);
            fill.color = fillColor;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var text = EnsureText(root, "Label", label, 13, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
        }

        static void EnsureStatBox(RectTransform parent, string name, Vector2 position, string label)
        {
            var box = EnsurePanel(parent, name + " Box", position, new Vector2(104f, 26f), Vector2.up, SlotColor);
            EnsureFrame(box, box.sizeDelta);
            var text = EnsureText(box, "Label", label, 13, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = new Vector2(96f, 22f);
        }

        static Text EnsureText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var existing = parent.Find(name);
            var text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                text = new GameObject(name).AddComponent<Text>();
                text.transform.SetParent(parent, false);
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.color = Color.white;
                text.raycastTarget = false;
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static void EnsureFrame(Transform parent, Vector2 size)
        {
            UiBoxOutline.Apply(parent, EdgeColor, 2f);
        }

        static void AddIconOutline(Image image)
        {
            if (image == null) return;
            var outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void ImportGeneratedSprite(string name)
        {
            var path = "Assets/AreaSurvivors/Resources/Generated/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
