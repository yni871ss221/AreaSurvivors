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

        public int Damage(int value)
        {
            if (IsDead) return 0;
            int amount = Mathf.Max(1, value);
            int before = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
            int dealt = before - currentHp;
            Damaged?.Invoke(this, dealt);
            if (currentHp <= 0) Died?.Invoke(this);
            return dealt;
        }

        public void FullHeal()
        {
            currentHp = maxHp;
        }
    }
}
