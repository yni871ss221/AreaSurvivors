using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UpgradeScreen : MonoBehaviour
    {
        Transform graphRoot;
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

            graphRoot = FindDeep(uiObject.transform, "Skill Tree");
            tooltipTitle = FindDeep(uiObject.transform, "Tooltip Title")?.GetComponent<Text>();
            tooltipDescription = FindDeep(uiObject.transform, "Tooltip Description")?.GetComponent<Text>();
            tokenLabel = FindDeep(uiObject.transform, "TokenLabel")?.GetComponent<Text>();

            BindSceneButton(uiObject.transform, "スキル初期化", ResetUpgradesForTesting);
            BindSceneButton(uiObject.transform, "トークン+99999", AddTestTokens);
            var nav = gameObject.GetComponent<SceneNavigator>();
            if (nav == null) nav = gameObject.AddComponent<SceneNavigator>();
            BindSceneButton(uiObject.transform, "ロビーへ", nav.LoadLobby);

            RefreshSceneTree();
            return true;
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

            var sceneLinks = graphRoot.GetComponentsInChildren<SkillLinkSegment>(true);
            if (sceneLinks != null && sceneLinks.Length > 0)
            {
                foreach (var link in sceneLinks)
                {
                    if (link == null) continue;
                    link.ApplyState(ProgressionStore.GetLevel(link.prerequisite) > 0);
                }
            }
            else
            {
                Debug.LogWarning("UpgradeScreen requires Scene-authored SkillLinkSegment objects. Runtime link generation is disabled.");
            }

            foreach (var node in sceneNodes)
            {
                ConfigureSceneNode(node, sceneNodes);
            }
        }

        void ConfigureSceneNode(SkillNodeView node, SkillNodeView[] sceneNodes)
        {
            if (node == null) return;
            if (ProgressionStore.IsRetiredUpgrade(node.type))
            {
                node.gameObject.SetActive(false);
                return;
            }

            node.gameObject.SetActive(true);
            node.ResolveReferences();

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

            if (node.icon != null)
            {
                node.icon.color = node.implemented && (purchased || affordable) ? Color.white : new Color(0.45f, 0.48f, 0.48f, 1f);
            }

            if (node.statusText != null)
            {
                node.statusText.text = !node.implemented ? "予定" : maxed ? $"Lv {level} MAX" : purchased ? $"Lv {level}" : prerequisiteMet ? $"Cost {cost}" : "LOCK";
                node.statusText.color = purchased ? new Color(0.72f, 1f, 0.74f) : new Color(0.96f, 0.90f, 0.62f);
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
                hover.titleText = node.implemented ? $"{node.title}  Lv {level}  Cost {cost}" : $"{node.title}  実装予定";
                hover.descriptionText = node.implemented ? node.description : node.description + "（現在はツリー上の予約ノードです）";
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

        void ResetUpgradesForTesting()
        {
            ProgressionStore.ResetUpgradesForTesting();
            RefreshSceneTree();
        }

        void AddTestTokens()
        {
            ProgressionStore.AddTokensForTesting(99999);
            RefreshSceneTree();
        }

    }
}
