using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class LocalizationService
    {
        sealed class TextState
        {
            public string source;
            public string lastOutput;
        }

        static readonly Dictionary<Text, TextState> TextStates = new Dictionary<Text, TextState>();
        static readonly Dictionary<string, string> EnglishToJapanese = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly List<Text> DeadTexts = new List<Text>();
        static bool initialized;

        public static event Action LanguageChanged;

        public static GameLanguage CurrentLanguage => LocalizationSettingsStore.Current;
        public static bool IsEnglish => CurrentLanguage == GameLanguage.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static string Text(string japanese, string english)
        {
            RegisterPair(japanese, english);
            return IsEnglish ? english : japanese;
        }

        public static string Format(string japanese, string english, params object[] args)
        {
            string japaneseOutput = string.Format(japanese, args);
            string englishOutput = string.Format(english, args);
            RegisterPair(japaneseOutput, englishOutput);
            return IsEnglish ? englishOutput : japaneseOutput;
        }

        public static string LocalizeSource(string source)
        {
            string japaneseSource = CanonicalJapaneseSource(source);
            string localized = LocalizationTextCatalog.Translate(japaneseSource, CurrentLanguage);
            if (IsEnglish) RegisterPair(japaneseSource, localized);
            return localized;
        }

        public static bool ContainsJapanese(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if ((character >= '\u3040' && character <= '\u30ff')
                    || (character >= '\u3400' && character <= '\u9fff')
                    || (character >= '\uff66' && character <= '\uff9d')) return true;
            }

            return false;
        }

        public static void NotifyLanguageChanged()
        {
            RefreshAllTexts();
            LanguageChanged?.Invoke();
        }

        public static void RefreshAllTexts()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                TrackScene(SceneManager.GetSceneAt(sceneIndex));
            }

            foreach (var pair in TextStates)
            {
                var text = pair.Key;
                if (text == null)
                {
                    DeadTexts.Add(text);
                    continue;
                }

                Apply(text, pair.Value, true);
            }

            RemoveDeadTexts();
        }

        public static void RefreshHierarchy(GameObject root)
        {
            if (root == null) return;
            TrackHierarchy(root);
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text != null && TextStates.TryGetValue(text, out var state)) Apply(text, state, true);
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrackScene(scene);
            RefreshAllTexts();
        }

        static void TrackScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                TrackHierarchy(root);
            }
        }

        static void TrackHierarchy(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text == null || TextStates.ContainsKey(text)) continue;
                TextStates.Add(text, new TextState { source = text.text, lastOutput = null });
            }
        }

        static void Apply(Text text, TextState state, bool force)
        {
            if (text.text != state.lastOutput)
            {
                state.source = CanonicalJapaneseSource(text.text);
            }

            string localized = LocalizeSource(state.source);
            if (force || text.text != localized) text.text = localized;
            state.lastOutput = localized;
        }

        static string CanonicalJapaneseSource(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (EnglishToJapanese.TryGetValue(value, out string japanese)) return japanese;
            return LocalizationTextCatalog.Translate(value, GameLanguage.Japanese);
        }

        static void RegisterPair(string japanese, string english)
        {
            if (string.IsNullOrEmpty(japanese) || string.IsNullOrEmpty(english) || japanese == english) return;
            if (EnglishToJapanese.TryGetValue(english, out string existing) && existing == japanese) return;
            EnglishToJapanese[english] = japanese;
        }

        static void RemoveDeadTexts()
        {
            if (DeadTexts.Count == 0) return;
            foreach (var text in DeadTexts) TextStates.Remove(text);
            DeadTexts.Clear();
        }
    }
}
