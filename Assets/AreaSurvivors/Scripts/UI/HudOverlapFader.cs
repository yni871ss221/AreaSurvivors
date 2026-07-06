using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class HudOverlapFader : MonoBehaviour
    {
        [Range(0f, 1f)] public float backgroundAlpha = 0.5f;
        [Range(0f, 1f)] public float overlapAlpha = 0.2f;
        [Min(0f)] public float padding = 96f;
        [Min(0.01f)] public float fadeSpeed = 10f;
        public string groupId;

        static readonly Dictionary<string, List<HudOverlapFader>> Groups = new Dictionary<string, List<HudOverlapFader>>();

        RectTransform panel;
        CanvasGroup group;
        Canvas canvas;
        Image background;
        Transform player;

        void Awake()
        {
            panel = GetComponent<RectTransform>();
            group = GetComponent<CanvasGroup>();
            canvas = GetComponentInParent<Canvas>();
            background = GetComponent<Image>();
            ApplyBackgroundAlpha();
        }

        void OnEnable()
        {
            RegisterGroup();
        }

        void OnDisable()
        {
            UnregisterGroup();
        }

        void LateUpdate()
        {
            float targetAlpha = IsGroupOverlapping() ? overlapAlpha : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            ApplyBackgroundAlpha();
        }

        public void SetGroup(string value)
        {
            if (groupId == value) return;
            UnregisterGroup();
            groupId = value;
            RegisterGroup();
        }

        void RegisterGroup()
        {
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(groupId)) return;
            if (!Groups.TryGetValue(groupId, out var members))
            {
                members = new List<HudOverlapFader>();
                Groups.Add(groupId, members);
            }

            if (!members.Contains(this)) members.Add(this);
        }

        void UnregisterGroup()
        {
            if (string.IsNullOrWhiteSpace(groupId)) return;
            if (!Groups.TryGetValue(groupId, out var members)) return;
            members.Remove(this);
            if (members.Count == 0) Groups.Remove(groupId);
        }

        bool IsGroupOverlapping()
        {
            if (string.IsNullOrWhiteSpace(groupId)) return IsPlayerOverlapping();
            if (!Groups.TryGetValue(groupId, out var members)) return IsPlayerOverlapping();

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member != null && member.isActiveAndEnabled && member.IsPlayerOverlapping()) return true;
            }

            return false;
        }

        bool IsPlayerOverlapping()
        {
            if (player == null && GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                player = GameManager.Instance.Player.transform;
            }

            if (player == null || panel == null || canvas == null || Camera.main == null) return false;
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(player.position);
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            var panelCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(panelCamera, corners[0]) - Vector2.one * padding;
            Vector2 max = RectTransformUtility.WorldToScreenPoint(panelCamera, corners[2]) + Vector2.one * padding;
            return screenPoint.x >= min.x && screenPoint.x <= max.x &&
                   screenPoint.y >= min.y && screenPoint.y <= max.y;
        }

        void ApplyBackgroundAlpha()
        {
            if (background == null) return;
            var color = background.color;
            color.a = backgroundAlpha;
            background.color = color;
        }
    }
}
