using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class GeneratedSpriteCatalog : ScriptableObject
    {
        public Entry[] entries;

        public Sprite Find(string name)
        {
            if (string.IsNullOrEmpty(name) || entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].name == name) return entries[i].sprite;
            }
            return null;
        }

        public Sprite[] FindAll(string prefix)
        {
            if (entries == null) return Array.Empty<Sprite>();
            prefix = string.IsNullOrEmpty(prefix) ? string.Empty : prefix.TrimEnd('/') + "/";
            var results = new List<Sprite>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].sprite == null) continue;
                if (string.IsNullOrEmpty(prefix) || entries[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    results.Add(entries[i].sprite);
                }
            }
            results.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
            return results.ToArray();
        }

        [Serializable]
        public struct Entry
        {
            public string name;
            public Sprite sprite;
        }
    }
}
