using UnityEngine;

namespace AreaSurvivors
{
    public sealed class BuildingPrefabVisualSet : MonoBehaviour
    {
        public bool usePrefabLayout = true;
        public PaperMeshVisual ghostVisual;
        public PaperMeshVisual buildFillVisual;
        public PaperMeshVisual completeVisual;
        public PaperMeshVisual upgradedGhostVisual;
        public PaperMeshVisual upgradedBuildFillVisual;
        public PaperMeshVisual upgradedCompleteVisual;
        public Sprite upgradedOpenSprite;
        public PaperMeshVisual hammerVisual;
        public PaperMeshVisual sparkleVisual;

        public bool HasBaseVisuals => ghostVisual != null && buildFillVisual != null && completeVisual != null;
        public bool HasUpgradeVisuals => upgradedGhostVisual != null && upgradedBuildFillVisual != null && upgradedCompleteVisual != null;

        void Awake()
        {
            BindMissingVisualsFromChildren();
            DisableBillboardsForBuildingVisuals();
            ApplyInitialVisibility();
        }

        public void BindMissingVisualsFromChildren()
        {
            if (ghostVisual == null) ghostVisual = FindVisual("Ghost Image");
            if (buildFillVisual == null) buildFillVisual = FindVisual("Build Fill Image");
            if (completeVisual == null) completeVisual = FindVisual("Complete Image");
            if (upgradedGhostVisual == null) upgradedGhostVisual = FindVisual("Upgrade Ghost");
            if (upgradedBuildFillVisual == null) upgradedBuildFillVisual = FindVisual("Upgrade Build Fill");
            if (upgradedCompleteVisual == null) upgradedCompleteVisual = FindVisual("Upgraded Building Image");
            if (hammerVisual == null) hammerVisual = FindVisual("Hammer");
            if (sparkleVisual == null) sparkleVisual = FindVisual("Completion Sparkle");
        }

        public void DisableBillboardsForBuildingVisuals()
        {
            DisableBillboard(ghostVisual);
            DisableBillboard(buildFillVisual);
            DisableBillboard(completeVisual);
            DisableBillboard(upgradedGhostVisual);
            DisableBillboard(upgradedBuildFillVisual);
            DisableBillboard(upgradedCompleteVisual);
        }

        public void ApplyInitialVisibility()
        {
            SetFill(ghostVisual, 1f);
            SetFill(buildFillVisual, 1f);
            SetFill(completeVisual, 1f);
            SetFill(upgradedGhostVisual, 1f);
            SetFill(upgradedBuildFillVisual, 1f);
            SetFill(upgradedCompleteVisual, 1f);
            SetVisible(ghostVisual, true);
            SetVisible(buildFillVisual, false);
            SetVisible(completeVisual, false);
            SetVisible(upgradedGhostVisual, false);
            SetVisible(upgradedBuildFillVisual, false);
            SetVisible(upgradedCompleteVisual, false);
            SetVisible(hammerVisual, false);
            SetVisible(sparkleVisual, false);
        }

        public void ApplySpriteToBase(Sprite sprite)
        {
            SetSprite(ghostVisual, sprite);
            SetSprite(buildFillVisual, sprite);
            SetSprite(completeVisual, sprite);
        }

        public void ApplySpriteToUpgrade(Sprite sprite)
        {
            SetSprite(upgradedGhostVisual, sprite);
            SetSprite(upgradedBuildFillVisual, sprite);
            SetSprite(upgradedCompleteVisual, sprite);
        }

        static void SetSprite(PaperMeshVisual visual, Sprite sprite)
        {
            if (visual == null || sprite == null) return;
            visual.sprite = sprite;
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
