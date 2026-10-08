namespace Alerena
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Translations;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Commands.Targeting;
	using CounterStrikeSharp.API.Modules.Utils;
	using Alerena.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Commands()
		{
			Config.CommandSettings.QueueCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Checks the queue position for the 1v1 arena", Command_Queue);
			});

			Config.CommandSettings.RoundsCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Opens the round preference menu", Command_RoundPref);
			});

			Config.CommandSettings.GunsCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Opens the weapon preference menu", Command_WeaponPref);
			});

			Config.CommandSettings.AdminCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Opens the arena settings menu", Command_ArenaConfig);
			});

			Config.CommandSettings.TopCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Shows the best players by Elo", Command_Top);
			});

			Config.CommandSettings.AFKCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Toggles the AFK status", Command_AFK);
			});

			Config.CommandSettings.ChallengeCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Challenges a player to a 1v1", Command_Challenge);
			});

			Config.CommandSettings.ChallengeAcceptCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Accepts a challenge", Command_Accept);
			});

			Config.CommandSettings.ChallengeDeclineCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Declines a challenge", Command_Decline);
			});
		}

		public void Command_Accept(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			ChallengeModel? challenge = FindChallengeForPlayer(p1.Controller);

			if (challenge is null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.notchallenged")}");
				return;
			}

			ArenaPlayer p2 = challenge.Player1;

			if (!p2.IsValid)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.notavailable")}");
				Challenges.Remove(challenge);
				return;
			}

			challenge.IsAccepted = true;

			info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.accepted", p2.Controller.PlayerName)}");
			p2.Controller.PrintToChat($" {Localizer.ForPlayer(p2.Controller, "alerena.general.prefix")} {Localizer.ForPlayer(p2.Controller, "alerena.general.challenge.acceptedby", player!.PlayerName)}");
		}

		public void Command_Decline(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			ChallengeModel? challenge = FindChallengeForPlayer(p1.Controller);

			if (challenge is null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.notchallenged")}");
				return;
			}

			ArenaPlayer p2 = challenge.Player1;

			if (!p2.IsValid)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.notavailable")}");
				Challenges.Remove(challenge);
				return;
			}

			Challenges.Remove(challenge);

			info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.declined", p2.Controller.PlayerName)}");
			p2.Controller.PrintToChat($" {Localizer.ForPlayer(p2.Controller, "alerena.general.prefix")} {Localizer.ForPlayer(p2.Controller, "alerena.general.challenge.declinedby", player!.PlayerName)}");
		}

		public void Command_Challenge(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY, 1, "[name]"))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			ChallengeModel? challenge = FindChallengeForPlayer(p1.Controller);

			if (challenge != null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.inchallenge")}");
				return;
			}

			TargetResult targetResult = info.GetArgTargetResult(1);
			if (targetResult.Count() != 1)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.invalidtarget")}");
				return;
			}

			CCSPlayerController challengedPlayer = targetResult.First();
			ArenaPlayer? p2 = Arenas?.FindPlayer(challengedPlayer);

			if (p2 is null || p2 == p1)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.invalidtarget")}");
				return;
			}

			ChallengeModel? enemyChallenge = FindChallengeForPlayer(p2.Controller);

			if (enemyChallenge != null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.inchallenge")}");
				return;
			}

			int p1ArenaID = GetPlayerArenaID(p1);
			int p2ArenaID = GetPlayerArenaID(p2);

			if (p1ArenaID == -1 || p2ArenaID == -1)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.notinarena")}");
				return;
			}

			ChallengeModel newChallenge = new(p1!, p2, p1ArenaID, p2ArenaID);
			Challenges.Add(newChallenge);

			info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.general.challenge.waiting", challengedPlayer.PlayerName)}");

			if (p2.Controller.IsBot)
			{
				AddTimer(1, () =>
				{
					ArenaPlayer fromPlayer = newChallenge.Player1;

					if (!fromPlayer.IsValid)
					{
						Challenges.Remove(newChallenge);
						return;
					}

					newChallenge.IsAccepted = true;

					fromPlayer.Controller.PrintToChat($" {Localizer.ForPlayer(fromPlayer.Controller, "alerena.general.prefix")} {Localizer.ForPlayer(fromPlayer.Controller, "alerena.general.challenge.acceptedby", p2.Controller.PlayerName)}");
				});
			}
			else
			{
				challengedPlayer.PrintToChat($" {Localizer.ForPlayer(challengedPlayer, "alerena.general.prefix")} {Localizer.ForPlayer(challengedPlayer, "alerena.general.challenge.request", player!.PlayerName, Config.CommandSettings.ChallengeAcceptCommands.FirstOrDefault("Missing"), Config.CommandSettings.ChallengeDeclineCommands.FirstOrDefault("Missing"))}");
			}
		}

		public void Command_AFK(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player!);

			if (arenaPlayer is null)
				return;

			arenaPlayer.AFK = !arenaPlayer.AFK;

			if (arenaPlayer.AFK)
			{
				player!.ChangeTeam(CsTeam.Spectator);
				arenaPlayer.ArenaTag = $"{Localizer["alerena.general.afk"]} |";

				if (!Config.CompatibilitySettings.DisableClantags)
				{
					player.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
				}

				TerminateRoundIfPossible();
			}
			else
			{
				arenaPlayer.ArenaTag = $"{Localizer["alerena.general.waiting"]} |";

				if (!Config.CompatibilitySettings.DisableClantags)
				{
					arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
				}
			}

			info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {(arenaPlayer.AFK ? string.Format(Localizer.ForPlayer(player, "alerena.chat.afk_enabled"), Config.CommandSettings.AFKCommands.FirstOrDefault("Missing")) : Localizer.ForPlayer(player, "alerena.chat.afk_disabled"))}");
		}

		public void Command_Queue(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			int queuePlace = WaitingArenaPlayers.ToList().FindIndex(p => p.Controller == player);

			if (queuePlace == -1)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.chat.queue_not_in_queue")}");
				return;
			}

			info.ReplyToCommand($" {Localizer.ForPlayer(player, "alerena.general.prefix")} {Localizer.ForPlayer(player, "alerena.chat.queue_position", queuePlace + 1)}");
		}

		public void Command_RoundPref(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			Arenas?.FindPlayer(player!)?.ShowRoundPreferenceMenu();
		}

		public void Command_WeaponPref(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			Arenas?.FindPlayer(player!)?.ShowWeaponPreferenceMenu();
		}
	}
}