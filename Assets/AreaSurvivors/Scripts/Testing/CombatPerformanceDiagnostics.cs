using System;
using System.Diagnostics;

namespace AreaSurvivors
{
    public static class CombatPerformanceDiagnostics
    {
        public struct Snapshot
        {
            public long areaOverlapQueries;
            public long areaColliderCandidates;
            public long areaDamageAttempts;
            public long areaDamageHits;
            public long groundTargetScans;
            public long groundTargetCandidates;
            public long groundStrikeSpawns;
            public long projectileTriggerCallbacks;
            public long projectileTargetScans;
            public long projectileTargetCandidates;
            public long projectileOverlapQueries;
            public long projectileColliderCandidates;
            public long projectileDamageAttempts;
            public long projectileDamageHits;
            public long excaliburProjectileDamageHits;
            public long bananaProjectileDamageHits;
            public long otherProjectileDamageHits;
            public long auraPaintCalls;
            public long excaliburPaintCalls;
            public long otherTrailPaintCalls;
            public long excaliburShapeRebuilds;
            public long excaliburShapeManagedBytes;
            public long damageFeedbackEvents;
            public long damagePopupRequests;
            public long damagePopupSpawns;
            public long damagePopupInstancesCreated;
            public long damagePopupReuses;
            public long damagePopupDrops;
            public long hitFlashPlayRequests;
            public long hitFlashCoalescedRequests;
            public long hitFlashComponentCreates;
            public long hitFlashOverlayCreates;
            public long enemyDeaths;
            public long xpOrbSpawns;
            public long pickupProximityScans;
            public long pickupScanCandidates;
            public long pickupAttractionsStarted;
            public long pickupMovementTicks;
            public long bananaOverlapQueries;
            public long bananaColliderCandidates;
            public long summonedEnemySpawnAttempts;
            public long summonedEnemySpawns;
            public long summonedEnemyCapBlocked;

            public string ToCompactString()
            {
                return
                    $"areaQueries={areaOverlapQueries}; areaCandidates={areaColliderCandidates}; " +
                    $"areaAttempts={areaDamageAttempts}; areaHits={areaDamageHits}; " +
                    $"groundScans={groundTargetScans}; groundCandidates={groundTargetCandidates}; groundSpawns={groundStrikeSpawns}; " +
                    $"projectileTriggers={projectileTriggerCallbacks}; projectileTargetScans={projectileTargetScans}; " +
                    $"projectileTargetCandidates={projectileTargetCandidates}; projectileQueries={projectileOverlapQueries}; " +
                    $"projectileCandidates={projectileColliderCandidates}; projectileAttempts={projectileDamageAttempts}; " +
                    $"projectileHits={projectileDamageHits}; excaliburHits={excaliburProjectileDamageHits}; " +
                    $"bananaHits={bananaProjectileDamageHits}; otherProjectileHits={otherProjectileDamageHits}; " +
                    $"auraPaint={auraPaintCalls}; excaliburPaint={excaliburPaintCalls}; " +
                    $"otherTrailPaint={otherTrailPaintCalls}; " +
                    $"excaliburShape={excaliburShapeRebuilds}; excaliburShapeBytes={excaliburShapeManagedBytes}; " +
                    $"feedback={damageFeedbackEvents}; popupRequests={damagePopupRequests}; popupSpawns={damagePopupSpawns}; " +
                    $"popupCreated={damagePopupInstancesCreated}; popupReuses={damagePopupReuses}; popupDrops={damagePopupDrops}; " +
                    $"flashRequests={hitFlashPlayRequests}; flashCoalesced={hitFlashCoalescedRequests}; " +
                    $"flashComponents={hitFlashComponentCreates}; " +
                    $"flashOverlays={hitFlashOverlayCreates}; deaths={enemyDeaths}; xpOrbs={xpOrbSpawns}; " +
                    $"pickupScans={pickupProximityScans}; pickupCandidates={pickupScanCandidates}; " +
                    $"pickupStarts={pickupAttractionsStarted}; pickupMoves={pickupMovementTicks}; " +
                    $"bananaQueries={bananaOverlapQueries}; bananaCandidates={bananaColliderCandidates}; " +
                    $"summonAttempts={summonedEnemySpawnAttempts}; summonSpawns={summonedEnemySpawns}; " +
                    $"summonCapBlocked={summonedEnemyCapBlocked}";
            }
        }

        static Snapshot counters;

        public static bool IsRecording { get; private set; }
        public static bool ContinuousRecordingEnabled { get; set; }
        public static bool SuppressDamagePopups { get; set; }
        public static bool SuppressHitFlash { get; set; }
        static bool ShouldRecord => IsRecording || ContinuousRecordingEnabled;

        public static Snapshot GetCurrentSnapshot()
        {
            return counters;
        }

        public static void BeginRecording()
        {
            counters = default;
            IsRecording = true;
            ResetModeOverrides();
        }

