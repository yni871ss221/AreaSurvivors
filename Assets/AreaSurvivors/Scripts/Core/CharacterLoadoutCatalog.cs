using UnityEngine;

namespace AreaSurvivors
{
    public static class CharacterLoadoutCatalog
    {
        public static WeaponType StartingWeapon(CharacterType type)
        {
            switch (type)
            {
                case CharacterType.Archer:
                    return WeaponType.Arrow;
                case CharacterType.Mage:
                    return WeaponType.Fireball;
                default:
                    return WeaponType.Slash;
            }
        }

        public static int InitialWeaponLevel(CharacterType character, WeaponType weapon, int startingWeaponLevelBonus)
        {
            if (weapon != StartingWeapon(character)) return 0;
            return Mathf.Clamp(1 + Mathf.Max(0, startingWeaponLevelBonus), 1, GameConfig.MaxWeaponLevel);
        }
    }
}
