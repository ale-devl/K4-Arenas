namespace K4Arenas
{
    using Microsoft.Extensions.Logging;

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    using K4Arenas.Models;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Modules.Timers;
    using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
    using System.Runtime.InteropServices;
    using System.Text.Json;

    [MinimumApiVersion(374)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public GameConfig? GameConfig { get; set; }
        public bool IsBetweenRounds = false;
        public MatchHistory MatchHistory { get; } = new();
        public bool UseRotation => !Config.Matchmaking.Equals("ladder", StringComparison.OrdinalIgnoreCase);

        public void OnConfigParsed(PluginConfig config)
        {
            CheckCommonProblems();

            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

            ValidateConfig(config);

            //** ? Load Round Types */

            if (config.RoundSettings.Count > 0)
            {
                RoundType.ClearRoundTypes();
                foreach (RoundTypeReader round in config.RoundSettings)
                {
                    RoundType.AddRoundType(round);
                }
            }
            else
                RoundType.ResetRoundTypes();

            string? defaultRound = config.DefaultWeaponSettings.DefaultRound;
            if (!string.IsNullOrEmpty(defaultRound) && !RoundType.RoundTypes.Any(rt => rt.Name == defaultRound))
                Logger.LogWarning("Config: default-round '{Round}' matches no round in round-settings", defaultRound);

            Logger.LogInformation("Config loaded from configs/plugins/{Folder}/ with {Count} round type(s)", Path.GetFileName(ModuleDirectory), RoundType.RoundTypes.Count);

            this.Config = config;
        }

        // Values that parse fine but would be silently ignored get a warning naming the field
        private void ValidateConfig(PluginConfig config)
        {
            void WarnUnknownKeys(string section, Dictionary<string, JsonElement>? keys)
            {
                foreach (string key in keys?.Keys ?? Enumerable.Empty<string>())
                    Logger.LogWarning("Config: unknown key '{Key}' in {Section} is ignored, check the spelling", key, section);
            }

            void WarnUnknownWeapon(string field, string? weapon)
            {
                if (weapon != null && FindEnumValueByEnumMemberValue(weapon) == null)
                    Logger.LogWarning("Config: unknown weapon '{Weapon}' in {Field} is ignored, expected a name like 'weapon_ak47'", weapon, field);
            }

            WarnUnknownKeys("the top level", config.UnknownKeys);

            if (!new[] { "rotation", "ladder" }.Contains(config.Matchmaking, StringComparer.OrdinalIgnoreCase))
                Logger.LogWarning("Config: matchmaking '{Mode}' is not 'rotation' or 'ladder', using rotation", config.Matchmaking);
            WarnUnknownKeys("database-settings", config.DatabaseSettings.UnknownKeys);
            WarnUnknownKeys("command-settings", config.CommandSettings.UnknownKeys);
            WarnUnknownKeys("compatibility-settings", config.CompatibilitySettings.UnknownKeys);
            WarnUnknownKeys("elo-settings", config.EloSettings.UnknownKeys);
            WarnUnknownKeys("default-weapon-settings", config.DefaultWeaponSettings.UnknownKeys);
            WarnUnknownKeys("allowed-weapon-prefs", config.AllowedWeaponPreferences.UnknownKeys);

            DefaultWeaponSettings dws = config.DefaultWeaponSettings;
            WarnUnknownWeapon("default-rifle", dws.DefaultRifle);
            WarnUnknownWeapon("default-sniper", dws.DefaultSniper);
            WarnUnknownWeapon("default-smg", dws.DefaultSMG);
            WarnUnknownWeapon("default-lmg", dws.DefaultLMG);
            WarnUnknownWeapon("default-shotgun", dws.DefaultShotgun);
            WarnUnknownWeapon("default-pistol", dws.DefaultPistol);

            for (int i = 0; i < config.RoundSettings.Count; i++)
            {
                RoundTypeReader round = config.RoundSettings[i];
                string where = $"round-settings[{i}]";

                WarnUnknownKeys(where, round.UnknownKeys);
                WarnUnknownWeapon($"{where}.PrimaryWeapon", round.PrimaryWeapon);
                WarnUnknownWeapon($"{where}.SecondaryWeapon", round.SecondaryWeapon);

                if (string.IsNullOrWhiteSpace(round.TranslationName))
                    Logger.LogWarning("Config: {Where} has no TranslationName and is skipped", where);
            }

            config.RoundSettings.RemoveAll(r => string.IsNullOrWhiteSpace(r.TranslationName));

            foreach (string duplicate in config.RoundSettings.GroupBy(r => r.TranslationName).Where(g => g.Count() > 1).Select(g => g.Key))
                Logger.LogWarning("Config: round-settings has more than one '{Round}', only the first is used", duplicate);

            config.RoundSettings = [.. config.RoundSettings.DistinctBy(r => r.TranslationName)];
        }

        public Queue<ArenaPlayer> WaitingArenaPlayers { get; set; } = new Queue<ArenaPlayer>();
        public List<ChallengeModel> Challenges { get; set; } = [];
        public Arenas? Arenas { get; set; } = null;

        public CCSGameRules? gameRules = null;
        public Timer? WarmupTimer { get; set; } = null;
        public bool FlashFixFound { get; set; } = false;

        public override void Load(bool hotReload)
        {
            if (Config.UsePredefinedConfig)
                GameConfig = new GameConfig(this);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ModulePath);
            FlashFixFound = Directory.Exists(Path.Combine(ModulePath, "..", "FlashingXMLHintFix"));

            try
            {
                Task.Run(() => PlayerStore.InitializeAsync(DatabasePath)).Wait();
                Task.Run(PurgeDatabaseAsync);
            }
            catch (Exception ex)
            {
                // The plugin still runs on config defaults, preferences just aren't saved
                Logger.LogError("Could not open the preferences database at {Path}: {Error}", DatabasePath, ex.InnerException?.Message ?? ex.Message);
            }

            //** ? Core */

            Initialize_API();
            Initialize_Events();
            Initialize_Commands();
            Initialize_Listeners();

            //** ? Setup */

            if (hotReload)
            {
                var players = Utilities.GetPlayers().Where(p => p.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsHLTV).ToList();
                lastRealPlayers = players.Count(p => !p.IsBot);

                Arenas ??= new Arenas(this);

                gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

                players.ForEach(p =>
                {
                    SetupPlayer(p);
                });

                GameConfig?.Apply();

                Server.ExecuteCommand("mp_restartgame 1");
            }

            //** ? Force Clantags */

            if (Config.CompatibilitySettings.ForceArenaClantags)
            {
                AddTimer(1, () =>
                {
                    if (Arenas is null) return;

                    var validPlayers = Utilities.GetPlayers()
                        .Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.Connected);

                    foreach (CCSPlayerController player in validPlayers)
                    {
                        string? requiredTag = GetRequiredTag(player);
                        if (requiredTag != null && player.Clan != requiredTag)
                        {
                            var arenaPlayer = Arenas.FindPlayer(player);
                            if (arenaPlayer is null) continue;

                            arenaPlayer.ArenaTag = requiredTag;

                            if (!Config.CompatibilitySettings.DisableClantags)
                            {
                                player.Clan = arenaPlayer.ArenaTag;
                                Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
                            }
                        }
                    }
                }, TimerFlags.REPEAT);
            }
        }
    }
}