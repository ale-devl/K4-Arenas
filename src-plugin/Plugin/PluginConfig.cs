namespace Alerena
{
	using CounterStrikeSharp.API.Core;
	using Alerena.Models;
	using AlerenaApi;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	// Collects keys the config classes don't know (typos, wrong casing) so they can be reported instead of silently ignored
	public abstract class ConfigSection
	{
		[JsonExtensionData]
		public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
	}

	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonExtensionData]
		public Dictionary<string, JsonElement>? UnknownKeys { get; set; }

		[JsonPropertyName("use-predefined-config")]
		public bool UsePredefinedConfig { get; set; } = true;

		// "rotation": everyone faces the opponents they've met least this session. "ladder": winners move up, losers down.
		[JsonPropertyName("matchmaking")]
		public string Matchmaking { get; set; } = "rotation";

		[JsonPropertyName("database-settings")]
		public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

		[JsonPropertyName("command-settings")]
		public CommandSettings CommandSettings { get; set; } = new CommandSettings();

		[JsonPropertyName("round-settings")]
		public List<RoundTypeReader> RoundSettings { get; set; } =
		[
			new() {
				TranslationName = "alerena.rounds.rifle",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Rifle,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.sniper",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Sniper,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.shotgun",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Shotgun,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.pistol",
				TeamSize = 1,
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.scout",
				TeamSize = 1,
				PrimaryWeapon = "weapon_ssg08",
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.awp",
				TeamSize = 1,
				PrimaryWeapon = "weapon_awp",
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.deagle",
				TeamSize = 1,
				SecondaryWeapon = "weapon_deagle",
				Armor = false,
				Helmet = false
			},
			new() {
				TranslationName = "alerena.rounds.smg",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.SMG,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.lmg",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.LMG,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "alerena.rounds.2vs2",
				TeamSize = 2,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Unknown,
				Armor = true,
				Helmet = true,
				EnabledByDefault = false
			},
			new() {
				TranslationName = "alerena.rounds.3vs3",
				TeamSize = 3,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Unknown,
				Armor = true,
				Helmet = true,
				EnabledByDefault = false
			},
			new() {
				TranslationName = "alerena.rounds.knife",
				TeamSize = 1,
				Armor = false,
				Helmet = false
			}
		];

		[JsonPropertyName("compatibility-settings")]
		public CompatibilitySettings CompatibilitySettings { get; set; } = new CompatibilitySettings();

		[JsonPropertyName("elo-settings")]
		public EloSettings EloSettings { get; set; } = new EloSettings();

		[JsonPropertyName("default-weapon-settings")]
		public DefaultWeaponSettings DefaultWeaponSettings { get; set; } = new DefaultWeaponSettings();

		[JsonPropertyName("allowed-weapon-prefs")]
		public AllowedWeaponPreferences AllowedWeaponPreferences { get; set; } = new AllowedWeaponPreferences();

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 10;
	}

	public sealed class CompatibilitySettings : ConfigSection
	{
		[JsonPropertyName("force-arena-clantags")]
		public bool ForceArenaClantags { get; set; } = false;

		[JsonPropertyName("block-flash-of-not-opponent")]
		public bool BlockFlashOfNotOpponent { get; set; } = false;

		[JsonPropertyName("block-damage-of-not-opponent")]
		public bool BlockDamageOfNotOpponent { get; set; } = false;

		[JsonPropertyName("give-knife-by-default")]
		public bool GiveKnifeByDefault { get; set; } = true;

		[JsonPropertyName("disable-clantags")]
		public bool DisableClantags { get; set; } = false;

		[JsonPropertyName("prevent-draw-rounds")]
		public bool PreventDrawRounds { get; set; } = true;
	}

	public sealed class EloSettings : ConfigSection
	{
		// How far one duel can move a rating: higher means faster swings
		[JsonPropertyName("k-factor")]
		public double KFactor { get; set; } = 32;

		[JsonPropertyName("start-rating")]
		public double StartRating { get; set; } = 1000;
	}

	public sealed class AllowedWeaponPreferences : ConfigSection
	{
		[JsonPropertyName("rifle")]
		public bool Rifle { get; set; } = true;

		[JsonPropertyName("sniper")]
		public bool Sniper { get; set; } = true;

		[JsonPropertyName("smg")]
		public bool SMG { get; set; } = true;

		[JsonPropertyName("lmg")]
		public bool LMG { get; set; } = true;

		[JsonPropertyName("shotgun")]
		public bool Shotgun { get; set; } = true;

		[JsonPropertyName("pistol")]
		public bool Pistol { get; set; } = true;
	}

	public sealed class CommandSettings : ConfigSection
	{
		[JsonPropertyName("gun-pref-commands")]
		public List<string> GunsCommands { get; set; } =
		[
			"guns",
			"gunpref",
			"weaponpref",
		];

		[JsonPropertyName("round-pref-commands")]
		public List<string> RoundsCommands { get; set; } =
		[
			"rounds",
			"roundpref",
		];

		[JsonPropertyName("queue-commands")]
		public List<string> QueueCommands { get; set; } =
		[
			"queue"
		];

		[JsonPropertyName("afk-commands")]
		public List<string> AFKCommands { get; set; } =
		[
			"afk"
		];

		[JsonPropertyName("challenge-commands")]
		public List<string> ChallengeCommands { get; set; } =
		[
			"challenge",
			"duel"
		];

		[JsonPropertyName("challenge-accept-commands")]
		public List<string> ChallengeAcceptCommands { get; set; } =
		[
			"caccept",
			"capprove"
		];

		[JsonPropertyName("challenge-decline-commands")]
		public List<string> ChallengeDeclineCommands { get; set; } =
		[
			"cdecline",
			"cdeny"
		];

		// Opens the in-game settings menu, needs the @css/config permission
		[JsonPropertyName("admin-commands")]
		public List<string> AdminCommands { get; set; } =
		[
			"arenaconfig"
		];

		[JsonPropertyName("top-commands")]
		public List<string> TopCommands { get; set; } =
		[
			"top",
			"elo"
		];

		[JsonPropertyName("center-announce-mode")]
		public bool CenterAnnounceMode { get; set; } = true;
	}

	public sealed class DefaultWeaponSettings : ConfigSection
	{
		[JsonPropertyName("default-rifle")]
		public string? DefaultRifle { get; set; } = null;

		[JsonPropertyName("default-sniper")]
		public string? DefaultSniper { get; set; } = null;

		[JsonPropertyName("default-smg")]
		public string? DefaultSMG { get; set; } = null;

		[JsonPropertyName("default-lmg")]
		public string? DefaultLMG { get; set; } = null;

		[JsonPropertyName("default-shotgun")]
		public string? DefaultShotgun { get; set; } = null;

		[JsonPropertyName("default-pistol")]
		public string? DefaultPistol { get; set; } = null;

		[JsonPropertyName("default-round")]
		public string? DefaultRound { get; set; } = "alerena.rounds.rifle";
	}

	public sealed class DatabaseSettings : ConfigSection
	{
		// Players not seen for this many days are deleted from the preferences database; 0 keeps them forever
		[JsonPropertyName("table-purge-days")]
		public int TablePurgeDays { get; set; } = 0;
	}
}