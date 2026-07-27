using UnityEngine;

namespace AreaSurvivors
{
    internal sealed class TokenRuntimeService
    {
        internal const int PaintAreaTokenThreshold = 500;
        internal const float ElapsedTokenRewardIntervalSeconds = 30f;

        public int RunTokens { get; private set; }
        public int PaintAreaTokenProgress { get; private set; }
        public int KillTokenProgress { get; private set; }
        public int KillMilestoneTokens { get; private set; }
        public int ElapsedTimeTokens { get; private set; }
        public int TokenOrbTokens { get; private set; }
        public int PaintAreaTokens { get; private set; }
        public int RelicDuplicateTokens { get; private set; }
        public int TokenBalanceAtRunStart { get; private set; }
        public float NextElapsedTokenRewardSeconds { get; private set; } = ElapsedTokenRewardIntervalSeconds;

        public void Initialize()
        {
            TokenBalanceAtRunStart = ProgressionStore.Data.tokens;
        }

        public int AwardKillTokens(bool gameEnding, GameConfig config)
        {
            if (gameEnding || config == null) return 0;

            KillTokenProgress++;
            int threshold = Mathf.Max(1, config.tokenKillsDivisor);
            int rewards = KillTokenProgress / threshold;
            if (rewards <= 0) return 0;

            KillTokenProgress -= rewards * threshold;
            return rewards;
        }

        public int AwardElapsedTimeTokens(float elapsed, bool gameEnding)
        {
            if (gameEnding) return 0;

            int rewards = 0;
            while (elapsed + 0.0001f >= NextElapsedTokenRewardSeconds)
            {
                rewards++;
                NextElapsedTokenRewardSeconds += ElapsedTokenRewardIntervalSeconds;
            }
            return rewards;
        }

        public TokenGainResult AddRunTokens(int amount, RunTokenSource source)
        {
            int gained = Mathf.Max(0, amount);
            if (gained <= 0) return default;

            int previousAttackTier = RunTokens / 10;
            RunTokens += gained;
            TrackRunTokenSource(source, gained);
            return new TokenGainResult(gained, RunTokens / 10 != previousAttackTier);
        }

        public int CalculatePaintAreaTokenReward(int count)
        {
            int rewardLevel = ProgressionStore.GetLevel(UpgradeType.PaintAreaTokenGain);
            if (rewardLevel <= 0 || count <= 0) return 0;

            PaintAreaTokenProgress += count;
            int rewards = PaintAreaTokenProgress / PaintAreaTokenThreshold;
            if (rewards <= 0) return 0;

            PaintAreaTokenProgress -= rewards * PaintAreaTokenThreshold;
            return rewards * Mathf.Clamp(
                rewardLevel,
                1,
                ProgressionStore.GetMaxLevel(UpgradeType.PaintAreaTokenGain));
        }

        public void AddRelicDuplicateTokens(int amount)
        {
            RelicDuplicateTokens += Mathf.Max(0, amount);
        }

        public void SetElapsedTokenRewardSchedule(float elapsed)
        {
            NextElapsedTokenRewardSeconds =
                (Mathf.Floor(elapsed / ElapsedTokenRewardIntervalSeconds) + 1f) *
                ElapsedTokenRewardIntervalSeconds;
        }

        void TrackRunTokenSource(RunTokenSource source, int gained)
        {
            switch (source)
            {
                case RunTokenSource.KillMilestone:
                    KillMilestoneTokens += gained;
                    break;
                case RunTokenSource.ElapsedTime:
                    ElapsedTimeTokens += gained;
                    break;
                case RunTokenSource.PaintArea:
                    PaintAreaTokens += gained;
                    break;
                default:
                    TokenOrbTokens += gained;
                    break;
            }
        }

        internal readonly struct TokenGainResult
        {
            public readonly int gained;
            public readonly bool attackTierChanged;

            public TokenGainResult(int gained, bool attackTierChanged)
            {
                this.gained = gained;
                this.attackTierChanged = attackTierChanged;
            }
        }
    }
}
