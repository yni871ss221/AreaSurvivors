using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public enum RunDamageBuildingSource
    {
        None,
        CenterTower,
        Ballista,
        WatchTower
    }

    public enum RunDamageSourceKind
    {
        None,
        Building,
        Weapon
    }

    [Serializable]
    public struct RunDamageSource
    {
        public RunDamageSourceKind kind;
        public RunDamageBuildingSource building;
        public WeaponType weapon;

        public bool IsAssigned => kind != RunDamageSourceKind.None;

        public static RunDamageSource ForBuilding(RunDamageBuildingSource source)
        {
            return new RunDamageSource
            {
                kind = source == RunDamageBuildingSource.None ? RunDamageSourceKind.None : RunDamageSourceKind.Building,
                building = source
            };
        }

        public static RunDamageSource ForWeapon(WeaponType type)
        {
            return new RunDamageSource
            {
                kind = RunDamageSourceKind.Weapon,
                weapon = type
            };
        }
    }

    [Serializable]
    public sealed class RunDamageReportEntry
    {
        public string label;
        public int totalDamage;
        public float activeSeconds;
        public bool visible;
        public RunDamageSourceKind sourceKind;
        public RunDamageBuildingSource building;
        public WeaponType weapon;

        public float Dps => activeSeconds > 0.001f ? totalDamage / activeSeconds : 0f;

        public RunDamageReportEntry Clone()
        {
            return new RunDamageReportEntry
            {
                label = label,
                totalDamage = totalDamage,
                activeSeconds = activeSeconds,
                visible = visible,
                sourceKind = sourceKind,
                building = building,
                weapon = weapon
            };
        }
    }

    public sealed class RunDamageTracker
    {
        public const int MaxEntries = 10;
        const int CenterTowerIndex = 0;
        const int BallistaIndex = 1;
        const int WatchTowerIndex = 2;
        const int WeaponStartIndex = 3;
        const int WeaponSlotCount = 3;

        readonly RunDamageReportEntry[] entries = new RunDamageReportEntry[MaxEntries];
        readonly int[] lastActiveFrame = new int[MaxEntries];
        readonly Dictionary<WeaponType, int> weaponEntryIndexByType = new Dictionary<WeaponType, int>();

        public RunDamageTracker()
        {
            Reset();
        }

        public void Reset()
        {
            weaponEntryIndexByType.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new RunDamageReportEntry
                {
                    label = DefaultLabel(i),
                    visible = i < WeaponStartIndex + WeaponSlotCount,
                    sourceKind = DefaultKind(i),
                    building = DefaultBuilding(i)
                };
                lastActiveFrame[i] = -1;
            }
        }

        public void RegisterWeaponSlot(WeaponType type, int slotIndex, string displayName)
        {
            if (slotIndex < 0 || slotIndex >= WeaponSlotCount) return;
            int entryIndex = WeaponStartIndex + slotIndex;
            weaponEntryIndexByType[type] = entryIndex;
            entries[entryIndex].label = string.IsNullOrWhiteSpace(displayName) ? DefaultLabel(entryIndex) : displayName;
            entries[entryIndex].visible = true;
            entries[entryIndex].sourceKind = RunDamageSourceKind.Weapon;
            entries[entryIndex].weapon = type;
        }

        public void MarkActive(RunDamageSource source)
        {
            int index = EntryIndex(source);
            if (index < 0) return;
            MarkActive(index);
        }

        public void RegisterDamage(RunDamageSource source, int amount)
        {
            int index = EntryIndex(source);
            if (index < 0) return;
            entries[index].totalDamage += Mathf.Max(0, amount);
            entries[index].visible = true;
        }

        public List<RunDamageReportEntry> BuildReport()
        {
            var report = new List<RunDamageReportEntry>(MaxEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                report.Add(entries[i].Clone());
            }

            return report;
        }

        int EntryIndex(RunDamageSource source)
        {
            switch (source.kind)
            {
                case RunDamageSourceKind.Building:
                    return BuildingIndex(source.building);
                case RunDamageSourceKind.Weapon:
                    return weaponEntryIndexByType.TryGetValue(source.weapon, out var index) ? index : -1;
                default:
                    return -1;
            }
        }

        static int BuildingIndex(RunDamageBuildingSource source)
        {
            switch (source)
            {
                case RunDamageBuildingSource.CenterTower: return CenterTowerIndex;
                case RunDamageBuildingSource.Ballista: return BallistaIndex;
                case RunDamageBuildingSource.WatchTower: return WatchTowerIndex;
                default: return -1;
            }
        }

        void MarkActive(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= entries.Length) return;
            if (lastActiveFrame[entryIndex] == Time.frameCount) return;
            entries[entryIndex].activeSeconds += Mathf.Max(0f, Time.deltaTime);
            entries[entryIndex].visible = true;
            lastActiveFrame[entryIndex] = Time.frameCount;
        }

        static string DefaultLabel(int index)
        {
            switch (index)
            {
                case CenterTowerIndex: return "中心塔";
                case BallistaIndex: return "バリスタ";
                case WatchTowerIndex: return "監視塔";
                case WeaponStartIndex: return "武器1枠目";
                case WeaponStartIndex + 1: return "武器2枠目";
                case WeaponStartIndex + 2: return "武器3枠目";
                default: return "予備";
            }
        }

        static RunDamageSourceKind DefaultKind(int index)
        {
            return index < WeaponStartIndex
                ? RunDamageSourceKind.Building
                : RunDamageSourceKind.None;
        }

        static RunDamageBuildingSource DefaultBuilding(int index)
        {
            switch (index)
            {
                case CenterTowerIndex: return RunDamageBuildingSource.CenterTower;
                case BallistaIndex: return RunDamageBuildingSource.Ballista;
                case WatchTowerIndex: return RunDamageBuildingSource.WatchTower;
                default: return RunDamageBuildingSource.None;
            }
        }
    }
}
