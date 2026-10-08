namespace Alerena
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Translations;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Extensions;
	using CSSUniversalMenuAPI;
	using Alerena.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		// Drawn by whichever CSSUniversalMenuAPI driver is installed (SharpModMenu ships in the release zip)
		public IMenu? CreateMenu(CCSPlayerController player, string titleKey, IMenu? parent = null)
		{
			if (UniversalMenu.DefaultDriver is null)
			{
				player.PrintToChat($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.chat.no_menu_driver")}");
				Logger.LogError("No menu plugin installed: menus need CSSUniversalMenuAPI with a driver such as SharpModMenu");
				return null;
			}

			IMenu menu = parent is null ? UniversalMenu.CreateMenu(player) : UniversalMenu.CreateMenu(parent);
			menu.Title = Localizer.ForPlayer(player, titleKey);
			return menu;
		}

		public void Command_ArenaConfig(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY, permission: "@css/config"))
				return;

			IMenu? menu = CreateMenu(player!, "alerena.admin.title");
			if (menu is null)
				return;

			string Text(string key) => Localizer.ForPlayer(player, key);

			AddEntry(menu, () => $"{Text("alerena.admin.matchmaking")}: {Text(UseRotation ? "alerena.admin.rotation" : "alerena.admin.ladder")}", _ =>
			{
				Config.Matchmaking = UseRotation ? "ladder" : "rotation";
				SaveConfig();
			});
			AddEntry(menu, () => Text("alerena.admin.default_rounds"), _ => ShowDefaultRoundsMenu(player!, menu));
			AddEntry(menu, () => $"{Text("alerena.admin.fallback_round")}: {Text(Config.DefaultWeaponSettings.DefaultRound ?? "alerena.general.random")}",
				item => ShowFallbackRoundMenu(player!, menu, () => item.Title = $"{Text("alerena.admin.fallback_round")}: {Text(Config.DefaultWeaponSettings.DefaultRound ?? "alerena.general.random")}"));

			CompatibilitySettings Compat() => Config.CompatibilitySettings;
			AddToggle(menu, player!, () => Text("alerena.admin.prevent_draws"), () => Compat().PreventDrawRounds, v => Compat().PreventDrawRounds = v);
			AddToggle(menu, player!, () => Text("alerena.admin.block_damage"), () => Compat().BlockDamageOfNotOpponent, v => Compat().BlockDamageOfNotOpponent = v);
			AddToggle(menu, player!, () => Text("alerena.admin.block_flash"), () => Compat().BlockFlashOfNotOpponent, v => Compat().BlockFlashOfNotOpponent = v);
			AddToggle(menu, player!, () => Text("alerena.admin.knife"), () => Compat().GiveKnifeByDefault, v => Compat().GiveKnifeByDefault = v);

			menu.Display();
		}

		private void ShowDefaultRoundsMenu(CCSPlayerController player, IMenu parent)
		{
			IMenu? menu = CreateMenu(player, "alerena.admin.default_rounds", parent);
			if (menu is null)
				return;

			foreach (RoundTypeReader round in Config.RoundSettings)
				AddToggle(menu, player, () => Localizer.ForPlayer(player, round.TranslationName), () => round.EnabledByDefault, v => round.EnabledByDefault = v);

			menu.Display();
		}

		private void ShowFallbackRoundMenu(CCSPlayerController player, IMenu parent, Action refreshParent)
		{
			IMenu? menu = CreateMenu(player, "alerena.admin.fallback_round", parent);
			if (menu is null)
				return;

			foreach (RoundType round in RoundType.RoundTypes.Where(r => r.TeamSize < 2 && r.StartFunction == null))
			{
				string key = Config.DefaultWeaponSettings.DefaultRound == round.Name ? "alerena.menu.weaponpref.item_enabled" : "alerena.menu.weaponpref.item_disabled";
				AddEntry(menu, () => Localizer.ForPlayer(player, key, Localizer.ForPlayer(player, round.Name)), _ =>
				{
					Config.DefaultWeaponSettings.DefaultRound = round.Name;
					SaveConfig();
					refreshParent();
					menu.Close();
				});
			}

			menu.Display();
		}

		// A menu entry whose title shows the current value; selecting runs `select` and refreshes the title
		private static IMenuItem AddEntry(IMenu menu, Func<string> title, Action<IMenuItem> select)
		{
			IMenuItem item = menu.CreateItem();
			item.Title = title();
			item.Selected += _ =>
			{
				select(item);
				item.Title = title();
			};
			return item;
		}

		private void AddToggle(IMenu menu, CCSPlayerController player, Func<string> label, Func<bool> get, Action<bool> set)
			=> AddEntry(menu, () => $"{label()}: {Localizer.ForPlayer(player, get() ? "alerena.admin.on" : "alerena.admin.off")}", _ =>
			{
				set(!get());
				SaveConfig();
			});

		// Writes alerena.json and applies the change right away
		private void SaveConfig()
		{
			try
			{
				Config.Update();
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not save the config: {0}", ex.InnerException?.Message ?? ex.Message);
			}

			ApplyRoundSettings(Config);
		}
	}
}
