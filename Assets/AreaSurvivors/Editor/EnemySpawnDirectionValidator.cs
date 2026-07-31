using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class EnemySpawnDirectionValidator
    {
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const float ExpectedDirectionChangeSeconds = 30f;
        const float ExpectedSpawnArcDegrees = 60f;

        static readonly float[] ExpectedDirectionDegrees =
        {
            75f, 60f, 30f, 15f,
            345f, 330f, 300f, 285f,
            255f, 240f, 210f, 195f,
            165f, 150f, 120f, 105f
        };

        [MenuItem("Area Survivors/Validate/Enemy Spawn Direction Rules")]
        public static void ValidateFromMenu()
        {
            var failures = new List<string>();
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                failures.Add($"GameConfig was not found at {ConfigPath}.");
            }
            else
            {
                ValidateConfig(config, failures);
            }

            ValidateDirections(failures);
            if (config != null)
            {
                ValidateSpawnRanges(config, failures);
            }
            ValidateDirectionChanges(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Enemy spawn direction validation failed:\n- " +
                    string.Join("\n- ", failures));
            }

            Debug.Log(
                "Enemy spawn direction validation passed. Sixteen non-principal directions are used as " +
                "spawn centers, each enemy spawns within ±30 degrees, and later centers change by ±2 to ±4.");
        }

        static void ValidateConfig(GameConfig config, List<string> failures)
        {
            if (!Mathf.Approximately(
                    config.spawnDirectionChangeSeconds,
                    ExpectedDirectionChangeSeconds))
            {
                failures.Add(
                    $"spawnDirectionChangeSeconds must be {ExpectedDirectionChangeSeconds}, but was " +
                    $"{config.spawnDirectionChangeSeconds}.");
            }

            if (!Mathf.Approximately(config.spawnDirectionArcDegrees, ExpectedSpawnArcDegrees))
            {
                failures.Add(
                    $"spawnDirectionArcDegrees must be {ExpectedSpawnArcDegrees}, but was " +
                    $"{config.spawnDirectionArcDegrees}.");
            }
        }

        static void ValidateDirections(List<string> failures)
        {
            if (EnemySpawner.SpawnDirectionCount != ExpectedDirectionDegrees.Length)
            {
                failures.Add(
                    $"SpawnDirectionCount must be {ExpectedDirectionDegrees.Length}, but was " +
                    $"{EnemySpawner.SpawnDirectionCount}.");
                return;
            }

            var actualDirections = new HashSet<int>();
            for (int index = 0; index < ExpectedDirectionDegrees.Length; index++)
            {
                float actual = EnemySpawner.DirectionDegreesForIndex(index);
                float expected = ExpectedDirectionDegrees[index];
                if (!Mathf.Approximately(actual, expected))
                {
                    failures.Add(
                        $"Direction {index + 1} must be {expected} degrees, but was {actual}.");
                }

                int angleStep = Mathf.RoundToInt(actual / 15f);
                if (!Mathf.Approximately(actual, angleStep * 15f))
                {
                    failures.Add($"Direction {index + 1} ({actual} degrees) is not on the 15-degree grid.");
                }

                if (EnemySpawner.IsPrincipalAxisDirection(actual))
                {
                    failures.Add(
                        $"Direction {index + 1} ({actual} degrees) must not be horizontal, vertical, or diagonal.");
                }

                if (!actualDirections.Add(Mathf.RoundToInt(Mathf.Repeat(actual, 360f))))
                {
                    failures.Add($"Direction {index + 1} duplicates {actual} degrees.");
                }
            }

            for (int angle = 0; angle < 360; angle += 15)
            {
                bool shouldExist = angle % 45 != 0;
                bool exists = actualDirections.Contains(angle);
                if (exists != shouldExist)
                {
                    failures.Add(
                        $"{angle} degrees should {(shouldExist ? "" : "not ")}be included in the 16 directions.");
                }
            }
        }

        static void ValidateSpawnRanges(GameConfig config, List<string> failures)
        {
            float expectedHalfArc = ExpectedSpawnArcDegrees * 0.5f;
            for (int index = 0; index < EnemySpawner.SpawnDirectionCount; index++)
            {
                float center = EnemySpawner.DirectionDegreesForIndex(index);
                float minimum = EnemySpawner.ResolveSpawnAngleDegrees(
                    center,
                    config.spawnDirectionArcDegrees,
                    0f);
                float midpoint = EnemySpawner.ResolveSpawnAngleDegrees(
                    center,
                    config.spawnDirectionArcDegrees,
                    0.5f);
                float maximum = EnemySpawner.ResolveSpawnAngleDegrees(
                    center,
                    config.spawnDirectionArcDegrees,
                    1f);

                if (!Mathf.Approximately(minimum, center - expectedHalfArc) ||
                    !Mathf.Approximately(midpoint, center) ||
                    !Mathf.Approximately(maximum, center + expectedHalfArc))
                {
                    failures.Add(
                        $"Direction {index + 1} must spawn from {center - expectedHalfArc} through " +
                        $"{center + expectedHalfArc} degrees around center {center}.");
                }
            }

            for (int principalAngle = 0; principalAngle < 360; principalAngle += 45)
            {
                bool canSpawnAtPrincipalAngle = false;
                for (int index = 0; index < EnemySpawner.SpawnDirectionCount; index++)
                {
                    float center = EnemySpawner.DirectionDegreesForIndex(index);
                    float delta = Mathf.DeltaAngle(center, principalAngle);
                    if (Mathf.Abs(delta) > expectedHalfArc) continue;

                    float randomUnit = (delta + expectedHalfArc) / ExpectedSpawnArcDegrees;
                    float resolved = EnemySpawner.ResolveSpawnAngleDegrees(
                        center,
                        config.spawnDirectionArcDegrees,
                        randomUnit);
                    if (Mathf.Abs(Mathf.DeltaAngle(resolved, principalAngle)) <= 0.001f)
                    {
                        canSpawnAtPrincipalAngle = true;
                        break;
                    }
                }

                if (!canSpawnAtPrincipalAngle)
                {
                    failures.Add(
                        $"Principal angle {principalAngle} degrees must remain available inside a spawn range.");
                }
            }
        }

        static void ValidateDirectionChanges(List<string> failures)
        {
            int[] expectedOffsets = { -2, -3, -4, 2, 3, 4 };
            for (int previous = 0; previous < EnemySpawner.SpawnDirectionCount; previous++)
            {
                var candidates = new HashSet<int>();
                for (int choice = 0; choice < expectedOffsets.Length; choice++)
                {
                    int actual = EnemySpawner.DirectionIndexForTransitionChoice(previous, choice);
                    int expected = WrapDirectionIndex(previous + expectedOffsets[choice]);
                    if (actual != expected)
                    {
                        failures.Add(
                            $"Direction {previous + 1}, choice {choice} must select {expected + 1}, " +
                            $"but selected {actual + 1}.");
                    }

                    if (!EnemySpawner.IsDirectionTransitionAllowed(previous, actual))
                    {
                        failures.Add(
                            $"Direction {previous + 1} must allow transition to {actual + 1}.");
                    }

                    if (!candidates.Add(actual))
                    {
                        failures.Add(
                            $"Direction {previous + 1} produced duplicate transition candidate {actual + 1}.");
                    }
                }

                if (candidates.Count != 6)
                {
                    failures.Add(
                        $"Direction {previous + 1} must have six unique transition candidates, " +
                        $"but had {candidates.Count}.");
                }
            }

            int[] exampleExpected = { 0, 15, 14, 4, 5, 6 };
            for (int choice = 0; choice < exampleExpected.Length; choice++)
            {
                int actual = EnemySpawner.DirectionIndexForTransitionChoice(2, choice);
                if (actual != exampleExpected[choice])
                {
                    failures.Add(
                        $"Direction 3 example choice {choice} must select {exampleExpected[choice] + 1}, " +
                        $"but selected {actual + 1}.");
                }
            }
        }

        static int WrapDirectionIndex(int index)
        {
            int wrapped = index % EnemySpawner.SpawnDirectionCount;
            return wrapped < 0 ? wrapped + EnemySpawner.SpawnDirectionCount : wrapped;
        }
    }
}
