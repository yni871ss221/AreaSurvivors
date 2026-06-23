using UnityEngine;

namespace AreaSurvivors
{
    public class AutoBuildingScheduler : MonoBehaviour
    {
        public GameConfig config;
        static readonly bool DisableWorkerHutAutoGatherForPhase1 = true;

        float workerHutTimer;
        float watchTowerTimer;

        public void Configure(GameConfig runConfig)
        {
            config = runConfig;
            workerHutTimer = 0f;
            watchTowerTimer = 0f;
        }

        void Update()
        {
            if (Time.timeScale <= 0f || config == null) return;
            TickWorkerHutTimer();
            TickWatchTowerTimer();
        }

        void TickWorkerHutTimer()
        {
            if (DisableWorkerHutAutoGatherForPhase1) return;
            float interval = AutoGatherIntervalSeconds();
            workerHutTimer += Time.deltaTime;
            if (workerHutTimer < interval) return;

            workerHutTimer %= interval;
            int amount = AutoGatherAmount();
            foreach (var hut in FindObjectsOfType<WorkerHut>())
            {
                if (hut != null && hut.IsBuilt) hut.AutoGather(amount);
            }
        }

        void TickWatchTowerTimer()
        {
            float interval = Mathf.Max(0.1f, config.watchTowerAutoPaintIntervalSeconds);
            watchTowerTimer += Time.deltaTime;
            if (watchTowerTimer < interval) return;

            watchTowerTimer %= interval;
            foreach (var tower in FindObjectsOfType<WatchTower>())
            {
                if (tower != null && tower.IsBuilt) tower.AutoPaintNearestCell();
            }
        }

        float AutoGatherIntervalSeconds()
        {
            int speedLevel = ProgressionStore.GetLevel(UpgradeType.AutoResourceInterval);
            return Mathf.Max(0.5f, config.workerHutAutoGatherBaseIntervalSeconds -
                speedLevel * config.workerHutAutoGatherIntervalReductionPerLevel);
        }

        int AutoGatherAmount()
        {
            int gainLevel = ProgressionStore.GetLevel(UpgradeType.AutoResourceGain);
            return Mathf.Max(1, config.workerHutAutoGatherBaseAmount +
                gainLevel * config.workerHutAutoGatherAmountPerLevel);
        }
    }
}
