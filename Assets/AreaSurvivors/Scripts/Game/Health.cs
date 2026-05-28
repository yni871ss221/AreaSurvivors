using System;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class Health : MonoBehaviour
    {
        public int maxHp = 10;
        public int currentHp = 10;
        public event Action<Health, int> Damaged;
        public event Action<Health> Died;

        public float Normalized => maxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / maxHp);
        public bool IsDead => currentHp <= 0;

        public void SetMax(int value)
        {
            maxHp = Mathf.Max(1, value);
            currentHp = maxHp;
        }

        public void Damage(int value)
        {
            if (IsDead) return;
            int amount = Mathf.Max(1, value);
            currentHp = Mathf.Max(0, currentHp - amount);
            Damaged?.Invoke(this, amount);
            if (currentHp <= 0) Died?.Invoke(this);
        }

        public void FullHeal()
        {
            currentHp = maxHp;
        }
    }
}
