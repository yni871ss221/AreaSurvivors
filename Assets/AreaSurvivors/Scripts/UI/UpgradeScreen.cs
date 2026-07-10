using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UpgradeScreen : MonoBehaviour
    {
        Transform graphRoot;
        RectTransform tooltipRoot;
        RectTransform canvasRoot;
        Canvas uiCanvas;
        Text tooltipTitle;
        Text tooltipDescription;
        Text tokenLabel;
        SkillNodeView[] sceneNodes;
        SceneNavigator navigator;
        Button lobbyButton;
        UpgradeNodeHover focusedHover;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            if (!TryBindSceneTree())
            {
                Debug.LogError("UpgradeScreen requires a Scene-authored Upgrade UI with SkillNodeView objects.");
            }
        }

        bool TryBindSceneTree()
        {
            var uiObject = GameObject.Find("Upgrade UI");
            if (uiObject == null) return false;
            sceneNodes = uiObject.GetComponentsInChildren<SkillNodeView>(true);
            if (sceneNodes == null || sceneNodes.Length == 0) return false;

            canvasRoot = uiObject.transform as RectTransform;
            uiCanvas = uiObject.GetComponent<Canvas>();
            graphRoot = FindDeep(uiObject.transform, "Skill Tree");
            tooltipRoot = FindDeep(uiObject.transform, "Tooltip") as RectTransform;
            tooltipTitle = FindDeep(uiObject.transform, "Tooltip Title")?.GetComponent<Text>();
            tooltipDescription = FindDeep(uiObject.transform, "Tooltip Description")?.GetComponent<Text>();
            tokenLabel = FindDeep(uiObject.transform, "TokenLabel")?.GetComponent<Text>();
            ConfigureTooltipRoot();

            navigator = gameObject.GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();
            lobbyButton = BindSceneButton(uiObject.transform, "ロビーへ", navigator.LoadLobby);

            RefreshSceneTree();
            return true;
        }

        void Update()
        {
            if (UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                if (navigator != null) navigator.LoadLobby();
                return;
            }

            var candidates = SelectionCandidates();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            UiSelectionUtility.EnsureSelection(candidates);
            RefreshFocusedTooltip();
        }

        void ConfigureTooltipRoot()
        {
            if (tooltipRoot == null) return;
            tooltipRoot.gameObject.SetActive(false);
            foreach (var graphic in tooltipRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null) graphic.raycastTarget = false;
            }
        }

        void RefreshFocusedTooltip()
        {
            if (!UiSelectionUtility.IsControllerInputMode)
            {
                if (UpgradeNodeHover.PointerHover != null)
                {
                    focusedHover = UpgradeNodeHover.PointerHover;
                    return;
                }

                if (focusedHover != null) focusedHover.Hide();
                focusedHover = null;
                if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
                return;
            }

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var nextHover = selected != null ? selected.GetComponent<UpgradeNodeHover>() : null;
            if (nextHover == focusedHover)
            {
                if (focusedHover != null) focusedHover.ShowForFocus();
                return;
            }

            if (focusedHover != null) focusedHover.Hide();
            focusedHover = nextHover;
            if (focusedHover != null) focusedHover.ShowForFocus();
            else if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var result = FindDeep(root.GetChild(i), name);
                if (result != null) return result;
            }

            return null;
        }

        static Button BindSceneButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindDeep(root, name)?.GetComponent<Button>();
            if (button == null) return null;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() =>
                {
                    AudioManager.PlayButtonConfirm();
                    action();
                });
            }

            return button;
        }

        void RefreshSceneTree(SkillNodeView preferredFocusNode = null)
        {
            if (graphRoot == null) return;
            if (tokenLabel != null) tokenLabel.text = $"所持トークン {ProgressionStore.Data.tokens}";

            foreach (var node in sceneNodes)
            {
                if (node == null) continue;
                node.ResolveReferences();
                node.ApplyGridPosition();
            }

            foreach (var node in sceneNodes)
            {
                ConfigureSceneNode(node, sceneNodes);
            }

            ConfigureSceneNavigation();
            RefreshSceneLinks();
            RestoreFocus(preferredFocusNode);
        }

        void ConfigureSceneNode(SkillNodeView node, SkillNodeView[] sceneNodes)
        {
            if (node == null) return;
            node.ResolveReferences();
            if (ProgressionStore.IsRetiredUpgrade(node.type))
            {
                node.gameObject.SetActive(false);
                return;
            }

            bool visible = IsSceneNodeVisible(node);
            node.gameObject.SetActive(visible);
            if (!visible) return;

            if (node.nodeNumberText != null)
            {
                node.nodeNumberText.gameObject.SetActive(false);
            }

            int level = ProgressionStore.GetLevel(node.type);
            int cost = ProgressionStore.GetCost(node.type, level);
            bool purchased = level > 0;
            bool affordable = ProgressionStore.Data.tokens >= cost;
            bool prerequisiteMet = AreScenePrerequisitesMet(node);
            bool maxed = level >= ProgressionStore.GetMaxLevel(node.type);

            if (node.background != null)
            {
                node.background.color = purchased ? new Color(0.28f, 0.55f, 0.38f, 0.96f) : affordable ? new Color(0.17f, 0.28f, 0.24f, 0.96f) : new Color(0.12f, 0.13f, 0.14f, 0.92f);
            }

            if (node.panelOutline != null)
            {
                node.panelOutline.effectColor = maxed ? new Color(1f, 0.88f, 0.18f, 1f) : Color.white;
            }

            if (node.icon != null)
            {
                node.icon.color = node.implemented && (purchased || affordable) ? Color.white : new Color(0.45f, 0.48f, 0.48f, 1f);
            }

            if (node.statusText != null)
            {
                node.statusText.text = !node.implemented ? "予定" : maxed ? $"Lv {level} MAX" : purchased ? $"Lv {level}" : prerequisiteMet ? $"Cost {cost}" : "LOCK";
                node.statusText.color = GetStatusTextColor(node.implemented, purchased, prerequisiteMet, maxed);
                node.statusText.transform.SetAsLastSibling();
            }

            if (node.button != null)
            {
                ConfigureNodeFocusHighlight(node.button);
                node.button.onClick.RemoveAllListeners();
                node.button.interactable = node.implemented;
                var colors = node.button.colors;
                colors.disabledColor = Color.white;
                node.button.colors = colors;
                node.button.onClick.AddListener(() =>
                {
                    int currentLevel = ProgressionStore.GetLevel(node.type);
                    bool canPurchase = node.implemented
                        && AreScenePrerequisitesMet(node)
                        && currentLevel < ProgressionStore.GetMaxLevel(node.type)
                        && ProgressionStore.Data.tokens >= ProgressionStore.GetCost(node.type, currentLevel);
                    if (!canPurchase) return;

                    AudioManager.PlayButtonConfirm();
                    if (ProgressionStore.TryBuy(node.type)) RefreshSceneTree(node);
                });
            }

            var hover = node.button != null ? node.button.GetComponent<UpgradeNodeHover>() : null;
            if (hover == null && node.button != null) hover = node.button.gameObject.AddComponent<UpgradeNodeHover>();
            if (hover != null)
            {
                hover.title = tooltipTitle;
                hover.description = tooltipDescription;
                hover.tooltipRoot = tooltipRoot;
                hover.canvasRoot = canvasRoot;
                hover.canvas = uiCanvas;
                hover.targetRect = node.RectTransform;
                hover.titleText = node.implemented ? $"{node.title}  Lv {level}  Cost {cost}" : $"{node.title}  実装予定";
                hover.descriptionText = node.implemented ? node.description : node.description + "（現在はツリー上の予約ノードです）";
            }
        }

        void RefreshSceneLinks()
        {
            var sceneLinks = graphRoot.GetComponentsInChildren<SkillLinkSegment>(true);
            var sceneLinkViews = graphRoot.GetComponentsInChildren<SkillLinkView>(true);
            if ((sceneLinks == null || sceneLinks.Length == 0) && (sceneLinkViews == null || sceneLinkViews.Length == 0))
            {
                Debug.LogWarning("UpgradeScreen requires Scene-authored skill link objects. Runtime link generation is disabled.");
                return;
            }

            if (sceneLinks != null)
            {
                foreach (var link in sceneLinks)
                {
                    if (link == null) continue;
                    bool visible = ProgressionStore.GetLevel(link.prerequisite) > 0;
                    link.gameObject.SetActive(visible);
                    if (visible) link.ApplyState(true);
                }
            }

            if (sceneLinkViews != null)
            {
                foreach (var link in sceneLinkViews)
                {
                    if (link == null) continue;
                    bool visible = ProgressionStore.GetLevel(link.prerequisite) > 0
                        && IsSceneNodeVisible(link.fromNode)
                        && IsSceneNodeVisible(link.toNode);
                    link.gameObject.SetActive(visible);
                    if (visible) link.ApplyState(true);
                }
            }
        }

        static bool AreScenePrerequisitesMet(SkillNodeView node)
        {
            if (node == null) return true;
            var prerequisites = node.EffectivePrerequisites();
            if (prerequisites.Length == 0) return true;
            foreach (var prerequisite in prerequisites)
            {
                if (ProgressionStore.GetLevel(prerequisite) <= 0) return false;
            }

            return true;
        }

        static bool IsSceneNodeVisible(SkillNodeView node)
        {
            if (node == null || ProgressionStore.IsRetiredUpgrade(node.type)) return false;
            if (ProgressionStore.GetLevel(node.type) > 0) return true;
            return AreScenePrerequisitesMet(node);
        }

        static Color GetStatusTextColor(bool implemented, bool purchased, bool prerequisiteMet, bool maxed)
        {
            if (!implemented) return new Color(0.78f, 0.78f, 0.78f, 1f);
            if (maxed) return new Color(1f, 0.88f, 0.28f, 1f);
            if (purchased) return new Color(0.92f, 1f, 0.95f, 1f);
            if (prerequisiteMet) return new Color(1f, 0.94f, 0.58f, 1f);
            return new Color(0.66f, 0.70f, 0.72f, 1f);
        }

        Button FirstSelectableNodeButton()
        {
            if (sceneNodes == null) return null;
            for (int i = 0; i < sceneNodes.Length; i++)
            {
                var node = sceneNodes[i];
                if (node != null && UiSelectionUtility.IsSelectable(node.button)) return node.button;
            }

            return null;
        }

        Selectable[] SelectionCandidates(params Selectable[] priority)
        {
            var candidates = new List<Selectable>();
            if (priority != null)
            {
                for (int i = 0; i < priority.Length; i++)
                {
                    if (UiSelectionUtility.IsSelectable(priority[i])) candidates.Add(priority[i]);
                }
            }

            if (sceneNodes != null)
            {
                for (int i = 0; i < sceneNodes.Length; i++)
                {
                    var node = sceneNodes[i];
                    if (node != null && UiSelectionUtility.IsSelectable(node.button) && !candidates.Contains(node.button))
                    {
                        candidates.Add(node.button);
                    }
                }
            }

            if (UiSelectionUtility.IsSelectable(lobbyButton) && !candidates.Contains(lobbyButton)) candidates.Add(lobbyButton);
            return candidates.ToArray();
        }

        void ConfigureSceneNavigation()
        {
            if (sceneNodes == null) return;

            var buttons = new List<Button>();
            foreach (var node in sceneNodes)
            {
                if (node == null || !UiSelectionUtility.IsSelectable(node.button)) continue;
                buttons.Add(node.button);
            }

            if (UiSelectionUtility.IsSelectable(lobbyButton)) buttons.Add(lobbyButton);

            foreach (var button in buttons)
            {
                if (button == null) continue;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.wrapAround = false;
                navigation.selectOnUp = BestDirectionalSelectable(button, buttons, Vector2.up);
                navigation.selectOnDown = BestDirectionalSelectable(button, buttons, Vector2.down);
                navigation.selectOnLeft = BestDirectionalSelectable(button, buttons, Vector2.left);
                navigation.selectOnRight = BestDirectionalSelectable(button, buttons, Vector2.right);
                button.navigation = navigation;
            }

            ApplyUnlockShieldNavigationOverride();
        }

        void ApplyUnlockShieldNavigationOverride()
        {
            var shieldNode = FindSceneNode(UpgradeType.UnlockShield);
            if (shieldNode == null || !UiSelectionUtility.IsSelectable(shieldNode.button)) return;

            var shieldNavigation = shieldNode.button.navigation;
            var shieldCenter = CenterInGraph(shieldNode.button.transform as RectTransform);
            Button nearestUpperPrerequisite = null;
            float nearestUpperDistance = float.PositiveInfinity;
            Button nearestLowerChild = null;
            float nearestLowerDistance = float.PositiveInfinity;

            var prerequisites = shieldNode.EffectivePrerequisites();
            foreach (var prerequisite in prerequisites)
            {
                var prerequisiteNode = FindSceneNode(prerequisite);
                if (prerequisiteNode == null || !UiSelectionUtility.IsSelectable(prerequisiteNode.button)) continue;

                var prerequisiteNavigation = prerequisiteNode.button.navigation;
                if (IsBelow(prerequisiteNode.button, shieldNode.button))
                {
                    prerequisiteNavigation.selectOnDown = shieldNode.button;
                    prerequisiteNode.button.navigation = prerequisiteNavigation;

                    float distance = Vector2.Distance(CenterInGraph(prerequisiteNode.button.transform as RectTransform), shieldCenter);
                    if (distance < nearestUpperDistance)
                    {
                        nearestUpperDistance = distance;
                        nearestUpperPrerequisite = prerequisiteNode.button;
                    }
                }
            }

            foreach (var node in sceneNodes)
            {
                if (node == null || node == shieldNode || !UiSelectionUtility.IsSelectable(node.button)) continue;
                if (!HasPrerequisite(node, UpgradeType.UnlockShield)) continue;

                var childNavigation = node.button.navigation;
                if (IsBelow(shieldNode.button, node.button))
                {
                    childNavigation.selectOnUp = shieldNode.button;
                    node.button.navigation = childNavigation;

                    float distance = Vector2.Distance(CenterInGraph(node.button.transform as RectTransform), shieldCenter);
                    if (distance < nearestLowerDistance)
                    {
                        nearestLowerDistance = distance;
                        nearestLowerChild = node.button;
                    }
                }
            }

            if (nearestUpperPrerequisite != null) shieldNavigation.selectOnUp = nearestUpperPrerequisite;
            if (nearestLowerChild != null) shieldNavigation.selectOnDown = nearestLowerChild;
            shieldNode.button.navigation = shieldNavigation;
        }

        SkillNodeView FindSceneNode(UpgradeType type)
        {
            if (sceneNodes == null) return null;
            foreach (var node in sceneNodes)
            {
                if (node != null && node.type == type) return node;
            }

            return null;
        }

        static bool HasPrerequisite(SkillNodeView node, UpgradeType prerequisite)
        {
            if (node == null) return false;
            var prerequisites = node.EffectivePrerequisites();
            foreach (var candidate in prerequisites)
            {
                if (candidate == prerequisite) return true;
            }

            return false;
        }

        bool IsBelow(Button upper, Button lower)
        {
            return CenterInGraph(lower.transform as RectTransform).y < CenterInGraph(upper.transform as RectTransform).y;
        }

        Selectable BestDirectionalSelectable(Button origin, List<Button> candidates, Vector2 direction)
        {
            if (origin == null || candidates == null) return null;

            Vector2 originCenter = CenterInGraph(origin.transform as RectTransform);
            Selectable best = null;
            float bestScore = float.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate == origin) continue;

                Vector2 offset = CenterInGraph(candidate.transform as RectTransform) - originCenter;
                float forward = Vector2.Dot(offset, direction);
                if (forward <= 1f) continue;

                float perpendicular = Mathf.Abs(direction.x * offset.y - direction.y * offset.x);
                float score = perpendicular * 4f + forward;
                if (score >= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        Vector2 CenterInGraph(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            var graphRect = graphRoot as RectTransform;
            if (graphRect == null) return rect.position;

            var worldCenter = rect.TransformPoint(rect.rect.center);
            return graphRect.InverseTransformPoint(worldCenter);
        }

        void RestoreFocus(SkillNodeView preferredFocusNode)
        {
            if (preferredFocusNode != null)
            {
                preferredFocusNode.ResolveReferences();
                if (UiSelectionUtility.IsSelectable(preferredFocusNode.button))
                {
                    UiSelectionUtility.SelectFirst(SelectionCandidates(preferredFocusNode.button));
                    return;
                }
            }

            UiSelectionUtility.SelectFirst(SelectionCandidates(FirstSelectableNodeButton()));
        }

        static void ConfigureNodeFocusHighlight(Button button)
        {
            if (button == null) return;
            var highlight = button.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = button.gameObject.AddComponent<UiSelectionHighlight>();
            highlight.forceSelected = false;
            highlight.padding = 7f;
            highlight.thickness = 3f;
            highlight.focusColor = Color.white;
            highlight.shadowColor = new Color(0f, 0f, 0f, 0.78f);
            highlight.showBackgroundOnFocus = false;
            highlight.bringToFrontOnHighlight = false;
        }

    }
}
