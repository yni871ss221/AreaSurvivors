using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Testing
{
    public sealed class GameplayTestBootstrap : MonoBehaviour
    {
        public GameplayTestScenario scenario;
        public GameConfig config;
        public GameObject enemyPrefab;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public string gameSceneName = SceneNames.Game;

        void Start()
        {
#if UNITY_EDITOR
            string scenarioPath = UnityEditor.EditorPrefs.GetString("AreaSurvivors.GameplayTestScenarioPath", string.Empty);
            if (!string.IsNullOrEmpty(scenarioPath))
            {
                var selectedScenario = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(scenarioPath);
                if (selectedScenario != null) scenario = selectedScenario;
            }
#endif
            if (scenario == null || config == null)
            {
                Debug.LogError("[GameplayTest] Bootstrap references are incomplete.");
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Additive);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive || scene.name != gameSceneName) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            var runnerObject = new GameObject("Gameplay Test Runner");
            runnerObject.SetActive(false);
            var runner = runnerObject.AddComponent<GameplayTestRunner>();
            runner.scenario = scenario;
            runner.config = config;
            runner.grid = FindObjectOfType<TileGrid>();
            runner.landmarkSpawner = FindObjectOfType<NaturalLandmarkSpawner>();
            runner.enemyPrefab = enemyPrefab;
            runner.xpOrbPrefab = xpOrbPrefab;
            runner.damagePopupPrefab = damagePopupPrefab;
            runnerObject.SetActive(true);
        }
    }
}
