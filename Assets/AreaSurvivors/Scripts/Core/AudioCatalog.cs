namespace AreaSurvivors
{
    public enum BgmTrack
    {
        TitleOptions,
        LobbyUpgrades,
        OpeningStory,
        GameNormal,
        GameBoss,
        EndingCredits
    }

    public enum SfxTrack
    {
        SlashSwing,
        ArrowShot,
        FireballCast,
        ExplosionHit,
        ButtonConfirm,
        LevelUp,
        EnemyHit,
        ExperiencePickup,
        GameOver,
        GameClear,
        ResultPanelReveal,
        ShieldHit,
        BoomerangSwordThrow,
        AuraSwordCast,
        ArrowRainTick,
        GunShot,
        FrostCast,
        ThunderBallCast,
        TowerCollapse,
        BossDefeatRumble,
        RelicChestPickup,
        RelicChestOpen,
        BossShockwaveHit,
        GoblinLordDarkMagic,
        LichSummonMagic,
        StageUnlockPopup,
        MissionCompleteFanfare,
        MissionCompleteCheer,
        TokenGain,
        StudioLogoBounce
    }

    public static class AudioCatalog
    {
        public static string BgmPath(BgmTrack track)
        {
            switch (track)
            {
                case BgmTrack.TitleOptions:
                    return "Audio/BGM/title_options";
                case BgmTrack.LobbyUpgrades:
                    return "Audio/BGM/lobby_upgrades";
                case BgmTrack.OpeningStory:
                    return "Audio/BGM/opening_story";
                case BgmTrack.GameNormal:
                    return "Audio/BGM/game_normal";
                case BgmTrack.GameBoss:
                    return "Audio/BGM/game_boss";
                case BgmTrack.EndingCredits:
                    return "Audio/BGM/yuusou";
                default:
                    return string.Empty;
            }
        }

        public static string SfxPath(SfxTrack track)
        {
            switch (track)
            {
                case SfxTrack.SlashSwing:
                    return "Audio/SFX/slash_swing";
                case SfxTrack.ArrowShot:
                    return "Audio/SFX/arrow_shot";
                case SfxTrack.FireballCast:
                    return "Audio/SFX/fireball_cast";
                case SfxTrack.ExplosionHit:
                    return "Audio/SFX/explosion_hit";
                case SfxTrack.ButtonConfirm:
                    return "Audio/SFX/button_confirm";
                case SfxTrack.LevelUp:
                    return "Audio/SFX/level_up";
                case SfxTrack.EnemyHit:
                    return "Audio/SFX/enemy_hit";
                case SfxTrack.ExperiencePickup:
                    return "Audio/SFX/experience_pickup";
                case SfxTrack.GameOver:
                    return "Audio/SFX/game_over";
                case SfxTrack.GameClear:
                    return "Audio/SFX/game_clear";
                case SfxTrack.ResultPanelReveal:
                    return "Audio/SFX/result_panel_reveal";
                case SfxTrack.ShieldHit:
                    return "Audio/SFX/shield_hit";
                case SfxTrack.BoomerangSwordThrow:
                    return "Audio/SFX/boomerang_sword";
                case SfxTrack.AuraSwordCast:
                    return "Audio/SFX/aura_sword";
                case SfxTrack.ArrowRainTick:
                    return "Audio/SFX/arrow_rain";
                case SfxTrack.GunShot:
                    return "Audio/SFX/gun_shot";
                case SfxTrack.FrostCast:
                    return "Audio/SFX/frost_cast";
                case SfxTrack.ThunderBallCast:
                    return "Audio/SFX/thunder_ball";
                case SfxTrack.TowerCollapse:
                    return "Audio/SFX/tower_collapse";
                case SfxTrack.BossDefeatRumble:
                    return "Audio/SFX/boss_defeat_rumble";
                case SfxTrack.RelicChestPickup:
                    return "Audio/SFX/relic_chest_pickup";
                case SfxTrack.RelicChestOpen:
                    return "Audio/SFX/relic_chest_open";
                case SfxTrack.BossShockwaveHit:
                    return "Audio/SFX/boss_shockwave_hit";
                case SfxTrack.GoblinLordDarkMagic:
                    return "Audio/SFX/goblin_lord_dark_magic";
                case SfxTrack.LichSummonMagic:
                    return "Audio/SFX/lich_summon_magic";
                case SfxTrack.StageUnlockPopup:
                    return "Audio/SFX/stage_unlock_popup";
                case SfxTrack.MissionCompleteFanfare:
                    return "Audio/SFX/mission_complete_fanfare";
                case SfxTrack.MissionCompleteCheer:
                    return "Audio/SFX/mission_complete_cheer";
                case SfxTrack.TokenGain:
                    return "Audio/SFX/token_gain";
                case SfxTrack.StudioLogoBounce:
                    return "Audio/SFX/studio_logo_bounce";
                default:
                    return string.Empty;
            }
        }
    }
}
