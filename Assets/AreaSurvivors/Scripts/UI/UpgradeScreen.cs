using System.Collections.Generic;
using UnityEngine;
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

            var nav = gameObject.GetComponent<SceneNavigator>();
            if (nav == null) nav = gameObject.AddComponent<SceneNavigator>();
            BindSceneButton(uiObject.transform, "ロビーへ", nav.LoadLobby);

            RefreshSceneTree();
            return true;
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

        static void BindSceneButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindDeep(root, name)?.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() =>
                {
                    AudioManager.PlayButtonConfirm();
                    action();
                });
            }
        }

        void RefreshSceneTree()
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

            RefreshSceneLinks();
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
            }

            if (node.button != null)
            {
                node.button.onClick.RemoveAllListeners();
                node.button.interactable = node.implemented && prerequisiteMet && affordable && !maxed;
                var colors = node.button.colors;
                colors.disabledColor = Color.white;
                node.button.colors = colors;
                node.button.onClick.AddListener(() =>
                {
                    AudioManager.PlayButtonConfirm();
                    if (ProgressionStore.TryBuy(node.type)) RefreshSceneTree();
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

    }
}
