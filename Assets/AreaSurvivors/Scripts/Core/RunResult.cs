using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AreaSurvivors
{
    [System.Serializable]
    public sealed class RunRelicReportEntry
    {
        public RelicType type;
        public string displayName;
        public bool convertedToToken;
    }

    public sealed class RunResult
    {
        public int kills;
        public int damageDealt;
        public int level;
        public int tokensEarned;
        public int reachedStage;
        public float survivedSeconds;
        public bool gameClear;
        public int clearedStage;
        public int unlockedStage;
        public bool allStagesDifficultyFiveCleared;
        public string clearMessage;
        public List<string> upgrades = new List<string>();
        public List<string> acquiredRelics = new List<string>();
        public List<RunRelicReportEntry> acquiredRelicEntries = new List<RunRelicReportEntry>();
        public List<RunDamageReportEntry> damageReport = new List<RunDamageReportEntry>();

        public static RunResult Last { get; set; }
    }

    public enum RunTokenSource
    {
        KillMilestone,
        ElapsedTime,
        TokenOrb,
        PaintArea
    }

    [Serializable]
    public sealed class RunStageLogEntry
    {
        public int stage;
        public int difficulty;
        public float reachedSeconds;
        public string reachedTime;
        public bool startStage;
    }

    [Serializable]
    public sealed class RunBossClearLogEntry
    {
        public int stage;
        public int difficulty;
        public string bossName;
        public string enemyKind;
        public bool firstClear;
        public bool unlockedNextStage;
        public int unlockedStage;
        public float clearedSeconds;
        public string clearedTime;
        public int kills;
        public int level;
        public int runTokens;
        public int bossMaxHp;
        public float bossFightSeconds;
        public int playerDamageTakenDuringFight;
        public int buildingDamageTakenDuringFight;
    }

    [Serializable]
    public sealed class RunLevelLogEntry
    {
        public int level;
        public int stage;
        public float reachedSeconds;
        public string reachedTime;
        public int kills;
        public int currentXp;
        public int xpToNext;
        public int baseExperienceCollected;
        public int appliedExperienceGained;
        public float xpMultiplier;
        public string source;
    }

    [Serializable]
    public sealed class RunUpgradeLogEntry
    {
        public int level;
        public int stage;
        public float acquiredSeconds;
        public string acquiredTime;
        public string label;
    }

    [Serializable]
    public sealed class RunEnemyKindLogEntry
    {
        public int stage;
        public int difficulty;
        public string enemyKind;
        public string displayName;
        public bool elite;
        public bool boss;
        public int maxHp;
        public int attackDamage;
        public int xpValue;
        public int spawned;
        public int killed;
    }

    [Serializable]
    public sealed class RunBuildingLogEntry
    {
        public string buildingType;
        public int instanceCount;
        public int totalMaxHp;
        public int totalCurrentHp;
        public int damageTaken;
        public int destroyedCount;
        public int currentDestroyedCount;
    }

    [Serializable]
    public sealed class RunDifficultyCheckpoint
    {
        public string eventType;
        public int stage;
        public int difficulty;
        public float elapsedSeconds;
        public string elapsedTime;
        public int level;
        public int currentXp;
        public int xpToNext;
        public int kills;
        public int baseExperienceCollected;
        public int appliedExperienceGained;
        public int playerCurrentHp;
        public int playerDamageTaken;
        public int buildingDamageTaken;
        public int buildingDestroyedCount;
        public int enemySpawned;
        public int enemyKilled;
        public int peakAliveEnemies;
        public string bossName;
        public int bossMaxHp;
        public float bossFightSeconds;
    }

    [Serializable]
    public sealed class TokenRunLogEntry
    {
        public int schemaVersion = 3;
        public string sessionId;
        public string timestampLocal;
        public string timestampUtc;
        public string appVersion;
        public string unityVersion;
        public bool gameClear;
        public int startStage;
        public int startStageDifficulty;
        public int reachedStage;
        public int reachedStageDifficulty;
        public int clearedStage;
        public int unlockedStage;
        public float survivedSeconds;
        public string survivedTime;
        public int level;
        public int kills;
        public int damageDealt;
        public int runTokensBeforeEndReward;
        public int killMilestoneTokens;
        public int elapsedTimeTokens;
        public int tokenOrbTokens;
        public int paintAreaTokens;
        public int relicDuplicateTokens;
        public int guaranteedEndTokens;
        public int endTokenGainLevel;
        public float endTokenMultiplier;
        public int endTokenBaseBeforeMultiplier;
        public int finalEndRewardTokens;
        public int tokenBalanceAtRunStart;
        public int tokenBalanceBeforeEndReward;
        public int tokenBalanceAfterEndReward;
        public int totalTokenBalanceIncrease;
        public int killTokenDivisor;
        public int killTokenRemainder;
        public float elapsedTokenIntervalSeconds;
        public float nextElapsedTokenRewardSeconds;
        public int paintAreaTokenThreshold;
        public int paintAreaTokenRemainder;
        public string reachedStageSummary;
        public string bossClearSummary;
        public List<RunStageLogEntry> reachedStages = new List<RunStageLogEntry>();
        public List<RunBossClearLogEntry> bossClears = new List<RunBossClearLogEntry>();
        public List<string> upgrades = new List<string>();
        public List<string> acquiredRelics = new List<string>();
        public int currentXp;
        public int xpToNext;
        public int baseExperienceCollected;
        public int appliedExperienceGained;
        public float currentXpMultiplier;
        public int playerCurrentHp;
        public int playerMaxHp;
        public int playerMinimumHp;
        public int playerDamageTaken;
        public int playerHitCount;
        public int playerHealingReceived;
        public int playerDeaths;
        public float playerDefense;
        public float playerMoveSpeed;
        public int playerPaintRadius;
        public int playerAutoRegen;
        public int buildingDamageTaken;
        public int buildingDestroyedCount;
        public int buildingRevivedCount;
        public int enemySpawned;
        public int enemyKilled;
        public int peakAliveEnemies;
        public List<RunLevelLogEntry> levelUps = new List<RunLevelLogEntry>();
        public List<RunUpgradeLogEntry> upgradeHistory = new List<RunUpgradeLogEntry>();
        public List<RunEnemyKindLogEntry> enemyStats = new List<RunEnemyKindLogEntry>();
        public List<RunBuildingLogEntry> buildingStats = new List<RunBuildingLogEntry>();
        public List<RunDifficultyCheckpoint> difficultyCheckpoints = new List<RunDifficultyCheckpoint>();
        public List<RunDamageReportEntry> damageReport = new List<RunDamageReportEntry>();
    }

    public static class TokenRunLogger
    {
        public const string FileName = "token_run_log.jsonl";

        public static string LogFilePath => Path.Combine(GameLogPaths.LogDirectory, FileName);

        public static void Append(TokenRunLogEntry entry)
        {
            if (entry == null) return;

            try
            {
                Directory.CreateDirectory(GameLogPaths.LogDirectory);
                string json = JsonUtility.ToJson(entry);
                File.AppendAllText(LogFilePath, json + Environment.NewLine, Encoding.UTF8);
                Debug.Log("Token run log written: " + LogFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to write token run log: " + ex.Message);
            }
        }
    }

    public static class GameLogPaths
    {
        public const string LogDirectoryName = "logs";

        public static string LogDirectory => Path.Combine(ApplicationRootDirectory, LogDirectoryName);

        static string ApplicationRootDirectory
        {
            get
            {
                try
                {
                    if (!Application.isEditor)
                    {
                        var dataDirectory = new DirectoryInfo(Application.dataPath);
                        if (dataDirectory.Parent != null && Application.dataPath.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                        {
                            return dataDirectory.Parent.FullName;
                        }

                        if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory))
                        {
                            return AppDomain.CurrentDomain.BaseDirectory;
                        }
                    }

                    var assetsDirectory = new DirectoryInfo(Application.dataPath);
                    return assetsDirectory.Parent != null ? assetsDirectory.Parent.FullName : Application.dataPath;
                }
                catch
                {
                    return Application.persistentDataPath;
                }
            }
        }
    }

    public static class RuntimeFileLogger
    {
        const string FileName = "application.log";
        static bool initialized;

        public static string LogFilePath => Path.Combine(GameLogPaths.LogDirectory, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                Directory.CreateDirectory(GameLogPaths.LogDirectory);
                File.AppendAllText(
                    LogFilePath,
                    Environment.NewLine
                    + "===== Area Survivors Log Start "
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + " ====="
                    + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                return;
            }

            Application.logMessageReceived += HandleLogMessage;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        }

        static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            try
            {
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [");
                builder.Append(type);
                builder.Append("] ");
                builder.AppendLine(condition);
                if (!string.IsNullOrEmpty(stackTrace) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                {
                    builder.AppendLine(stackTrace);
                }

                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                File.AppendAllText(
                    LogFilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " [UnhandledException] "
                    + args.ExceptionObject
                    + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
