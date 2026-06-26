using UnityEngine;

namespace AreaSurvivors
{
    public sealed class BuildingPrefabVisualSet : MonoBehaviour
    {
        public bool usePrefabLayout = true;
        public PaperMeshVisual completeVisual;
        public PaperMeshVisual upgradedCompleteVisual;
        public Sprite destroyedCompleteSprite;
        public Sprite destroyedUpgradedCompleteSprite;
        public PaperMeshVisual destroyedCompleteVisual;
        public PaperMeshVisual destroyedUpgradedCompleteVisual;
        public PaperMeshVisual sparkleVisual;

        public bool HasBaseVisuals => completeVisual != null;
        public bool HasUpgradeVisuals => upgradedCompleteVisual != null;

        void Awake()
        {
            BindMissingVisualsFromChildren();
            DisableBillboardsForBuildingVisuals();
            ApplyInitialVisibility();
        }

        public void BindMissingVisualsFromChildren()
        {
            if (completeVisual == null) completeVisual = FindVisual("Complete Image");
            if (upgradedCompleteVisual == null) upgradedCompleteVisual = FindVisual("Upgraded Building Image");
            if (destroyedCompleteVisual == null) destroyedCompleteVisual = FindVisual("Destroyed Image");
            if (destroyedUpgradedCompleteVisual == null) destroyedUpgradedCompleteVisual = FindVisual("Destroyed Upgraded Image");
            if (sparkleVisual == null) sparkleVisual = FindVisual("Completion Sparkle");
            ConfigureSparkleVisual();
        }

        public void DisableBillboardsForBuildingVisuals()
        {
            DisableBillboard(completeVisual);
            DisableBillboard(upgradedCompleteVisual);
            DisableBillboard(destroyedCompleteVisual);
            DisableBillboard(destroyedUpgradedCompleteVisual);
        }

        public void ApplyInitialVisibility()
        {
            SetFill(completeVisual, 1f);
            SetFill(upgradedCompleteVisual, 1f);
            SetVisible(completeVisual, true);
            SetVisible(upgradedCompleteVisual, false);
            SetVisible(destroyedCompleteVisual, false);
            SetVisible(destroyedUpgradedCompleteVisual, false);
            SetVisible(sparkleVisual, false);
        }

        public bool ApplyDestroyedVisual(bool upgraded)
        {
            BindMissingVisualsFromChildren();

            var targetVisual = upgraded && destroyedUpgradedCompleteVisual != null
                ? destroyedUpgradedCompleteVisual
                : destroyedCompleteVisual;
            var destroyedSprite = targetVisual != null && targetVisual.sprite != null
                ? targetVisual.sprite
                : upgraded && destroyedUpgradedCompleteSprite != null
                    ? destroyedUpgradedCompleteSprite
                    : destroyedCompleteSprite;

            if (destroyedSprite == null || targetVisual == null) return false;

            SetVisible(completeVisual, false);
            SetVisible(upgradedCompleteVisual, false);
            SetVisible(destroyedCompleteVisual, targetVisual == destroyedCompleteVisual);
            SetVisible(destroyedUpgradedCompleteVisual, targetVisual == destroyedUpgradedCompleteVisual);
            SetVisible(sparkleVisual, false);
            targetVisual.SetVerticalFill(1f);
            targetVisual.sprite = destroyedSprite;
            targetVisual.color = Color.white;

            var ySort = GetComponent<YSort>();
            if (ySort != null)
            {
                ySort.renderers = new[] { targetVisual.Renderer };
                ySort.Apply();
            }
            return true;
        }

        void ConfigureSparkleVisual()
        {
            if (sparkleVisual == null) return;
            sparkleVisual.order = 22030;
            var billboard = sparkleVisual.GetComponent<PaperBillboard>();
            if (billboard != null) billboard.faceCamera = true;
            if (sparkleVisual.GetComponent<PreserveSortingOrder>() == null)
                sparkleVisual.gameObject.AddComponent<PreserveSortingOrder>();
            var outline = sparkleVisual.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = sparkleVisual.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
        }

        static void SetVisible(PaperMeshVisual visual, bool visible)
        {
            if (visual == null) return;
            visual.visible = visible;
        }

        static void SetFill(PaperMeshVisual visual, float fill)
        {
            if (visual == null) return;
            visual.SetVerticalFill(fill);
        }

        PaperMeshVisual FindVisual(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<PaperMeshVisual>() : null;
        }

        static void DisableBillboard(PaperMeshVisual visual)
        {
            if (visual == null) return;
            var billboard = visual.GetComponent<PaperBillboard>();
            if (billboard != null) billboard.faceCamera = false;
        }
    }
}
