using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Tests
{
    public sealed class CharacterOcclusionRevealTests
    {
        [Test]
        public void FootprintObjectOcclusionUsesGridRootInsteadOfYSortOffset()
        {
            var root = new GameObject("Tower");
            var child = new GameObject("Tower Visual");
            child.transform.SetParent(root.transform, false);

            try
            {
                root.transform.position = new Vector3(0f, 10f, 0f);
                var gridVisual = root.AddComponent(RequiredType("AreaSurvivors.GridObjectVisual"));
                gridVisual.GetType().GetMethod("ConfigureFootprint").Invoke(gridVisual, new object[] { new Vector2Int(3, 3) });

                var ySort = root.AddComponent(RequiredType("AreaSurvivors.YSort"));
                ySort.GetType().GetField("sortPivotOffsetY").SetValue(ySort, -1.2f);

                var renderer = child.AddComponent<MeshRenderer>();

                Assert.AreEqual(10f, ComputeOccluderFrontY(renderer), 0.0001f);
                Assert.IsFalse(IsOccluderInFrontOfCharacter(renderer, 9.9f, 0));
                Assert.IsTrue(IsOccluderInFrontOfCharacter(renderer, 10.1f, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonGridOccluderFallsBackToYSortOffset()
        {
            var root = new GameObject("Legacy Occluder");
            var child = new GameObject("Legacy Visual");
            child.transform.SetParent(root.transform, false);

            try
            {
                root.transform.position = new Vector3(0f, 10f, 0f);
                var ySort = root.AddComponent(RequiredType("AreaSurvivors.YSort"));
                ySort.GetType().GetField("sortPivotOffsetY").SetValue(ySort, -1.2f);

                var renderer = child.AddComponent<MeshRenderer>();

                Assert.AreEqual(8.8f, ComputeOccluderFrontY(renderer), 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CenterTowerPrefabDoesNotUseYSortOffsetForGridObject()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/CenterTower.prefab");
            Assert.NotNull(prefab);

            var ySort = prefab.GetComponent(RequiredType("AreaSurvivors.YSort"));
            Assert.NotNull(ySort);
            Assert.AreEqual(0f, (float)ySort.GetType().GetField("sortPivotOffsetY").GetValue(ySort), 0.0001f);
        }

        static float ComputeOccluderFrontY(Renderer renderer)
        {
            var method = RequiredType("AreaSurvivors.CharacterOcclusionReveal")
                .GetMethod("ComputeOccluderFrontY", BindingFlags.Public | BindingFlags.Static);
            return (float)method.Invoke(null, new object[] { renderer });
        }

        static bool IsOccluderInFrontOfCharacter(Renderer renderer, float characterY, int sourceOrder)
        {
            var method = RequiredType("AreaSurvivors.CharacterOcclusionReveal")
                .GetMethod("IsOccluderInFrontOfCharacter", BindingFlags.Public | BindingFlags.Static);
            return (bool)method.Invoke(null, new object[] { renderer, characterY, sourceOrder });
        }

        static Type RequiredType(string fullName)
        {
            var type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.NotNull(type, $"{fullName} was not found in Assembly-CSharp.");
            return type;
        }
    }
}
