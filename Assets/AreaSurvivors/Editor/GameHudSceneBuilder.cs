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
            characterImage.sprite = LoadGeneratedSprite("Knight");
            characterImage.preserveAspect = true;

            DestroyChild(root, "Weapon Frame");

            EnsureBar(root, "Player HP Bar", new Vector2(174f, -36f), new Vector2(190f, 24f), HpGreen, "45/45");
            EnsureBar(root, "Player XP Bar", new Vector2(174f, -72f), new Vector2(190f, 20f), HpBlue, "Lv.1");

            var statsRoot = splitPlayer != null
                ? canvas.transform.Find("Player Status") as RectTransform
                : root;
            if (statsRoot != null)
            {
                ConfigurePlayerStatColumn(statsRoot, splitPlayer != null);
                EnsureAdvancedStatBoxes(statsRoot, splitPlayer != null);
                RemoveWeaponStatBoxes(statsRoot);
            }

            DestroyChild(canvas.transform, "Weapon Status");

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Player Status HUD panel was created in the scene.");
        }

        public static void CreateRunResourcePanels()
        {
            ImportGeneratedSprite("SkullIcon");
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

            var stage = EnsurePanel(canvas.transform, "Stage Panel", new Vector2(-222f, -28f), new Vector2(118f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(stage, new Vector2(-222f, -28f), new Vector2(118f, 34f), new Vector2(0.5f, 1f));
            EnsureFrame(stage, stage.sizeDelta);
            var stageText = EnsureText(stage, "Label", "STAGE 1", 18, TextAnchor.MiddleCenter);
            Stretch(stageText.rectTransform);

            var kills = EnsurePanel(canvas.transform, "Kill Panel", new Vector2(82f, -28f), new Vector2(154f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(kills, new Vector2(82f, -28f), new Vector2(154f, 34f), new Vector2(0.5f, 1f));
            EnsureFrame(kills, kills.sizeDelta);
            ConfigureKillPanel(kills);

            EnsureTokenResourcePanel(canvas.transform);
            EnsureBossHud(canvas.transform);

            var oldBackplate = canvas.transform.Find("Run Stats Backplate");
            if (oldBackplate != null) oldBackplate.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Run stats and resource HUD panels were created in the scene.");
        }

        [MenuItem("Area Survivors/Rebuild/Restore Token HUD")]
        public static void RestoreTokenHud()
        {
            ImportGeneratedSprite("Token");
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            EnsureTokenResourcePanel(canvas.transform);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Token HUD panel was restored in the scene.");
        }

        [MenuItem("Area Survivors/Rebuild/Restore Weapon Status HUD")]
        public static void RestoreWeaponStatusHud()
        {
            var canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("HUD Canvas was not found.");
                return;
            }

            EnsureWeaponStatusPanels(canvas.transform);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("Weapon Status HUD panel was restored in the scene.");
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

        static void ConfigureKillPanel(RectTransform root)
        {
            var icon = EnsureImage(root, "Icon", new Vector2(24f, 24f));
            icon.sprite = LoadGeneratedSprite("SkullIcon");
            icon.preserveAspect = true;
            AddIconOutline(icon);
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(22f, 0f);

            var killText = EnsureText(root, "Label", "0", 20, TextAnchor.MiddleLeft);
            killText.rectTransform.anchorMin = Vector2.zero;
            killText.rectTransform.anchorMax = Vector2.one;
            killText.rectTransform.offsetMin = new Vector2(42f, 0f);
            killText.rectTransform.offsetMax = new Vector2(-8f, 0f);
        }

        static RectTransform EnsureTokenResourcePanel(Transform parent)
        {
            var root = EnsurePanel(parent, "Token Resource", new Vector2(442f, -28f), new Vector2(98f, 34f), new Vector2(0.5f, 1f));
            SetAnchored(root, new Vector2(442f, -28f), new Vector2(98f, 34f), new Vector2(0.5f, 1f));
            EnsureFrame(root, root.sizeDelta);

            var icon = EnsureImage(root, "Icon", new Vector2(24f, 24f));
            icon.sprite = LoadGeneratedSprite("Token");
            icon.preserveAspect = true;
            AddIconOutline(icon);
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(18f, 0f);

            var text = EnsureText(root, "Amount", "0", 18, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
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

        static void EnsureWeaponStatusPanels(Transform parent)
        {
            AssetDatabase.Refresh();
            ImportGeneratedSprite("ArrowHudIcon");
            ImportGeneratedSprite("FireballHudIcon");
            DestroyChild(parent, "Weapon Status");
            var playerStats = parent.Find("Player Status") as RectTransform;
            if (playerStats != null)
            {
                playerStats.sizeDelta = new Vector2(138f, 140f);
                EnsureFrame(playerStats, playerStats.sizeDelta);
                DestroyChild(playerStats, "Work Text Box");
                DestroyChild(playerStats, "Resource Text Box");
            }

            EnsureWeaponPanel(parent, "Slash Weapon Status", "スラッシュ", "Slash_0", new Vector2(14f, -277f), new Vector2(16f, 16f), new Vector2(7f, -4f), new[]
            {
                new WeaponRow("Attack Row", "攻撃力", "0", StatIconCatalog.Attack),
                new WeaponRow("Cooldown Row", "攻撃間隔", "0.00s", StatIconCatalog.Cooldown),
                new WeaponRow("Knockback Row", "ノックバック", "0", StatIconCatalog.Knockback),
                new WeaponRow("Range Row", "攻撃範囲", "0", StatIconCatalog.Range)
            });
            EnsureWeaponPanel(parent, "Arrow Weapon Status", "弓", "ArrowHudIcon", new Vector2(14f, -403f), new Vector2(18f, 18f), new Vector2(6f, -3f), new[]
            {
                new WeaponRow("Attack Row", "攻撃力", "-", StatIconCatalog.Attack),
                new WeaponRow("Cooldown Row", "攻撃間隔", "-", StatIconCatalog.Cooldown),
                new WeaponRow("Projectile Count Row", "矢の本数", "-", StatIconCatalog.Projectile),
                new WeaponRow("Range Row", "射程", "-", StatIconCatalog.Range)
            });
            EnsureWeaponPanel(parent, "Fireball Weapon Status", "火の玉", "FireballHudIcon", new Vector2(14f, -529f), new Vector2(18f, 18f), new Vector2(6f, -3f), new[]
            {
                new WeaponRow("Attack Row", "攻撃力", "-", StatIconCatalog.Attack),
                new WeaponRow("Cooldown Row", "攻撃間隔", "-", StatIconCatalog.Cooldown),
                new WeaponRow("Explosion Row", "爆発範囲", "-", StatIconCatalog.Range),
                new WeaponRow("Range Row", "射程", "-", StatIconCatalog.Range)
            });
        }

        static RectTransform EnsureWeaponPanel(Transform parent, string name, string titleText, string titleIcon, Vector2 position, Vector2 titleIconSize, Vector2 titleIconPosition, WeaponRow[] rows)
        {
            var root = EnsurePanel(parent, name, position, new Vector2(138f, 116f), Vector2.up, PanelColor);
            SetAnchored(root, position, new Vector2(138f, 116f), Vector2.up);
            EnsureFrame(root, root.sizeDelta);
            EnsureHeaderIcon(root, titleIcon, titleIconSize, titleIconPosition);

            var title = EnsureText(root, "Title", titleText, 13, TextAnchor.MiddleCenter);
            title.color = new Color(0.96f, 0.90f, 0.62f, 1f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            title.rectTransform.offsetMin = new Vector2(24f, title.rectTransform.offsetMin.y);
            title.rectTransform.offsetMax = new Vector2(-6f, title.rectTransform.offsetMax.y);
            title.rectTransform.sizeDelta = new Vector2(0f, 18f);

            for (int i = 0; i < rows.Length; i++)
            {
                EnsureWeaponRow(root, rows[i], new Vector2(6f, -24f - i * 22f));
            }

            return root;
        }

        static void EnsureHeaderIcon(RectTransform parent, string spriteName, Vector2 size, Vector2 position)
        {
            var icon = EnsureImage(parent, "Icon", size);
            icon.sprite = LoadGeneratedSprite(spriteName);
            icon.preserveAspect = true;
            AddIconOutline(icon);
            icon.rectTransform.anchorMin = new Vector2(0f, 1f);
            icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.anchoredPosition = position;
        }

        static void EnsureWeaponRow(RectTransform parent, WeaponRow row, Vector2 position)
        {
            var box = EnsurePanel(parent, row.name, position, new Vector2(126f, 20f), Vector2.up, SlotColor);
            SetAnchored(box, position, new Vector2(126f, 20f), Vector2.up);
            EnsureFrame(box, box.sizeDelta);
            EnsureRowIcon(box, row.icon);

            var label = EnsureText(box, "Name", row.label, 11, TextAnchor.MiddleLeft);
            SetColumns(label.rectTransform, 0f, 0.72f, 19f, -2f);

            var value = EnsureText(box, "Value", row.value, 11, TextAnchor.MiddleRight);
            SetColumns(value.rectTransform, 0.72f, 1f, 2f, -5f);

            var divider = box.Find("Divider");
            Image dividerImage;
            if (divider == null)
            {
                var dividerObject = new GameObject("Divider");
                dividerObject.transform.SetParent(box, false);
                dividerImage = dividerObject.AddComponent<Image>();
            }
            else
            {
                dividerImage = divider.GetComponent<Image>();
                if (dividerImage == null) dividerImage = divider.gameObject.AddComponent<Image>();
            }

            ConfigureDivider(dividerImage, 0.72f);
        }

        readonly struct WeaponRow
        {
            public readonly string name;
            public readonly string label;
            public readonly string value;
            public readonly string icon;

            public WeaponRow(string name, string label, string value, string icon)
            {
                this.name = name;
                this.label = label;
                this.value = value;
                this.icon = icon;
            }
        }

        static void ConfigurePlayerStatColumn(RectTransform root, bool splitLayout)
        {
            if (splitLayout)
            {
                root.sizeDelta = new Vector2(138f, 140f);
                EnsureFrame(root, root.sizeDelta);
                EnsureStatBox(root, "Speed Text", new Vector2(6f, -6f), "移動速度", "2.5", StatIconCatalog.MoveSpeed);
                EnsureStatBox(root, "Paint Text", new Vector2(6f, -28f), "塗り範囲", "3", StatIconCatalog.Paint);
                EnsureStatBox(root, "Revive Text", new Vector2(6f, -50f), "復活時間", "6.0s", StatIconCatalog.Revive);
                return;
            }

            EnsureStatBox(root, "Speed Text", new Vector2(6f, -162f), "移動速度", "2.5", StatIconCatalog.MoveSpeed);
            EnsureStatBox(root, "Paint Text", new Vector2(6f, -192f), "塗り範囲", "3", StatIconCatalog.Paint);
            EnsureStatBox(root, "Revive Text", new Vector2(6f, -222f), "復活時間", "6.0s", StatIconCatalog.Revive);
        }

        static void EnsureAdvancedStatBoxes(RectTransform root, bool splitLayout)
        {
            if (splitLayout)
            {
                EnsureStatBox(root, "Defense Text", new Vector2(6f, -72f), "防御力", "0", StatIconCatalog.Defense);
                EnsureStatBox(root, "Xp Gain Text", new Vector2(6f, -94f), "経験値倍率", "1.0x", StatIconCatalog.Xp);
                EnsureStatBox(root, "Regen Text", new Vector2(6f, -116f), "自動回復", "0", StatIconCatalog.Regen);
                DestroyChild(root, "Work Text Box");
                DestroyChild(root, "Resource Text Box");
                return;
            }

            EnsureStatBox(root, "Defense Text", new Vector2(6f, -342f), "防御力", "0", StatIconCatalog.Defense);
            EnsureStatBox(root, "Xp Gain Text", new Vector2(6f, -372f), "経験値倍率", "1.0x", StatIconCatalog.Xp);
            EnsureStatBox(root, "Regen Text", new Vector2(6f, -402f), "自動回復", "0", StatIconCatalog.Regen);
            DestroyChild(root, "Work Text Box");
            DestroyChild(root, "Resource Text Box");
        }

        static void RemoveWeaponStatBoxes(RectTransform root)
        {
            DestroyChild(root, "Weapon Level Text Box");
            DestroyChild(root, "Attack Text Box");
            DestroyChild(root, "Cooldown Text Box");
            DestroyChild(root, "Projectile Text Box");
            DestroyChild(root, "Range Text Box");
            DestroyChild(root, "Knockback Text Box");
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

        static Button EnsureButton(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var existing = parent.Find(name);
            var button = existing != null ? existing.GetComponent<Button>() : null;
            Image image;
            if (button == null)
            {
                image = existing != null ? existing.GetComponent<Image>() : null;
                if (image == null)
                {
                    image = new GameObject(name).AddComponent<Image>();
                    image.transform.SetParent(parent, false);
                }
                button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
            }
            else
            {
                image = button.GetComponent<Image>();
                if (image == null) image = button.gameObject.AddComponent<Image>();
                button.targetGraphic = image;
            }

            image.raycastTarget = true;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return button;
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

        static void EnsureStatBox(RectTransform parent, string name, Vector2 position, string label, string valueText, string iconResource)
        {
            var box = EnsurePanel(parent, name + " Box", position, new Vector2(126f, 20f), Vector2.up, SlotColor);
            SetAnchored(box, position, new Vector2(126f, 20f), Vector2.up);
            EnsureFrame(box, box.sizeDelta);
            EnsureRowIcon(box, iconResource);
            var oldLabel = box.Find("Label");
            var nameText = EnsureText(box, oldLabel != null ? "Label" : "Name", label, 11, TextAnchor.MiddleLeft);
            nameText.gameObject.name = "Name";
            SetColumns(nameText.rectTransform, 0f, 0.72f, 19f, -2f);

            var value = EnsureText(box, "Value", valueText, 11, TextAnchor.MiddleRight);
            SetColumns(value.rectTransform, 0.72f, 1f, 2f, -5f);

            var divider = box.Find("Divider");
            Image dividerImage;
            if (divider == null)
            {
                var dividerObject = new GameObject("Divider");
                dividerObject.transform.SetParent(box, false);
                dividerImage = dividerObject.AddComponent<Image>();
            }
            else
            {
                dividerImage = divider.GetComponent<Image>();
                if (dividerImage == null) dividerImage = divider.gameObject.AddComponent<Image>();
            }

            ConfigureDivider(dividerImage, 0.72f);
        }

        static void EnsureRowIcon(RectTransform parent, string iconResource)
        {
            var icon = EnsureImage(parent, "Icon", new Vector2(14f, 14f));
            icon.sprite = StatIconCatalog.Load(iconResource);
            icon.preserveAspect = true;
            AddIconOutline(icon);
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
        }

        static void ConfigureDivider(Image image, float anchorX)
        {
            image.color = new Color(0.58f, 0.68f, 0.40f, 0.65f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(anchorX, 0.15f);
            rect.anchorMax = new Vector2(anchorX, 0.85f);
            rect.sizeDelta = new Vector2(1f, 0f);
            rect.anchoredPosition = Vector2.zero;
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

        static void SetColumns(RectTransform rect, float minX, float maxX, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(right, 0f);
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
            var path = "Assets/AreaSurvivors/Sprites/Generated/" + name + ".png";
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

        static Sprite LoadGeneratedSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AreaSurvivors/Sprites/Generated/" + name + ".png");
        }
    }
}
