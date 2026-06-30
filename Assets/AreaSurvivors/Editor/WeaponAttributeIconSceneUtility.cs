using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class WeaponAttributeIconSceneUtility
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string GeneratedSpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/";
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";

        [MenuItem("Area Survivors/UI/Apply Weapon Attribute Icons")]
        public static void Apply()
        {
            GenerateAndImportIcons();
            UpdateGeneratedSpriteCatalog();
            ApplyGameSceneIcons();
            WeaponBookSceneSetup.Apply();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Weapon attribute icons were generated and applied.");
        }

        public static void GenerateAndImportIcons()
        {
            Directory.CreateDirectory(Path.Combine(GeneratedSpriteRoot, "UI"));
            CreateFallbackIconIfMissing(WeaponAttributeCatalog.MeleeIcon, new Color32(180, 45, 36, 255), DrawSword);
            CreateFallbackIconIfMissing(WeaponAttributeCatalog.RangedIcon, new Color32(44, 150, 70, 255), DrawBow);
            CreateFallbackIconIfMissing(WeaponAttributeCatalog.MagicIcon, new Color32(126, 64, 190, 255), DrawStaff);
            CreateFallbackIconIfMissing(WeaponAttributeCatalog.DefenseIcon, new Color32(48, 98, 185, 255), DrawShield);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ImportIcon(WeaponAttributeCatalog.MeleeIcon);
            ImportIcon(WeaponAttributeCatalog.RangedIcon);
            ImportIcon(WeaponAttributeCatalog.MagicIcon);
            ImportIcon(WeaponAttributeCatalog.DefenseIcon);
        }

        static void CreateFallbackIconIfMissing(string iconResource, Color32 panelColor, System.Action<Texture2D> drawSymbol)
        {
            if (File.Exists(GeneratedSpriteRoot + iconResource + ".png")) return;
            CreateIcon(iconResource, panelColor, drawSymbol);
        }

        public static void UpdateGeneratedSpriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("GeneratedSpriteCatalog.asset was not found.");
                return;
            }

            var entries = catalog.entries != null
                ? catalog.entries.ToList()
                : new List<GeneratedSpriteCatalog.Entry>();

            Upsert(entries, WeaponAttributeCatalog.MeleeIcon);
            Upsert(entries, WeaponAttributeCatalog.RangedIcon);
            Upsert(entries, WeaponAttributeCatalog.MagicIcon);
            Upsert(entries, WeaponAttributeCatalog.DefenseIcon);
            Upsert(entries, "Shield");
            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        public static WeaponAttributeIconSet EnsureIconSet(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Vector2 anchor,
            WeaponAttributeType activeType,
            bool showActive)
        {
            var root = parent.Find(name);
            RectTransform rect;
            WeaponAttributeIconSet iconSet;
            if (root == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                rect = go.GetComponent<RectTransform>();
                iconSet = go.AddComponent<WeaponAttributeIconSet>();
            }
            else
            {
                rect = root.GetComponent<RectTransform>();
                if (rect == null) rect = root.gameObject.AddComponent<RectTransform>();
                iconSet = root.GetComponent<WeaponAttributeIconSet>();
                if (iconSet == null) iconSet = root.gameObject.AddComponent<WeaponAttributeIconSet>();
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            iconSet.meleeIcon = EnsureIconImage(rect, "Melee Icon", WeaponAttributeCatalog.MeleeIcon, size);
            iconSet.rangedIcon = EnsureIconImage(rect, "Ranged Icon", WeaponAttributeCatalog.RangedIcon, size);
            iconSet.magicIcon = EnsureIconImage(rect, "Magic Icon", WeaponAttributeCatalog.MagicIcon, size);
            iconSet.defenseIcon = EnsureIconImage(rect, "Defense Icon", WeaponAttributeCatalog.DefenseIcon, size);

            if (showActive && activeType != WeaponAttributeType.None)
            {
                iconSet.gameObject.SetActive(true);
                iconSet.Show(activeType);
            }
            else
            {
                iconSet.Hide();
                iconSet.gameObject.SetActive(showActive);
            }

            EditorUtility.SetDirty(iconSet);
            return iconSet;
        }

        public static void ApplyGameSceneIcons()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var gameManager = Object.FindObjectOfType<GameManager>(true);
            if (gameManager != null && gameManager.upgradeButtons != null)
            {
                EnsureLevelUpPanelLayout(gameManager);
                foreach (var button in gameManager.upgradeButtons)
                {
                    EnsureLevelUpButtonIcons(button);
                }
            }

            var hud = FindRoot(scene, "HUD");
            if (hud != null)
            {
                EnsureHudWeaponTypeIcon(hud.transform, "Slash Weapon Status", WeaponAttributeType.Melee);
                EnsureHudWeaponTypeIcon(hud.transform, "Arrow Weapon Status", WeaponAttributeType.Ranged);
                EnsureHudWeaponTypeIcon(hud.transform, "Fireball Weapon Status", WeaponAttributeType.Magic);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void EnsureLevelUpButtonIcons(Button button)
        {
            if (button == null) return;
            var upgradeIcon = EnsureSceneImage(button.transform, "Upgrade Icon", new Vector2(-202f, -22f), new Vector2(28f, 28f), new Vector2(0.5f, 0.5f));
            upgradeIcon.sprite = GeneratedSpriteLoader.Load(StatIconCatalog.Attack);
            upgradeIcon.gameObject.SetActive(true);
            AddIconOutline(upgradeIcon);

            var weaponIcon = EnsureSceneImage(button.transform, "Weapon Icon", new Vector2(-202f, 20f), new Vector2(32f, 32f), new Vector2(0.5f, 0.5f));
            weaponIcon.sprite = GeneratedSpriteLoader.Load("Slash_0");
            weaponIcon.gameObject.SetActive(true);
            AddIconOutline(weaponIcon);

            EnsureIconSet(button.transform, "Weapon Type Icons", new Vector2(48f, 20f), new Vector2(28f, 28f), new Vector2(0.5f, 0.5f), WeaponAttributeType.Melee, true);
            var weaponName = EnsureText(button.transform, "Weapon Name Text", "スラッシュ", 20, TextAnchor.MiddleLeft);
            SetTextRect(weaponName.rectTransform, new Vector2(-80f, 20f), new Vector2(210f, 30f), new Vector2(0.5f, 0.5f));
            var upgradeText = EnsureText(button.transform, "Upgrade Text", "攻撃力 0>0", 20, TextAnchor.MiddleLeft);
            SetTextRect(upgradeText.rectTransform, new Vector2(-18f, -22f), new Vector2(330f, 30f), new Vector2(0.5f, 0.5f));
            var newWeaponMark = EnsureText(button.transform, "New Weapon Mark", "★", 26, TextAnchor.MiddleCenter);
            newWeaponMark.color = new Color(1f, 0.86f, 0.28f, 1f);
            SetTextRect(newWeaponMark.rectTransform, new Vector2(-202f, -22f), new Vector2(32f, 32f), new Vector2(0.5f, 0.5f));
            newWeaponMark.gameObject.SetActive(false);

            var legacyLabel = button.transform.Find("Label");
            if (legacyLabel != null) legacyLabel.gameObject.SetActive(false);
            EditorUtility.SetDirty(button);
        }

        static void EnsureLevelUpPanelLayout(GameManager gameManager)
        {
            if (gameManager == null || gameManager.levelUpPanel == null) return;

            var panelRect = gameManager.levelUpPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(600f, 390f);
                EditorUtility.SetDirty(panelRect);
            }

            var panelImage = gameManager.levelUpPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.05f, 0.065f, 0.07f, 0.94f);
                EditorUtility.SetDirty(panelImage);
            }

            var frame = gameManager.levelUpPanel.transform.Find("Level Up Frame") as RectTransform;
            if (frame != null)
            {
                frame.sizeDelta = new Vector2(600f, 390f);
                EditorUtility.SetDirty(frame);
            }

            var title = FindTitleLabel(gameManager.levelUpPanel.transform);
            if (title != null)
            {
                title.fontSize = 32;
                SetTextRect(title.rectTransform, new Vector2(0f, 148f), new Vector2(430f, 54f), new Vector2(0.5f, 0.5f));
                EditorUtility.SetDirty(title);
            }

            for (int i = 0; i < gameManager.upgradeButtons.Length; i++)
            {
                var button = gameManager.upgradeButtons[i];
                if (button == null) continue;
                var rect = button.GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.anchoredPosition = new Vector2(0f, 70f - i * 96f);
                rect.sizeDelta = new Vector2(470f, 82f);
                EditorUtility.SetDirty(rect);
            }
        }

        static Text FindTitleLabel(Transform levelUpPanel)
        {
            var texts = levelUpPanel.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                if (text != null && text.text == "レベルアップ") return text;
            }
            return null;
        }

        static void EnsureHudWeaponTypeIcon(Transform hud, string panelName, WeaponAttributeType attributeType)
        {
            var panel = hud.Find(panelName);
            if (panel == null) return;
            EnsureIconSet(panel, "Weapon Type Icons", new Vector2(119f, -12f), new Vector2(18f, 18f), new Vector2(0f, 1f), attributeType, true);

            var title = panel.Find("Title") as RectTransform;
            if (title != null)
            {
                title.offsetMax = new Vector2(-28f, title.offsetMax.y);
                EditorUtility.SetDirty(title);
            }
        }

        static Image EnsureSceneImage(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var existing = parent.Find(name);
            Image image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                image = go.AddComponent<Image>();
            }
            else
            {
                image = existing.GetComponent<Image>();
                if (image == null) image = existing.gameObject.AddComponent<Image>();
            }

            image.raycastTarget = false;
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(image);
            return image;
        }

        static Image EnsureIconImage(RectTransform parent, string name, string iconResource, Vector2 size)
        {
            var image = EnsureSceneImage(parent, name, Vector2.zero, size, new Vector2(0.5f, 0.5f));
            image.sprite = GeneratedSpriteLoader.Load(iconResource);
            image.color = Color.white;
            return image;
        }

        static Text EnsureText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var existing = parent.Find(name);
            Text text;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                text = go.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.raycastTarget = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            else
            {
                text = existing.GetComponent<Text>();
                if (text == null) text = existing.gameObject.AddComponent<Text>();
                if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            EditorUtility.SetDirty(text);
            return text;
        }

        static void SetTextRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
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

        static void Upsert(List<GeneratedSpriteCatalog.Entry> entries, string iconResource)
        {
            var sprite = GeneratedSpriteLoader.Load(iconResource);
            if (sprite == null) return;

            int index = entries.FindIndex(entry => entry.name == iconResource);
            var next = new GeneratedSpriteCatalog.Entry
            {
                name = iconResource,
                sprite = sprite
            };

            if (index >= 0) entries[index] = next;
            else entries.Add(next);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        static void ImportIcon(string iconResource)
        {
            var importer = AssetImporter.GetAtPath(GeneratedSpriteRoot + iconResource + ".png") as TextureImporter;
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

        static void CreateIcon(string iconResource, Color32 panelColor, System.Action<Texture2D> drawSymbol)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Clear(texture);
            FillRect(texture, 5, 5, 58, 58, new Color32(20, 22, 20, 255));
            FillRect(texture, 7, 7, 56, 56, panelColor);
            FillRect(texture, 10, 10, 53, 53, Darken(panelColor, 0.58f));
            DrawRect(texture, 5, 5, 58, 58, new Color32(255, 240, 180, 255));
            DrawRect(texture, 6, 6, 57, 57, new Color32(0, 0, 0, 220));
            drawSymbol(texture);
            texture.Apply();

            var path = GeneratedSpriteRoot + iconResource + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static void DrawSword(Texture2D texture)
        {
            DrawLine(texture, 19, 44, 45, 18, new Color32(245, 245, 238, 255), 4);
            DrawLine(texture, 22, 47, 48, 21, new Color32(95, 95, 100, 255), 2);
            DrawLine(texture, 17, 47, 27, 57, new Color32(80, 44, 24, 255), 4);
            DrawLine(texture, 13, 42, 27, 56, new Color32(230, 185, 70, 255), 3);
            FillRect(texture, 43, 15, 48, 20, new Color32(250, 250, 255, 255));
        }

        static void DrawBow(Texture2D texture)
        {
            DrawArc(texture, 38, 32, 20, -70, 70, new Color32(150, 84, 36, 255), 4);
            DrawLine(texture, 38, 13, 38, 51, new Color32(235, 235, 215, 255), 2);
            DrawLine(texture, 15, 32, 51, 32, new Color32(245, 230, 175, 255), 3);
            DrawLine(texture, 46, 27, 54, 32, new Color32(245, 230, 175, 255), 3);
            DrawLine(texture, 46, 37, 54, 32, new Color32(245, 230, 175, 255), 3);
            DrawLine(texture, 18, 29, 18, 35, new Color32(245, 230, 175, 255), 2);
        }

        static void DrawStaff(Texture2D texture)
        {
            DrawLine(texture, 22, 50, 43, 15, new Color32(120, 72, 34, 255), 5);
            DrawLine(texture, 25, 51, 46, 16, new Color32(230, 180, 80, 255), 2);
            FillCircle(texture, 45, 14, 7, new Color32(235, 215, 255, 255));
            FillCircle(texture, 45, 14, 4, new Color32(128, 236, 255, 255));
            DrawLine(texture, 45, 3, 45, 8, new Color32(255, 255, 255, 255), 1);
            DrawLine(texture, 45, 20, 45, 25, new Color32(255, 255, 255, 255), 1);
            DrawLine(texture, 34, 14, 39, 14, new Color32(255, 255, 255, 255), 1);
            DrawLine(texture, 51, 14, 56, 14, new Color32(255, 255, 255, 255), 1);
        }

        static void DrawShield(Texture2D texture)
        {
            FillPolygon(texture, new[]
            {
                new Vector2Int(20, 17),
                new Vector2Int(44, 17),
                new Vector2Int(43, 37),
                new Vector2Int(32, 52),
                new Vector2Int(21, 37)
            }, new Color32(220, 230, 245, 255));
            FillPolygon(texture, new[]
            {
                new Vector2Int(24, 21),
                new Vector2Int(40, 21),
                new Vector2Int(39, 35),
                new Vector2Int(32, 46),
                new Vector2Int(25, 35)
            }, new Color32(80, 150, 230, 255));
            DrawLine(texture, 32, 22, 32, 44, new Color32(245, 245, 255, 255), 2);
        }

        static void Clear(Texture2D texture)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }
        }

        static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    SetPixel(texture, x, y, color);
                }
            }
        }

        static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
        {
            DrawLine(texture, minX, minY, maxX, minY, color, 1);
            DrawLine(texture, maxX, minY, maxX, maxY, color, 1);
            DrawLine(texture, maxX, maxY, minX, maxY, color, 1);
            DrawLine(texture, minX, maxY, minX, minY, color, 1);
        }

        static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                FillCircle(texture, x0, y0, Mathf.Max(1, thickness / 2), color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * error;
                if (e2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        static void DrawArc(Texture2D texture, int centerX, int centerY, int radius, int startDegrees, int endDegrees, Color32 color, int thickness)
        {
            Vector2Int previous = default;
            bool hasPrevious = false;
            for (int angle = startDegrees; angle <= endDegrees; angle += 4)
            {
                float radians = angle * Mathf.Deg2Rad;
                var point = new Vector2Int(
                    centerX + Mathf.RoundToInt(Mathf.Cos(radians) * radius),
                    centerY + Mathf.RoundToInt(Mathf.Sin(radians) * radius));
                if (hasPrevious) DrawLine(texture, previous.x, previous.y, point.x, point.y, color, thickness);
                previous = point;
                hasPrevious = true;
            }
        }

        static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSqr = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSqr) SetPixel(texture, x, y, color);
                }
            }
        }

        static void FillPolygon(Texture2D texture, Vector2Int[] points, Color32 color)
        {
            int minY = points.Min(point => point.y);
            int maxY = points.Max(point => point.y);
            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new List<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                    {
                        float t = (float)(y - a.y) / (b.y - a.y);
                        intersections.Add(Mathf.RoundToInt(a.x + t * (b.x - a.x)));
                    }
                }

                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    for (int x = intersections[i]; x <= intersections[i + 1]; x++)
                    {
                        SetPixel(texture, x, y, color);
                    }
                }
            }
        }

        static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) return;
            texture.SetPixel(x, y, color);
        }

        static Color32 Darken(Color32 color, float multiplier)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * multiplier), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * multiplier), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * multiplier), 0, 255),
                color.a);
        }
    }
}
