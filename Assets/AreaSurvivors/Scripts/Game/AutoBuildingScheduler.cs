using UnityEngine;

namespace AreaSurvivors
{
    public class AutoBuildingScheduler : MonoBehaviour
    {
        public GameConfig config;

        float watchTowerTimer;

        public void Configure(GameConfig runConfig)
        {
            config = runConfig;
            watchTowerTimer = 0f;
        }

        void Update()
        {
            if (Time.timeScale <= 0f || config == null) return;
            TickWatchTowerTimer();
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

    }
}