        public static Snapshot EndRecording()
        {
            IsRecording = false;
            var result = counters;
            ResetModeOverrides();
            return result;
        }

        public static void ResetModeOverrides()
        {
            SuppressDamagePopups = false;
            SuppressHitFlash = false;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordAreaOverlapQuery(int colliderCandidates)
        {
            if (!ShouldRecord) return;
            counters.areaOverlapQueries++;
            counters.areaColliderCandidates += Math.Max(0, colliderCandidates);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordAreaDamageAttempt()
        {
            if (ShouldRecord) counters.areaDamageAttempts++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordAreaDamageHit()
        {
            if (ShouldRecord) counters.areaDamageHits++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordGroundTargetScan(int enemyCandidates)
        {
            if (!ShouldRecord) return;
            counters.groundTargetScans++;
            counters.groundTargetCandidates += Math.Max(0, enemyCandidates);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordGroundStrikeSpawn()
        {
            if (ShouldRecord) counters.groundStrikeSpawns++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileTriggerCallback()
        {
            if (ShouldRecord) counters.projectileTriggerCallbacks++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileTargetScan(int enemyCandidates)
        {
            if (!ShouldRecord) return;
            counters.projectileTargetScans++;
            counters.projectileTargetCandidates += Math.Max(0, enemyCandidates);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileOverlapQuery(int colliderCandidates)
        {
            if (!ShouldRecord) return;
            counters.projectileOverlapQueries++;
            counters.projectileColliderCandidates += Math.Max(0, colliderCandidates);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileDamageAttempt()
        {
            if (ShouldRecord) counters.projectileDamageAttempts++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileDamageHit(WeaponType type)
        {
            if (!ShouldRecord) return;
            counters.projectileDamageHits++;
            if (type == WeaponType.Excalibur) counters.excaliburProjectileDamageHits++;
            else if (type == WeaponType.Banana) counters.bananaProjectileDamageHits++;
            else counters.otherProjectileDamageHits++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordAttackPaint(WeaponType type)
        {
            if (!ShouldRecord) return;
            if (type == WeaponType.Excalibur) counters.excaliburPaintCalls++;
            else if (type == WeaponType.AuraSword) counters.auraPaintCalls++;
            else counters.otherTrailPaintCalls++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordExcaliburShapeRebuild(int estimatedManagedBytes)
        {
            if (!ShouldRecord) return;
            counters.excaliburShapeRebuilds++;
            counters.excaliburShapeManagedBytes += Math.Max(0, estimatedManagedBytes);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamageFeedbackEvent()
        {
            if (ShouldRecord) counters.damageFeedbackEvents++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamagePopupRequest()
        {
            if (ShouldRecord) counters.damagePopupRequests++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamagePopupSpawn()
        {
            if (ShouldRecord) counters.damagePopupSpawns++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamagePopupInstanceCreate()
        {
            if (ShouldRecord) counters.damagePopupInstancesCreated++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamagePopupReuse()
        {
            if (ShouldRecord) counters.damagePopupReuses++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordDamagePopupDrop()
        {
            if (ShouldRecord) counters.damagePopupDrops++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordHitFlashPlayRequest()
        {
            if (ShouldRecord) counters.hitFlashPlayRequests++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordHitFlashCoalescedRequest()
        {
            if (ShouldRecord) counters.hitFlashCoalescedRequests++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordHitFlashComponentCreate()
        {
            if (ShouldRecord) counters.hitFlashComponentCreates++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordHitFlashOverlayCreate()
        {
            if (ShouldRecord) counters.hitFlashOverlayCreates++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordEnemyDeath()
        {
            if (ShouldRecord) counters.enemyDeaths++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordXpOrbSpawn()
        {
            if (ShouldRecord) counters.xpOrbSpawns++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordPickupProximityScan(int candidates, int attractionsStarted)
        {
            if (!ShouldRecord) return;
            counters.pickupProximityScans++;
            counters.pickupScanCandidates += Math.Max(0, candidates);
            counters.pickupAttractionsStarted += Math.Max(0, attractionsStarted);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordPickupMovementTicks(int activePickups)
        {
            if (!ShouldRecord) return;
            counters.pickupMovementTicks += Math.Max(0, activePickups);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordBananaOverlapQuery(int colliderCandidates)
        {
            if (!ShouldRecord) return;
            counters.bananaOverlapQueries++;
            counters.bananaColliderCandidates += Math.Max(
                0,
                colliderCandidates);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordSummonedEnemySpawnAttempt()
        {
            if (ShouldRecord) counters.summonedEnemySpawnAttempts++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordSummonedEnemySpawned()
        {
            if (ShouldRecord) counters.summonedEnemySpawns++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordSummonedEnemyCapBlocked(int count = 1)
        {
            if (!ShouldRecord) return;
            counters.summonedEnemyCapBlocked += Math.Max(0, count);
        }
    }
}
