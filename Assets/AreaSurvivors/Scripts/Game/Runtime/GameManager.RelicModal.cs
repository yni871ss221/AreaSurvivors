using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed partial class GameManager
    {        public void ShowAnnouncement(string message)
        {
            gameHud?.ShowAnnouncement(message);
        }

        public void ShowRelicAcquisition(RelicDefinition definition)
        {
            ShowRelicAcquisition(definition, 0);
        }

        public void ShowRelicAcquisition(RelicDefinition definition, int duplicateTokenReward)
        {
            ShowRelicAcquisition(definition, duplicateTokenReward, null);
        }

        public void ShowRelicAcquisition(RelicDefinition definition, int duplicateTokenReward, Action onClosed)
        {
            if (definition == null)
            {
                ShowAnnouncement(LocalizationService.Text("レリック獲得", "Relic acquired"));
                onClosed?.Invoke();
                return;
            }

            runRelics.Add(duplicateTokenReward > 0
                ? definition.displayNameSource + "（変換）"
                : definition.displayNameSource);
            tokenRuntime.AddRelicDuplicateTokens(duplicateTokenReward);
            runRelicEntries.Add(new RunRelicReportEntry
            {
                type = definition.type,
                displayName = definition.displayNameSource,
                convertedToToken = duplicateTokenReward > 0
            });
            CombatModifiersChanged?.Invoke();
            gameHud?.RefreshRelics();
            if (relicAcquisitionPanelPrefab == null)
            {
                ShowAnnouncement(duplicateTokenReward > 0
                    ? LocalizationService.Format("レリック変換: トークン +{0}", "Relic converted: +{0} tokens", duplicateTokenReward)
                    : LocalizationService.Format("レリック獲得: {0}", "Relic acquired: {0}", definition.displayName));
                onClosed?.Invoke();
                return;
            }

            BeginRelicAcquisitionModal();
            var panel = Instantiate(relicAcquisitionPanelPrefab);
            panel.Show(definition, duplicateTokenReward, () =>
            {
                EndRelicAcquisitionModal();
                ShowAnnouncement(duplicateTokenReward > 0
                    ? LocalizationService.Format("レリック変換: トークン +{0}", "Relic converted: +{0} tokens", duplicateTokenReward)
                    : LocalizationService.Format("レリック獲得: {0}", "Relic acquired: {0}", definition.displayName));
                onClosed?.Invoke();
            });
        }

        void BeginRelicAcquisitionModal()
        {
            activeRelicAcquisitionModalCount++;
            levelUpInputBlockedThroughFrame = Mathf.Max(levelUpInputBlockedThroughFrame, Time.frameCount);
            ClearLevelUpSelection();
        }

        void EndRelicAcquisitionModal()
        {
            activeRelicAcquisitionModalCount = Mathf.Max(0, activeRelicAcquisitionModalCount - 1);
            levelUpInputBlockedThroughFrame = Mathf.Max(levelUpInputBlockedThroughFrame, Time.frameCount);
            ClearLevelUpSelection();
            if (activeRelicAcquisitionModalCount == 0) TryShowNextRunLevelUp();
        }

        bool IsLevelUpInputBlockedByFrontModal()
        {
            return activeRelicAcquisitionModalCount > 0 ||
                   Time.frameCount <= levelUpInputBlockedThroughFrame;
        }

        void ClearLevelUpSelection()
        {
            if (levelUpPanel == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(levelUpPanel.transform)) return;
            EventSystem.current.SetSelectedGameObject(null);
        }

    }
}
