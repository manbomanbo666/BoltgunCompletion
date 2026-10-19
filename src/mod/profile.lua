-- Only this module contains Boltgun-specific names and validation status.
-- Getter signatures are checked against live reflection before any invocation.
return {
    build = '14893097',
    executable_sha256 = 'bd32aecb2ef6feb3fd2edfb0095b7e534176c92c01d5fa5ccc43a0ddc639b01a',
    statistics_verified = false,
    diagnostics_enabled = false,
    -- C1M4 real settlement: EndLevel 234/234, widget 100%, secrets 6/6,
    -- independently compared with the player's visible game result.
    settlement_fields_verified = true,
    meta_class = 'BGMetaGame',
    player_class = 'BGPlayerCharacter',
    mode_class = 'BGGameplayGameMode',
    secret_class = 'BGActivatable_Volume_SecretUnlock',
    enemy_class = 'BGEnemyBase',
    level_data_class = 'BGLevelData',
    inspect_classes = {
        'BGMetaGame','BGGameplayGameMode','BGGameInstance','BGLevelData',
        'BGPlayerCharacter','BGEnemyBase','BGNonPlayableCharacterBase','BGActivatable_Volume_SecretUnlock',
        'BGEncounterManagerBase','BGSaveGameGameModeData','BGCampaignSlotTracker','BGStatsManager'
    },
    results_class = 'W_UI_17_Level_Complete_C',
    results_kills_text = 'TextBlock_Kills_Stat',
    results_secrets_text = 'TextBlock_Secrets_Stat',
    results_asset = '/Game/Blueprints/UI/17_Level_Complete/W_UI_17_Level_Complete.W_UI_17_Level_Complete_C',
    hooks = {
        end_level='/Script/Boltgun.BGGameInstance:EndLevel',
        restart='/Script/Boltgun.BGGameInstance:RestartCurrentLevel',
        restored='/Script/Boltgun.BGPlayerCharacter:ActorFullyLoaded',
        secret='/Script/Boltgun.BGMetaGame:SecretFound',
        enemy_killed='/Script/Boltgun.BGGameInstance:EnemyKilled'
    }
}
