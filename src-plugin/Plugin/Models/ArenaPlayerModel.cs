using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using K4ArenaSharedApi;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace K4Arenas.Models;

public class ArenaPlayer
{
	//** ? Main */
	private readonly Plugin Plugin;
	public readonly IStringLocalizer Localizer;
	public readonly PluginConfig Config;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public readonly ulong SteamID;
	public SpawnPoint? SpawnPoint;
	public bool PlayerIsSafe;
	public ushort MVPs = 0;
	public bool Loaded = false;
	public string ArenaTag = string.Empty;
	public string CenterMessage = string.Empty;

	//** ? Settings */
	public bool AFK = false;
	public double Rating;

	// Only what the player explicitly picked (loaded from the database); everything else follows the config.
	// A null weapon means the player picked "Random".
	public Dictionary<WeaponType, CsItem?> WeaponChoices = [];
	public Dictionary<string, bool> RoundChoices = [];

	public List<RoundType> RoundPreferences
		=> [.. RoundType.RoundTypes.Where(r => RoundChoices.TryGetValue(r.Name, out bool enabled) ? enabled : r.EnabledByDefault)];

	// The player's weapon for a type: their choice, else the config default; null = random
	public CsItem? GetWeaponPreference(WeaponType weaponType)
	{
		if (WeaponChoices.TryGetValue(weaponType, out CsItem? choice))
			return choice;

		DefaultWeaponSettings dws = Plugin.Config.DefaultWeaponSettings;
		return Plugin.FindEnumValueByEnumMemberValue(weaponType switch
		{
			WeaponType.Rifle => dws.DefaultRifle,
			WeaponType.Sniper => dws.DefaultSniper,
			WeaponType.SMG => dws.DefaultSMG,
			WeaponType.LMG => dws.DefaultLMG,
			WeaponType.Shotgun => dws.DefaultShotgun,
			WeaponType.Pistol => dws.DefaultPistol,
			_ => null
		});
	}

	public ArenaPlayer(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;
		Localizer = Plugin.Localizer;
		Config = Plugin.Config;

		Controller = playerController;
		SteamID = playerController.SteamID;
		PlayerIsSafe = playerController.IsBot;
		Rating = Plugin.Config.EloSettings.StartRating;
	}

	public bool IsValid
		=> Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true;

	public bool IsAlive
		=> Controller.PlayerPawn.Value?.Health > 0;

