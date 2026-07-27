using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class CharacterLoadoutValidator
    {
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        const string SuccessMarkerPath = "Library/AreaSafeUnity/character-loadout-validator.success";

        [MenuItem("Area Survivors/Validate/Character Loadouts")]
        public static void Validate()
        {
            DeleteMarker();
            int errors = 0;
            ValidateLoadout(CharacterType.Knight, WeaponType.Slash, ref errors);
            ValidateLoadout(CharacterType.Archer, WeaponType.Arrow, ref errors);
            ValidateLoadout(CharacterType.Mage, WeaponType.Fireball, ref errors);
            ValidatePlayerPrefab(ref errors);

            if (errors > 0) throw new InvalidOperationException($"Character loadout validation failed with {errors} error(s).");
            WriteMarker();
            Debug.Log("Character loadout validation passed.");
        }

        static void ValidateLoadout(CharacterType character, WeaponType expectedWeapon, ref int errors)
        {
            if (CharacterLoadoutCatalog.StartingWeapon(character) != expectedWeapon)
            {
                Error(character + " starting weapon is incorrect.", ref errors);
            }

            foreach (WeaponType weapon in new[] { WeaponType.Slash, WeaponType.Arrow, WeaponType.Fireball })
            {
                int expectedLevel = weapon == expectedWeapon ? 1 : 0;
                if (CharacterLoadoutCatalog.InitialWeaponLevel(character, weapon, 0) != expectedLevel)
                {
                    Error(character + " initial weapon levels are incorrect.", ref errors);
                }
            }

            if (CharacterLoadoutCatalog.InitialWeaponLevel(character, expectedWeapon, 2) != 3)
            {
                Error(character + " does not receive the StartingWeaponLevel bonus.", ref errors);
            }
        }

        static void ValidatePlayerPrefab(ref int errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = prefab != null ? prefab.GetComponent<PlayerController>() : null;
            if (player == null)
            {
                Error("Player prefab or PlayerController is missing.", ref errors);
                return;
            }

            if (player.weapon == null) Error("Player weapon reference is missing.", ref errors);
            if (player.directionalAnimatorDriver == null) Error("DirectionalAnimatorDriver reference is missing.", ref errors);
            if (player.knightAnimatorController == null) Error("Knight AnimatorController is missing.", ref errors);
            if (player.archerAnimatorController == null) Error("Archer AnimatorController is missing.", ref errors);
            if (player.mageAnimatorController == null) Error("Mage AnimatorController is missing.", ref errors);
            if (player.knightSprite == null) Error("Knight portrait sprite is missing.", ref errors);
            if (player.archerSprite == null) Error("Archer portrait sprite is missing.", ref errors);
            if (player.mageSprite == null) Error("Mage portrait sprite is missing.", ref errors);
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError("[Character Loadout] " + message);
        }

        static void DeleteMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteMarker()
        {
            var directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("o"));
        }
    }
}
