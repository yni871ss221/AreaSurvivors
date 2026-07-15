using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class FrostStormDualAttackMigration
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FrostStormSpike.prefab";
        const string SpikeSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/FrostStormSpikeTall.png";
        const string SpikeSourcePath = "Assets/AreaSurvivors/Sprites/External/FrostStormSpikeTallSource.png";
        const string ObsoleteSpikeSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/FrostStormSpike.png";
        const string ObsoleteSpikeSourcePath = "Assets/AreaSurvivors/Sprites/External/FrostStormSpikeSource.png";
        const string FrostSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/FrostAreaTexture.png";
        const string ObsoleteCompositePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/FrostStormEffect.png";
        const string RangeRootName = "Frost Storm Range Visual";
        const string FrostVisualName = "Frost Area Visual";
        const string OutlineName = "Ellipse Range Outline";
        const string SpikeVisualName = "Frost Storm Spike Visual";
        const string SpikeHitboxName = "Frost Storm Spike Hitbox";
        const string MarkerRelativePath = "Library/AreaSafeUnity/frost-storm-dual-attack-validator.ok";

        [MenuItem("Area Survivors/Migrations/Apply Frost Storm Dual Attack")]
        public static void Apply()
        {
            DeleteMarker();
            AssetDatabase.ImportAsset(SpikeSourcePath, ImportAssetOptions.ForceUpdate);
            ImportTallSpikeSprite();
            GeneratedSpriteAssetUtility.ImportSprite("Weapons/FrostAreaTexture", 128f);

            var spikeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSpritePath);
            var frostSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrostSpritePath);
            if (spikeSprite == null) throw new InvalidOperationException("Frost Storm spike sprite is missing: " + SpikeSpritePath);
            if (frostSprite == null) throw new InvalidOperationException("Frost area sprite is missing: " + FrostSpritePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                throw new InvalidOperationException("Frost Storm prefab is missing: " + PrefabPath);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var area = root.GetComponent<AdvancedWeaponArea>();
                if (area == null) throw new InvalidOperationException("FrostStormSpike prefab requires AdvancedWeaponArea.");

                Transform frostVisual = FindDescendant(root.transform, FrostVisualName);
                Transform outline = FindDescendant(root.transform, OutlineName);
                if (frostVisual == null) throw new InvalidOperationException("Frost area visual is missing from FrostStormSpike prefab.");

                // This legacy object renders FrostStormEffect through PaperMeshVisual and would overlap the
                // prefab-owned spike plus normal Frost area. The dual attack owns both visuals explicitly.
                if (outline != null && outline != frostVisual)
                {
                    UnityEngine.Object.DestroyImmediate(outline.gameObject, true);
                }

                Transform rangeRoot = FindDirectChild(root.transform, RangeRootName);
                if (rangeRoot == null)
                {
                    rangeRoot = new GameObject(RangeRootName).transform;
                    rangeRoot.SetParent(root.transform, false);
                }
                rangeRoot.localPosition = Vector3.zero;
                rangeRoot.localRotation = Quaternion.identity;
                rangeRoot.localScale = Vector3.one;
                ReparentPreservingLocalTransform(frostVisual, rangeRoot);

                var frostRenderer = frostVisual.GetComponent<SpriteRenderer>();
                if (frostRenderer == null) throw new InvalidOperationException("Frost Area Visual requires SpriteRenderer.");
                frostRenderer.sprite = frostSprite;
                frostRenderer.color = Color.white;
                frostRenderer.sortingOrder = WeaponSortingOrders.AreaEffect;
                frostVisual.localRotation = Quaternion.Euler(0f, 0f, frostVisual.localEulerAngles.z);

                var areaSerialized = new SerializedObject(area);
                var visualScaleRoot = areaSerialized.FindProperty("visualScaleRoot");
                if (visualScaleRoot == null) throw new InvalidOperationException("AdvancedWeaponArea.visualScaleRoot is missing.");
                visualScaleRoot.objectReferenceValue = rangeRoot;
                areaSerialized.ApplyModifiedPropertiesWithoutUndo();

                Transform spikeVisual = FindDirectChild(root.transform, SpikeVisualName);
                if (spikeVisual == null)
                {
                    spikeVisual = new GameObject(SpikeVisualName).transform;
                    spikeVisual.SetParent(root.transform, false);
                }
                spikeVisual.localPosition = Vector3.zero;
                spikeVisual.localRotation = Quaternion.identity;
                spikeVisual.localScale = new Vector3(TileGrid.DefaultCellSize, TileGrid.DefaultCellSize, 1f);

                var spikeRenderer = spikeVisual.GetComponent<SpriteRenderer>();
                if (spikeRenderer == null) spikeRenderer = spikeVisual.gameObject.AddComponent<SpriteRenderer>();
                spikeRenderer.sprite = spikeSprite;
                spikeRenderer.color = Color.white;
                spikeRenderer.sortingOrder = WeaponSortingOrders.Impact;

                var billboard = spikeVisual.GetComponent<PaperBillboard>();
                if (billboard != null) UnityEngine.Object.DestroyImmediate(billboard, true);

                Transform hitboxTransform = FindDirectChild(root.transform, SpikeHitboxName);
                if (hitboxTransform == null)
                {
                    hitboxTransform = new GameObject(SpikeHitboxName).transform;
                    hitboxTransform.SetParent(root.transform, false);
                }
                hitboxTransform.localPosition = Vector3.zero;
                hitboxTransform.localRotation = Quaternion.identity;
                hitboxTransform.localScale = Vector3.one;
                var hitbox = hitboxTransform.GetComponent<BoxCollider2D>();
                if (hitbox == null) hitbox = hitboxTransform.gameObject.AddComponent<BoxCollider2D>();
                hitbox.isTrigger = true;
                hitbox.offset = Vector2.zero;
                hitbox.size = FrostStormSpikeImpact.ResolveHitboxSize(TileGrid.DefaultCellSize);

                var impact = root.GetComponent<FrostStormSpikeImpact>();
                if (impact == null) impact = root.AddComponent<FrostStormSpikeImpact>();
                var impactSerialized = new SerializedObject(impact);
                impactSerialized.FindProperty("spikeRenderer").objectReferenceValue = spikeRenderer;
                impactSerialized.FindProperty("visualDurationSeconds").floatValue = 0.35f;
                impactSerialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(area);
                EditorUtility.SetDirty(frostRenderer);
                EditorUtility.SetDirty(spikeRenderer);
                EditorUtility.SetDirty(hitbox);
                EditorUtility.SetDirty(impact);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                {
                    throw new InvalidOperationException("Failed to save FrostStormSpike prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            DeleteObsoleteAsset(ObsoleteSpikeSpritePath);
            DeleteObsoleteAsset(ObsoleteSpikeSourcePath);
            GeneratedSpriteCatalogBuilder.Rebuild();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Frost Storm dual attack: prefab migration completed.");
        }

        [MenuItem("Area Survivors/Validate/Frost Storm Dual Attack")]
        public static void Validate()
        {
            DeleteMarker();
            ValidateTallSpikeImporter();
            ValidateImporter(FrostSpritePath, 128f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Frost Storm prefab is missing.");
            var area = prefab.GetComponent<AdvancedWeaponArea>();
            var impact = prefab.GetComponent<FrostStormSpikeImpact>();
            if (area == null || impact == null) throw new InvalidOperationException("Frost Storm dual attack components are incomplete.");

            Transform rangeRoot = FindDirectChild(prefab.transform, RangeRootName);
            Transform spikeVisual = FindDirectChild(prefab.transform, SpikeVisualName);
            Transform hitboxTransform = FindDirectChild(prefab.transform, SpikeHitboxName);
            if (rangeRoot == null || spikeVisual == null || hitboxTransform == null) throw new InvalidOperationException("Frost Storm visual or hitbox roots are incomplete.");
            if (rangeRoot.localRotation != Quaternion.identity) throw new InvalidOperationException("Frost Storm range root must not use Transform rotation.");
            if (spikeVisual.localRotation != Quaternion.identity) throw new InvalidOperationException("Frost Storm spike must not use Transform rotation.");
            Transform frostVisual = FindDescendant(rangeRoot, FrostVisualName);
            if (frostVisual == null)
            {
                throw new InvalidOperationException("Frost area visual must be a child of the range scale root.");
            }
            RequireZeroPitch(frostVisual, FrostVisualName);
            if (spikeVisual.IsChildOf(rangeRoot)) throw new InvalidOperationException("Frost Storm spike must remain outside the scalable range root.");

            var areaSerialized = new SerializedObject(area);
            if (areaSerialized.FindProperty("visualScaleRoot")?.objectReferenceValue != rangeRoot)
            {
                throw new InvalidOperationException("AdvancedWeaponArea must scale only the Frost Storm range visual root.");
            }

            var spikeRenderer = spikeVisual.GetComponent<SpriteRenderer>();
            if (spikeRenderer == null || AssetDatabase.GetAssetPath(spikeRenderer.sprite) != SpikeSpritePath)
            {
                throw new InvalidOperationException("Frost Storm spike sprite reference is invalid.");
            }
            if (spikeRenderer.sortingOrder != WeaponSortingOrders.Impact) throw new InvalidOperationException("Frost Storm spike sorting order is invalid.");
            if (spikeVisual.GetComponent<PaperBillboard>() != null) throw new InvalidOperationException("Frost Storm spike must not use PaperBillboard.");
            if (Mathf.Abs(spikeVisual.localScale.x - TileGrid.DefaultCellSize) > 0.001f ||
                Mathf.Abs(spikeVisual.localScale.y - TileGrid.DefaultCellSize) > 0.001f)
            {
                throw new InvalidOperationException("Frost Storm spike must remain approximately one cell wide.");
            }

            var hitbox = hitboxTransform.GetComponent<BoxCollider2D>();
            Vector2 expectedHitboxSize = FrostStormSpikeImpact.ResolveHitboxSize(TileGrid.DefaultCellSize);
            if (hitbox == null || !hitbox.isTrigger || hitbox.offset != Vector2.zero ||
                (hitbox.size - expectedHitboxSize).sqrMagnitude > 0.000001f ||
                hitboxTransform.localPosition != Vector3.zero || hitboxTransform.localRotation != Quaternion.identity)
            {
                throw new InvalidOperationException("Frost Storm spike hitbox must be a root-centered 1x1 cell trigger.");
            }

            var frostRenderer = frostVisual.GetComponent<SpriteRenderer>();
            if (frostRenderer == null || AssetDatabase.GetAssetPath(frostRenderer.sprite) != FrostSpritePath)
            {
                throw new InvalidOperationException("Frost Storm range must reuse the normal Frost texture.");
            }

            var impactSerialized = new SerializedObject(impact);
            if (impactSerialized.FindProperty("spikeRenderer")?.objectReferenceValue != spikeRenderer)
            {
                throw new InvalidOperationException("FrostStormSpikeImpact must reference the prefab spike renderer.");
            }
            if (FrostStormSpikeImpact.ResolveDamage(9) != 18) throw new InvalidOperationException("Frost Storm spike damage must be exactly 2x attack power.");
            if (FrostStormSpikeImpact.ResolveHitboxSize(0.7f) != new Vector2(0.7f, 0.7f))
            {
                throw new InvalidOperationException("Frost Storm spike hitbox contract must remain square.");
            }

            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (AssetDatabase.GetAssetPath(renderer.sprite) == ObsoleteCompositePath)
                {
                    throw new InvalidOperationException("Obsolete FrostStormEffect composite sprite remains in the prefab: " + renderer.name);
                }
            }
            if (prefab.GetComponentsInChildren<PaperMeshVisual>(true).Length != 0 ||
                prefab.GetComponentsInChildren<PaperBillboard>(true).Length != 0)
            {
                throw new InvalidOperationException("Frost Storm prefab must not retain legacy PaperMesh visuals.");
            }
            foreach (string dependency in AssetDatabase.GetDependencies(PrefabPath, true))
            {
                if (dependency == ObsoleteCompositePath)
                {
                    throw new InvalidOperationException("Frost Storm prefab still depends on the obsolete composite sprite.");
                }
            }
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ObsoleteSpikeSpritePath) != null ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ObsoleteSpikeSourcePath) != null)
            {
                throw new InvalidOperationException("Obsolete Frost Storm spike assets must be removed after replacement.");
            }

            string markerPath = MarkerPath();
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Frost Storm dual attack validator: passed.");
        }

        static void ValidateImporter(string path, float expectedPixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter is missing: " + path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled || importer.filterMode != FilterMode.Point || !importer.alphaIsTransparency ||
                Mathf.Abs(importer.spritePixelsPerUnit - expectedPixelsPerUnit) > 0.01f)
            {
                throw new InvalidOperationException("Sprite import settings are invalid: " + path);
            }
        }

        static void ImportTallSpikeSprite()
        {
            GeneratedSpriteAssetUtility.ImportSprite("Weapons/FrostStormSpikeTall", 128f);
            var importer = AssetImporter.GetAtPath(SpikeSpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter is missing: " + SpikeSpritePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 4f / 256f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void ValidateTallSpikeImporter()
        {
            ValidateImporter(SpikeSpritePath, 128f);
            var importer = AssetImporter.GetAtPath(SpikeSpritePath) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSpritePath);
            if (importer == null || sprite == null) throw new InvalidOperationException("Tall Frost Storm spike import is incomplete.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spriteMeshType != SpriteMeshType.FullRect ||
                (settings.spritePivot - new Vector2(0.5f, 4f / 256f)).sqrMagnitude > 0.000001f ||
                Mathf.Abs(sprite.rect.width - 128f) > 0.01f || Mathf.Abs(sprite.rect.height - 256f) > 0.01f ||
                (sprite.pivot - new Vector2(64f, 4f)).sqrMagnitude > 0.01f)
            {
                throw new InvalidOperationException("Tall Frost Storm spike must use a 128x256 Full Rect sprite with a bottom-center root pivot.");
            }
        }

        static void DeleteObsoleteAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Failed to delete obsolete Frost Storm asset: " + path);
            }
        }

        static void RequireZeroPitch(Transform target, string label)
        {
            Vector3 rotation = target.localEulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(rotation.x, 0f)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(rotation.y, 0f)) > 0.01f)
            {
                throw new InvalidOperationException(label + " must not use Transform rotation X/Y.");
            }
        }

        static void ReparentPreservingLocalTransform(Transform target, Transform parent)
        {
            if (target.parent == parent) return;
            Vector3 position = target.localPosition;
            Quaternion rotation = target.localRotation;
            Vector3 scale = target.localScale;
            target.SetParent(parent, false);
            target.localPosition = position;
            target.localRotation = rotation;
            target.localScale = scale;
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        static Transform FindDescendant(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name) return transforms[i];
            }
            return null;
        }

        static void DeleteMarker()
        {
            string path = MarkerPath();
            if (File.Exists(path)) File.Delete(path);
        }

        static string MarkerPath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, MarkerRelativePath);
        }
    }
}
