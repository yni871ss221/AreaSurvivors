using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class TokenGainPopupSetup
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/TokenGainPopup.prefab";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Player.prefab";
        const string TokenSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Token.png";
        const string AnchorName = "Token Gain Popup Anchor";
        const string PlayerPopupName = "Token Gain Popup";
        static readonly Vector3 PlayerPopupAnchorPosition = new Vector3(0f, 0.3f, 0f);

        [MenuItem("AreaSurvivors/Setup/Create Token Gain Popup")]
        public static void Apply()
        {
            CreateOrUpdatePrefab();
            AssignToPlayerPrefab();
        }

        static GameObject CreateOrUpdatePrefab()
        {
            var tokenSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TokenSpritePath);
            var root = new GameObject("TokenGainPopup");
            root.transform.localScale = Vector3.one;
            ConfigurePopup(root.transform, tokenSprite, true);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("Token gain popup prefab created: " + PrefabPath);
            return prefab;
        }

        static void AssignToPlayerPrefab()
        {
            var tokenSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TokenSpritePath);
            var playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var player = playerRoot.GetComponent<PlayerController>();
                if (player == null)
                {
                    Debug.LogError("PlayerController was not found in " + PlayerPrefabPath);
                    return;
                }

                var anchor = FindOrCreateChild(playerRoot.transform, AnchorName);
                anchor.localPosition = PlayerPopupAnchorPosition;
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;

                var popupRoot = FindOrCreateChild(anchor, PlayerPopupName);
                popupRoot.localPosition = Vector3.zero;
                popupRoot.localRotation = Quaternion.identity;
                popupRoot.localScale = Vector3.one;

                var popup = ConfigurePopup(popupRoot, tokenSprite, false);
                player.tokenGainPopup = popup;
                popup.gameObject.SetActive(false);

                EditorUtility.SetDirty(player);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
                Debug.Log("Token gain popup assigned to player prefab: " + PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        static Transform FindOrCreateChild(Transform parent, string objectName)
        {
            var child = parent.Find(objectName);
            if (child != null) return child;
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static TokenGainPopup ConfigurePopup(Transform root, Sprite tokenSprite, bool destroyOnComplete)
        {
            var popup = root.GetComponent<TokenGainPopup>();
            if (popup == null) popup = root.gameObject.AddComponent<TokenGainPopup>();
            popup.destroyOnComplete = destroyOnComplete;

            var icon = FindOrCreateChild(root, "Token Icon");
            icon.localPosition = new Vector3(0f, 0f, 0f);
            icon.localRotation = Quaternion.identity;
            icon.localScale = Vector3.one * 0.28f;
            var iconVisual = icon.GetComponent<PaperMeshVisual>();
            if (iconVisual == null) iconVisual = icon.gameObject.AddComponent<PaperMeshVisual>();
            iconVisual.Configure(tokenSprite, Color.white, 24100);

            var textObject = FindOrCreateChild(root, "Amount Text");
            textObject.localPosition = new Vector3(0.16f, 0.02f, 0f);
            textObject.localRotation = Quaternion.identity;
            textObject.localScale = Vector3.one;
            var text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.gameObject.AddComponent<TextMesh>();
            text.text = "1";
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 42;
            text.characterSize = 0.07f;
            text.color = new Color(1f, 0.9f, 0.28f, 1f);
            var outline = textObject.GetComponent<RuntimeTextMeshOutline>();
            if (outline == null) outline = textObject.gameObject.AddComponent<RuntimeTextMeshOutline>();
            outline.SetColors(text.color, Color.black);

            popup.iconVisual = iconVisual;
            popup.amountText = text;
            popup.tokenSprite = tokenSprite;
            return popup;
        }
    }
}
