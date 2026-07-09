using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class JapaneseFontProvider
    {
        static readonly string[] JapaneseFontCandidates =
        {
            "BIZ UDPGothic",
            "BIZ UDGothic",
            "Yu Gothic UI",
            "Yu Gothic",
            "Meiryo",
            "MS PGothic",
            "MS Gothic",
            "Noto Sans CJK JP",
            "Noto Sans JP"
        };

        static Font japaneseFont;
        static Font fallbackFont;

        public static Font Font
        {
            get
            {
                if (japaneseFont != null) return japaneseFont;
                japaneseFont = UnityEngine.Font.CreateDynamicFontFromOSFont(JapaneseFontCandidates, 24);
                if (japaneseFont != null) return japaneseFont;
                if (fallbackFont == null) fallbackFont = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
                return fallbackFont;
            }
        }

        public static void Apply(Text text)
        {
            var font = Font;
            if (text == null || font == null) return;
            if (text.font != font) text.font = font;
            text.alignByGeometry = true;
        }

        public static void Apply(TextMesh textMesh)
        {
            var font = Font;
            if (textMesh == null || font == null || textMesh.font == font) return;
            textMesh.font = font;
            var meshRenderer = textMesh.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sharedMaterial = font.material;
        }

        public static void ApplyAllLoadedText()
        {
            var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++) Apply(texts[i]);

            var textMeshes = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < textMeshes.Length; i++) Apply(textMeshes[i]);
        }
    }

    public sealed class JapaneseFontBootstrap : MonoBehaviour
    {
        const float RefreshIntervalSeconds = 2f;

        static JapaneseFontBootstrap instance;
        float nextRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (instance != null) return;
            var go = new GameObject("Japanese Font Bootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<JapaneseFontBootstrap>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            JapaneseFontProvider.ApplyAllLoadedText();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            JapaneseFontProvider.ApplyAllLoadedText();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(ApplyAfterSceneLoad());
        }

        IEnumerator ApplyAfterSceneLoad()
        {
            yield return null;
            JapaneseFontProvider.ApplyAllLoadedText();
            yield return null;
            JapaneseFontProvider.ApplyAllLoadedText();
        }
    }
}
