using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class LobbyReferenceLayoutSceneSetup
    {
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";

        [MenuItem("AreaSurvivors/Setup/Restore Lobby Reference Layout")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath);

            SetRect("Header Panel", new Vector2(0f, 280f), new Vector2(780f, 78f));
            SetRect("Title", new Vector2(0f, 298f), new Vector2(520f, 44f));
            SetRect("TokenInfo", new Vector2(0f, 260f), new Vector2(620f, 32f));

            SetRect("Stage Progress Panel", new Vector2(0f, 112f), new Vector2(900f, 176f));
            SetRect("StageProgressTitle", new Vector2(0f, 184f), new Vector2(420f, 34f));
            for (int stage = 1; stage <= 4; stage++)
            {
                SetRect("Stage " + stage + " Panel", new Vector2(-318f + (stage - 1) * 212f, 104f), new Vector2(184f, 126f));
            }

            SetRect("Character Panel", new Vector2(0f, -154f), new Vector2(900f, 218f));
            SetActive("CharacterTitle", false);
            SetRect("Character Knight", new Vector2(-300f, -2f), new Vector2(210f, 190f));
            DisableCharacterSelection("Character Knight");

            SetRect("Test Launch Button", new Vector2(-542f, -105f), new Vector2(180f, 52f));
            SetRect("Upgrade Button", new Vector2(-30f, -91f), new Vector2(220f, 58f));
            SetRect("Weapon Book Button", new Vector2(-30f, -157f), new Vector2(220f, 58f));
            SetRect("Relic Button", new Vector2(-30f, -223f), new Vector2(220f, 58f));
            SetRect("Title Button", new Vector2(-150f, -305f), new Vector2(220f, 52f));
            SetRect("Start Game Button", new Vector2(150f, -305f), new Vector2(220f, 58f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Lobby reference layout restored.");
        }

        static void SetRect(string name, Vector2 position, Vector2 size)
        {
            var rect = FindSceneTransform(name) as RectTransform;
            if (rect == null)
            {
                Debug.LogWarning($"Lobby layout target not found: {name}");
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetActive(string name, bool active)
        {
            var transform = FindSceneTransform(name);
            if (transform != null) transform.gameObject.SetActive(active);
        }

        static void DisableCharacterSelection(string name)
        {
            var transform = FindSceneTransform(name);
            if (transform == null) return;

            var button = transform.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            var selection = transform.GetComponent<UiSelectionHighlight>();
            if (selection != null)
            {
                selection.SetForceSelected(false);
                selection.enabled = false;
            }

            var pointerSelection = transform.GetComponent<SelectOnPointerEnter>();
            if (pointerSelection != null) pointerSelection.enabled = false;

            var characterSelection = transform.GetComponent<CharacterSelectionHighlight>();
            if (characterSelection != null) characterSelection.enabled = false;

            SetActive(transform, "State Fill", false);
            for (int i = 0; i < 4; i++)
            {
                SetActive(transform, "Selected Edge " + i, false);
                SetActive(transform, "Selected Shadow " + i, false);
            }
        }

        static void SetActive(Transform root, string childName, bool active)
        {
            var child = root.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        static Transform FindSceneTransform(string name)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null || transform.name != name) continue;
                if (!transform.gameObject.scene.IsValid() || transform.gameObject.scene.path != LobbyScenePath) continue;
                return transform;
            }

            return null;
        }
    }
}
