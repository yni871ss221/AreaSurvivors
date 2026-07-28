using System;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class Health : MonoBehaviour
    {
        public int maxHp = 10;
        public int currentHp = 10;
        public float defense;
        public bool invincible;
        public event Action<Health, int> Damaged;
        public event Action<Health, int> Healed;
        public event Action<Health> Died;
        public Vector3 LastDamagePoint { get; private set; }
        public int LastDamageDealt { get; private set; }

        public float Normalized => maxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / maxHp);
        public bool IsDead => currentHp <= 0;

        public void SetMax(int value)
        {
            SetMax(value, true);
        }

        public void SetMax(int value, bool fullHeal)
        {
            maxHp = Mathf.Max(1, value);
            currentHp = fullHeal ? maxHp : Mathf.Clamp(currentHp, 0, maxHp);
        }

        public int Damage(int value)
        {
            return Damage(value, transform.position);
        }

        public int DamageAmount(int value)
        {
            return Mathf.Max(0, Mathf.CeilToInt(value - Mathf.Max(0f, defense)));
        }

        public int Damage(int value, Vector3 worldPoint)
        {
            LastDamageDealt = 0;
            if (IsDead || invincible) return 0;
            LastDamagePoint = worldPoint;
            int amount = DamageAmount(value);
            int before = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
            int dealt = before - currentHp;
            LastDamageDealt = dealt;
            Damaged?.Invoke(this, amount);
            if (currentHp <= 0) Died?.Invoke(this);
            return dealt;
        }

        public void FullHeal()
        {
            currentHp = maxHp;
        }

        public int Heal(int value)
        {
            if (IsDead) return 0;
            int amount = Mathf.Max(0, value);
            if (amount <= 0 || currentHp >= maxHp) return 0;
            int before = currentHp;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            int healed = currentHp - before;
            if (healed > 0) Healed?.Invoke(this, healed);
            return healed;
        }
    }
}
