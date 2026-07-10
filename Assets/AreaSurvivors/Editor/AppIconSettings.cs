#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class AppIconSettings
    {
        private const string IconPath = "Assets/AreaSurvivors/Sprites/Generated/UI/AppIconKnight.png";

        [MenuItem("Area Survivors/Project/Apply App Icon")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"Application icon texture is missing: {IconPath}");
                return;
            }

#pragma warning disable 0618
            var sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
            var icons = new Texture2D[Mathf.Max(1, sizes.Length)];
            for (var i = 0; i < icons.Length; i++)
            {
                icons[i] = icon;
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
#pragma warning restore 0618
            AssetDatabase.SaveAssets();
            Debug.Log($"Applied standalone application icon: {IconPath}");
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
#endif