	public void SetupWeapons(RoundType roundType)
	{
		if (!this.IsValid || Controller.PlayerPawn.Value == null)
		{
			Plugin.Logger.LogWarning($"Cannot setup weapons for invalid player or null pawn: {Controller.PlayerName}");
			return;
		}

		Controller.RemoveWeapons();

		if (Config.CompatibilitySettings.GiveKnifeByDefault)
			Controller.GiveNamedItem(CsItem.Knife);

		if (roundType.PrimaryPreference == WeaponType.Unknown) // Warmup or Random round types
		{
			Controller.GiveNamedItem(WeaponModel.GetRandomWeapon(WeaponType.Unknown));
			Controller.GiveNamedItem(WeaponModel.GetRandomWeapon(WeaponType.Pistol));
		}
		else
		{
			if (roundType.PrimaryWeapon != null)
			{
				Controller.GiveNamedItem((CsItem)roundType.PrimaryWeapon);
			}
			else if (roundType.UsePreferredPrimary && roundType.PrimaryPreference != null)
			{
				WeaponType primaryPreferenceType = (WeaponType)roundType.PrimaryPreference;
				CsItem? primaryPreference = GetWeaponPreference(primaryPreferenceType) ?? WeaponModel.GetRandomWeapon(primaryPreferenceType);
				Controller.GiveNamedItem((CsItem)primaryPreference);
			}

			if (roundType.SecondaryWeapon != null)
			{
				Controller.GiveNamedItem((CsItem)roundType.SecondaryWeapon);
			}
			else if (roundType.UsePreferredSecondary)
			{
				CsItem? secondaryPreference = GetWeaponPreference(WeaponType.Pistol) ?? WeaponModel.GetRandomWeapon(WeaponType.Pistol);
				Controller.GiveNamedItem((CsItem)secondaryPreference);
			}
		}

		Server.NextWorldUpdate(() =>
		{
			if (!this.IsValid)
				return;

			if (Controller.PlayerPawn.Value != null)
			{
				CCSPlayerPawn playerPawn = Controller.PlayerPawn.Value;

				playerPawn.ArmorValue = roundType.Armor ? 100 : 0;
				Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");

				if (playerPawn.ItemServices != null)
				{
					CCSPlayer_ItemServices itemService = new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle)
					{
						HasHelmet = roundType.Helmet
					};

					Utilities.SetStateChanged(playerPawn, "CBasePlayerPawn", "m_pItemServices");
				}
				else
				{
					Plugin.Logger.LogWarning($"ItemServices is null for player: {Controller.PlayerName}");
				}
			}
			else
			{
				Plugin.Logger.LogWarning($"PlayerPawn is null for player: {Controller.PlayerName}");
			}
		});
	}

	public void ShowRoundPreferenceMenu()
	{
		ShowChatRoundPreferenceMenu();
	}

	private void ShowChatRoundPreferenceMenu()
	{
		ChatMenu roundPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.roundpref.title"));
		foreach (RoundType roundType in RoundType.RoundTypes)
		{
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			roundPreferenceMenu.AddMenuOption(isRoundTypeEnabled ? Localizer.ForPlayer(Controller, "k4.menu.roundpref.item_enabled", Localizer.ForPlayer(Controller, roundType.Name)) : Localizer.ForPlayer(Controller, "k4.menu.roundpref.item_disabled", Localizer.ForPlayer(Controller, roundType.Name)),
				(player, option) =>
				{
					ToggleRoundPreference(roundType);
					Plugin.SavePlayer(this);
				}
			);
		}

		MenuManager.OpenChatMenu(Controller, roundPreferenceMenu);
	}

	private void ToggleRoundPreference(RoundType roundType)
	{
		bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
		if (isRoundTypeEnabled)
		{
			if (RoundPreferences.Count == 1)
			{
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_atleastone")}");
			}
			else
			{
				RoundChoices[roundType.Name] = false;
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_removed", Localizer.ForPlayer(Controller, roundType.Name))}");
			}
		}
		else
		{
			RoundChoices[roundType.Name] = true;
			Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_added", Localizer.ForPlayer(Controller, roundType.Name))}");
		}
	}

	public void ShowWeaponPreferenceMenu()
	{
		ShowChatWeaponPreferenceMenu();
	}

	private void ShowChatWeaponPreferenceMenu()
	{
		ChatMenu weaponPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"));
		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			if (weaponType == WeaponType.Unknown || !IsAllowedWeaponType(weaponType))
				continue;
			weaponPreferenceMenu.AddMenuOption(Localizer.ForPlayer(Controller, $"k4.rounds.{weaponType.ToString().ToLower()}"),
				(player, option) =>
				{
					ShowWeaponSubPreferenceMenu(weaponType);
				}
			);
		}
		MenuManager.OpenChatMenu(Controller, weaponPreferenceMenu);
	}

	private bool IsAllowedWeaponType(WeaponType weaponType)
	{
		return weaponType switch
		{
			WeaponType.Rifle => Config.AllowedWeaponPreferences.Rifle,
			WeaponType.Sniper => Config.AllowedWeaponPreferences.Sniper,
			WeaponType.SMG => Config.AllowedWeaponPreferences.SMG,
			WeaponType.LMG => Config.AllowedWeaponPreferences.LMG,
			WeaponType.Shotgun => Config.AllowedWeaponPreferences.Shotgun,
			WeaponType.Pistol => Config.AllowedWeaponPreferences.Pistol,
			_ => false
		};
	}

	public void ShowWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		ShowChatWeaponSubPreferenceMenu(weaponType);
	}

	private void ShowChatWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		ChatMenu primaryPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"));
		AddWeaponOptions(primaryPreferenceMenu, weaponType);
		MenuManager.OpenChatMenu(Controller, primaryPreferenceMenu);
	}

	private void AddWeaponOptions(ChatMenu menu, WeaponType weaponType)
	{
		menu.AddMenuOption(GetWeaponPreference(weaponType) is null ? Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_enabled", Localizer.ForPlayer(Controller, "k4.general.random")) : Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_disabled", Localizer.ForPlayer(Controller, "k4.general.random")),
			(player, option) =>
			{
				SetWeaponPreference(weaponType, null);
				Plugin.SavePlayer(this);
			}
		);

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		foreach (CsItem item in possibleItems)
		{
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;

			bool isItemEnabled = GetWeaponPreference(weaponType) == item;
			menu.AddMenuOption(isItemEnabled ? Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_enabled", Localizer.ForPlayer(Controller, item.ToString())) : Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_disabled", Localizer.ForPlayer(Controller, item.ToString())),
				(player, option) =>
				{
					SetWeaponPreference(weaponType, item);
					Plugin.SavePlayer(this);
				}
			);
		}
	}

	private void SetWeaponPreference(WeaponType weaponType, CsItem? item)
	{
		WeaponChoices[weaponType] = item;
		Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.weapon_preferences_added", Localizer.ForPlayer(Controller, item?.ToString() ?? "k4.general.random"))}");
	}
}