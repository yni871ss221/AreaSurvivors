using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreaSurvivors.EditorTools
{
    public static class ExcaliburSectorVisualMigration
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ExcaliburSlash.prefab";
        const string TexturePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/ExcaliburEffect.png";
        const string MaterialFolder = "Assets/AreaSurvivors/Materials";
        const string WeaponMaterialFolder = MaterialFolder + "/Weapons";
        const string MaterialPath = WeaponMaterialFolder + "/ExcaliburSectorEffect.mat";
        const string SuccessMarkerPath = "Library/AreaSafeUnity/excalibur-sector-visual-migration.success";

        [MenuItem("Area Survivors/Migrations/Apply Excalibur Sector Visual")]
        public static void ApplyMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Excalibur sector visual migration: passed.");
        }

        static void Apply()
        {
            var texture = ConfigureAndLoadTexture();

            EnsureFolder(MaterialFolder);
            EnsureFolder(WeaponMaterialFolder);
            var material = CreateOrUpdateMaterial(texture);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root == null) throw new InvalidOperationException("Excalibur prefab could not be loaded: " + PrefabPath);

                var oldPaperVisual = root.transform.Find("Paper Visual");
                if (oldPaperVisual != null) UnityEngine.Object.DestroyImmediate(oldPaperVisual.gameObject);

                var polygonCollider = root.GetComponent<PolygonCollider2D>();
                if (polygonCollider == null) polygonCollider = root.AddComponent<PolygonCollider2D>();
                polygonCollider.isTrigger = true;

                // AdvancedWeaponProjectile requires at least one Collider2D.
                // Add the replacement first so Unity permits removing the old circle.
                var circleCollider = root.GetComponent<CircleCollider2D>();
                if (circleCollider != null) UnityEngine.Object.DestroyImmediate(circleCollider);

                var sectorTransform = root.transform.Find("Sector Visual");
                if (sectorTransform == null)
                {
                    var sectorObject = new GameObject("Sector Visual");
                    sectorTransform = sectorObject.transform;
                    sectorTransform.SetParent(root.transform, false);
                }
                sectorTransform.localPosition = Vector3.zero;
                sectorTransform.localRotation = Quaternion.identity;
                sectorTransform.localScale = Vector3.one;

                var meshFilter = sectorTransform.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = sectorTransform.gameObject.AddComponent<MeshFilter>();
                var meshRenderer = sectorTransform.GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = sectorTransform.gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var sectorVisual = root.GetComponent<ExcaliburSectorVisual>();
                if (sectorVisual == null) sectorVisual = root.AddComponent<ExcaliburSectorVisual>();
                sectorVisual.Initialize(meshFilter, meshRenderer, polygonCollider, material);

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (savedPrefab == null) throw new InvalidOperationException("Excalibur prefab save returned null: " + PrefabPath);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            NormalizePrefabYaml(PrefabPath);
            ValidateNoTrailingWhitespace(PrefabPath);
        }

        static Material CreateOrUpdateMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
                material = new Material(shader) { name = "Excalibur Sector Effect" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.color = new Color(1f, 1f, 1f, 0.55f);
            return material;
        }

        static Texture2D ConfigureAndLoadTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Excalibur sector texture importer is missing: " + TexturePath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 96f;
            importer.maxTextureSize = 256;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Excalibur sector texture is missing: " + TexturePath);
            return texture;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Invalid Unity folder path: " + path);
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void NormalizePrefabYaml(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
            File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        }

        static void ValidateNoTrailingWhitespace(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length != lines[i].TrimEnd().Length)
                {
                    throw new InvalidOperationException(
                        "Excalibur prefab still contains trailing whitespace at line " + (i + 1) + ": " + assetPath);
                }
            }
        }
    }
}
