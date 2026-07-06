using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class GeneratedSpriteCatalogBuilder
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string GeneratedSpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/";

        [MenuItem("AreaSurvivors/Assets/Rebuild Generated Sprite Catalog")]
        public static void Rebuild()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("GeneratedSpriteCatalog.asset was not found: " + CatalogPath);
                return;
            }

            var entries = new Dictionary<string, Sprite>();
            if (catalog.entries != null)
            {
                for (int i = 0; i < catalog.entries.Length; i++)
                {
                    var entry = catalog.entries[i];
                    if (entry.sprite == null) continue;
                    string name = NormalizeName(entry.name);
                    if (string.IsNullOrEmpty(name)) continue;
                    entries[name] = entry.sprite;
                }
            }

            if (AssetDatabase.IsValidFolder(GeneratedSpriteRoot.TrimEnd('/')))
            {
                foreach (var path in Directory.GetFiles(GeneratedSpriteRoot, "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = path.Replace("\\", "/");
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null) continue;
                    string name = NormalizeName(RelativeGeneratedName(assetPath));
                    if (string.IsNullOrEmpty(name)) continue;
                    entries[name] = sprite;
                }
            }

            var rebuilt = new List<GeneratedSpriteCatalog.Entry>();
            foreach (var pair in entries)
            {
                rebuilt.Add(new GeneratedSpriteCatalog.Entry { name = pair.Key, sprite = pair.Value });
            }
            rebuilt.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            catalog.entries = rebuilt.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"GeneratedSpriteCatalog rebuilt: entries={catalog.entries.Length}");
        }

        static string RelativeGeneratedName(string assetPath)
        {
            if (!assetPath.StartsWith(GeneratedSpriteRoot)) return Path.GetFileNameWithoutExtension(assetPath);
            string relative = assetPath.Substring(GeneratedSpriteRoot.Length);
            if (relative.EndsWith(".png")) relative = relative.Substring(0, relative.Length - ".png".Length);
            return relative;
        }

        static string NormalizeName(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : name.Replace("\\", "/").TrimEnd('.');
        }
    }
}
