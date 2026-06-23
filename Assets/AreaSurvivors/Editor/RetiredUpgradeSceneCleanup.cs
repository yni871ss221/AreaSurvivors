using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class RetiredUpgradeSceneCleanup
    {
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";

        [MenuItem("AreaSurvivors/Cleanup/Remove Retired Upgrade Nodes")]
        public static void RemoveRetiredUpgradeNodes()
        {
            var scene = EditorSceneManager.OpenScene(UpgradeScenePath);
            var roots = scene.GetRootGameObjects();
            int removedNodes = 0;
            int cleanedPrerequisites = 0;

            foreach (var root in roots)
            {
                var nodes = root.GetComponentsInChildren<SkillNodeView>(true);
                foreach (var node in nodes)
                {
                    if (node == null || !ProgressionStore.IsRetiredUpgrade(node.type)) continue;
                    Object.DestroyImmediate(node.gameObject);
                    removedNodes++;
                }
            }

            foreach (var root in roots)
            {
                var nodes = root.GetComponentsInChildren<SkillNodeView>(true);
                foreach (var node in nodes)
                {
                    if (node == null) continue;
                    cleanedPrerequisites += RemoveRetiredPrerequisites(node);
                    EditorUtility.SetDirty(node);
                }

                foreach (var validator in root.GetComponentsInChildren<SkillTreeLayoutValidator>(true))
                {
                    validator.RebuildSceneLinkSegments();
                    EditorUtility.SetDirty(validator);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Removed retired upgrade nodes: nodes={removedNodes}, prerequisites={cleanedPrerequisites}");
        }

        [MenuItem("AreaSurvivors/Cleanup/Remove Retired Lobby Character Cards")]
        public static void RemoveRetiredLobbyCharacterCards()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath);
            var roots = scene.GetRootGameObjects();
            int removed = 0;

            foreach (var root in roots)
            {
                removed += DestroyChildrenNamed(root.transform, "Character Archer");
                removed += DestroyChildrenNamed(root.transform, "Character Mage");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Removed retired lobby character cards: removed={removed}");
        }

        static int RemoveRetiredPrerequisites(SkillNodeView node)
        {
            int removed = 0;

            if (node.prerequisites != null && node.prerequisites.Length > 0)
            {
                var kept = new List<UpgradeType>(node.prerequisites.Length);
                foreach (var prerequisite in node.prerequisites)
                {
                    if (ProgressionStore.IsRetiredUpgrade(prerequisite))
                    {
                        removed++;
                        continue;
                    }

                    kept.Add(prerequisite);
                }

                node.prerequisites = kept.ToArray();
            }

            if (node.linkRoutes != null && node.linkRoutes.Length > 0)
            {
                var kept = new List<SkillNodeView.SkillLinkRoute>(node.linkRoutes.Length);
                foreach (var route in node.linkRoutes)
                {
                    if (route == null || ProgressionStore.IsRetiredUpgrade(route.prerequisite))
                    {
                        removed++;
                        continue;
                    }

                    kept.Add(route);
                }

                node.linkRoutes = kept.ToArray();
            }

            return removed;
        }

        static int DestroyChildrenNamed(Transform root, string name)
        {
            int removed = 0;
            var targets = new List<GameObject>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != root && transform.name == name) targets.Add(transform.gameObject);
            }

            foreach (var target in targets)
            {
                Object.DestroyImmediate(target);
                removed++;
            }

            return removed;
        }
    }
}
