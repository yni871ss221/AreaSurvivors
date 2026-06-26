using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace AreaSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class SkillTreeIconSceneBinder
    {
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const string PendingKey = "AreaSurvivors.SkillTreeIconSceneBinder.Pending";
        const string PendingFilePath = "Temp/AreaSurvivorsBindSkillIcons.flag";

        static SkillTreeIconSceneBinder()
        {
            EditorApplication.delayCall += RunPendingBind;
        }

        [MenuItem("Area Survivors/Upgrade Scene/Bind Skill Icons")]
        public static void BindSkillIcons()
        {
            var scene = EditorSceneManager.OpenScene(UpgradeScenePath, OpenSceneMode.Single);
            int changed = 0;
            var nodes = Object.FindObjectsOfType<SkillNodeView>(true);
            foreach (var node in nodes)
            {
                if (node == null) continue;
                node.ResolveReferences();
                if (node.icon == null) continue;

                var sprite = StatIconCatalog.Load(StatIconCatalog.ForUpgrade(node.type));
                if (sprite == null)
                {
                    Debug.LogWarning($"Missing skill icon for {node.type}");
                    continue;
                }

                if (node.icon.sprite == sprite) continue;
                Undo.RecordObject(node.icon, "Bind Skill Icon");
                node.icon.sprite = sprite;
                node.icon.preserveAspect = true;
                EditorUtility.SetDirty(node.icon);
                changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Bound {changed} skill icon sprite references in {UpgradeScenePath}.");
        }

        [MenuItem("Area Survivors/Upgrade Scene/Queue Skill Icon Bind")]
        public static void QueueBindSkillIcons()
        {
            EditorPrefs.SetBool(PendingKey, true);
            RunPendingBind();
        }

        static void RunPendingBind()
        {
            bool hasPendingFile = File.Exists(PendingFilePath);
            if (!EditorPrefs.GetBool(PendingKey, false) && !hasPendingFile) return;
            EditorPrefs.SetBool(PendingKey, false);
            if (hasPendingFile) File.Delete(PendingFilePath);
            BindSkillIcons();
        }
    }
}
