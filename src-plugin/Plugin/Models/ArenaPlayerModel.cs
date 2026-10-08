using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using CSSUniversalMenuAPI;
using AlerenaApi;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Alerena.Models;

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
		IMenu? menu = Plugin.CreateMenu(Controller, "alerena.menu.roundpref.title");
		if (menu is null)
			return;

		foreach (RoundType roundType in RoundType.RoundTypes)
		{
			IMenuItem item = menu.CreateItem();
			item.Title = RoundItemTitle(roundType);
			item.Selected += _ =>
			{
				ToggleRoundPreference(roundType);
				item.Title = RoundItemTitle(roundType);
				Plugin.SavePlayer(this);
			};
		}

		menu.Display();
	}

	private string RoundItemTitle(RoundType roundType)
		=> Localizer.ForPlayer(Controller, RoundPreferences.Contains(roundType) ? "alerena.menu.roundpref.item_enabled" : "alerena.menu.roundpref.item_disabled", Localizer.ForPlayer(Controller, roundType.Name));

	private void ToggleRoundPreference(RoundType roundType)
	{
		bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
		if (isRoundTypeEnabled)
		{
			if (RoundPreferences.Count == 1)
			{
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "alerena.general.prefix")} {Localizer.ForPlayer(Controller, "alerena.chat.round_preferences_atleastone")}");
			}
			else
			{
				RoundChoices[roundType.Name] = false;
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "alerena.general.prefix")} {Localizer.ForPlayer(Controller, "alerena.chat.round_preferences_removed", Localizer.ForPlayer(Controller, roundType.Name))}");
			}
		}
		else
		{
			RoundChoices[roundType.Name] = true;
			Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "alerena.general.prefix")} {Localizer.ForPlayer(Controller, "alerena.chat.round_preferences_added", Localizer.ForPlayer(Controller, roundType.Name))}");
		}
	}

	public void ShowWeaponPreferenceMenu()
	{
		IMenu? menu = Plugin.CreateMenu(Controller, "alerena.menu.weaponpref.title");
		if (menu is null)
			return;

		foreach (WeaponType weaponType in Enum.GetValues<WeaponType>().Where(t => t != WeaponType.Unknown && IsAllowedWeaponType(t)))
		{
			IMenuItem item = menu.CreateItem();
			item.Title = Localizer.ForPlayer(Controller, $"alerena.rounds.{weaponType.ToString().ToLower()}");
			item.Selected += _ => ShowWeaponSubPreferenceMenu(menu, weaponType);
		}

		menu.Display();
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

	private void ShowWeaponSubPreferenceMenu(IMenu parent, WeaponType weaponType)
	{
		IMenu? menu = Plugin.CreateMenu(Controller, "alerena.menu.weaponpref.title", parent);
		if (menu is null)
			return;

		// null is "Random"
		IEnumerable<CsItem?> weapons = WeaponModel.GetWeaponList(weaponType).Where(w => WeaponModel.GetWeaponType(w) == weaponType).Select(w => (CsItem?)w);
		foreach (CsItem? weapon in weapons.Prepend(null))
		{
			string name = Localizer.ForPlayer(Controller, weapon?.ToString() ?? "alerena.general.random");
			IMenuItem item = menu.CreateItem();
			item.Title = Localizer.ForPlayer(Controller, GetWeaponPreference(weaponType) == weapon ? "alerena.menu.weaponpref.item_enabled" : "alerena.menu.weaponpref.item_disabled", name);
			item.Selected += _ =>
			{
				SetWeaponPreference(weaponType, weapon);
				Plugin.SavePlayer(this);
				menu.Close(); // back to the weapon types
			};
		}

		menu.Display();
	}

	private void SetWeaponPreference(WeaponType weaponType, CsItem? item)
	{
		WeaponChoices[weaponType] = item;
		Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "alerena.general.prefix")} {Localizer.ForPlayer(Controller, "alerena.chat.weapon_preferences_added", Localizer.ForPlayer(Controller, item?.ToString() ?? "alerena.general.random"))}");
	}
}