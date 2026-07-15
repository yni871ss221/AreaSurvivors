using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class CombatVisualRotationGuard
    {
        const string WeaponPrefabRoot = "Assets/AreaSurvivors/Prefabs/Weapons";
        const string ReportRelativePath = "Library/AreaSafeUnity/combat-visual-rotation-report.txt";
        const string MarkerRelativePath = "Library/AreaSafeUnity/combat-visual-rotation-guard.ok";

        [MenuItem("Area Survivors/Reports/Combat Visual Rotation")]
        public static void ReportMenu()
        {
            Scan(false);
        }

        [MenuItem("Area Survivors/Validate/Combat Visual Rotation Guard")]
        public static void ValidateMenu()
        {
            Scan(true);
        }

        [MenuItem("Area Survivors/Migrations/Remove Forbidden Combat Visual Rotation")]
        public static void RemoveForbiddenRotationMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponPrefabRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            int changedPrefabs = 0;
            int removedBillboards = 0;
            int resetTransforms = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;
                try
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < transforms.Length; j++)
                    {
                        var target = transforms[j];
                        if (!RequiresZeroPitch(target)) continue;

                        Vector3 euler = target.localEulerAngles;
                        float pitch = Mathf.DeltaAngle(0f, euler.x);
                        float yaw = Mathf.DeltaAngle(0f, euler.y);
                        if (!Mathf.Approximately(pitch, 0f) || !Mathf.Approximately(yaw, 0f))
                        {
                            target.localRotation = Quaternion.Euler(0f, 0f, euler.z);
                            resetTransforms++;
                            changed = true;
                        }

                        var billboard = target.GetComponent<PaperBillboard>();
                        if (billboard != null && billboard.enabled && billboard.faceCamera)
                        {
                            UnityEngine.Object.DestroyImmediate(billboard, true);
                            removedBillboards++;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        changedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Removed forbidden combat visual rotation. prefabs=" + changedPrefabs +
                ", billboards=" + removedBillboards + ", transforms=" + resetTransforms);
            Scan(false);
        }

        static void Scan(bool failOnViolation)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponPrefabRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            int guardedVisuals = 0;
            var violations = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    var target = transforms[j];
                    if (!RequiresZeroPitch(target)) continue;
                    guardedVisuals++;

                    Vector3 euler = target.localEulerAngles;
                    float pitch = Mathf.DeltaAngle(0f, euler.x);
                    float yaw = Mathf.DeltaAngle(0f, euler.y);
                    if (!Mathf.Approximately(pitch, 0f) || !Mathf.Approximately(yaw, 0f))
                    {
                        violations.Add(prefabPath + " :: " + HierarchyPath(target) +
                            " :: local rotation X/Y=" + pitch.ToString("0.###") + "/" + yaw.ToString("0.###"));
                    }

                    var billboard = target.GetComponent<PaperBillboard>();
                    if (billboard != null && billboard.enabled && billboard.faceCamera)
                    {
                        violations.Add(prefabPath + " :: " + HierarchyPath(target) +
                            " :: PaperBillboard.faceCamera=true");
                    }
                }
            }

            var report = new StringBuilder();
            report.AppendLine("Combat Visual Rotation Report");
            report.AppendLine("weapon_prefabs=" + guids.Length);
            report.AppendLine("guarded_visuals=" + guardedVisuals);
            report.AppendLine("violations=" + violations.Count);
            for (int i = 0; i < violations.Count; i++) report.AppendLine(violations[i]);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString());

            if (violations.Count == 0)
            {
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                Debug.Log("Combat Visual Rotation Guard: passed. guarded visuals=" + guardedVisuals);
                return;
            }

            Debug.LogWarning("Combat Visual Rotation Guard: violations=" + violations.Count + ". Report: " + reportPath);
            if (failOnViolation)
            {
                throw new InvalidOperationException("Combat Visual Rotation Guard failed. violations=" + violations.Count);
            }
        }

        static bool RequiresZeroPitch(Transform target)
        {
            if (target.GetComponent<GroundStrikeAnimatorPlayback>() != null) return true;
            if (target.GetComponent<Animator>() != null && target.GetComponent<SpriteRenderer>() != null) return true;

            for (Transform current = target; current != null; current = current.parent)
            {
                string name = current.name;
                if (Contains(name, "Area") || Contains(name, "Range") || Contains(name, "Outline") ||
                    Contains(name, "Circle Visual"))
                {
                    return true;
                }

                var components = current.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "ArrowRainAreaVisual" || typeName == "AdvancedWeaponArea") return true;
                }
            }
            return false;
        }

        static bool Contains(string value, string part)
        {
            return value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string HierarchyPath(Transform target)
        {
            var names = new List<string>();
            for (Transform current = target; current != null; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
