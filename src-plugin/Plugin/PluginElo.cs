namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Translations;
	using CounterStrikeSharp.API.Modules.Commands;
	using K4Arenas.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		// Real duels only: no warmup, sit-outs, bots, or players whose saved rating isn't loaded yet
		private void ApplyElo(Arena arena)
		{
			ArenaResult result = arena.Result;
			if (arena.ArenaID == -1 || result.ResultType is not (ArenaResultType.Win or ArenaResultType.Tie) || result.Winners is null || result.Losers is null)
				return;

			if (result.Winners.Concat(result.Losers).Any(p => !p.IsValid || p.Controller.IsBot || !p.Loaded))
				return;

			// For a tie, "winners" is just the first team
			bool tie = result.ResultType == ArenaResultType.Tie;
			double change = Elo.Change(result.Winners.Average(p => p.Rating), result.Losers.Average(p => p.Rating), tie ? 0.5 : 1, Config.EloSettings.KFactor);

			result.Winners.ForEach(p => UpdateRating(p, change, tie ? 0.5 : 1));
			result.Losers.ForEach(p => UpdateRating(p, -change, tie ? 0.5 : 0));
		}

		private void UpdateRating(ArenaPlayer player, double change, double score)
		{
			player.Rating += change;
			int shown = (int)Math.Round(player.Rating);

			player.Controller.Score = shown;
			Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_iScore");

			string key = change >= 0 ? "k4.chat.elo_gain" : "k4.chat.elo_loss";
			player.Controller.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, key, Math.Abs(Math.Round(change)), shown)}");

			ulong steamId = player.SteamID;
			string name = player.Controller.PlayerName;
			double rating = player.Rating;

			Task.Run(async () =>
			{
				try
				{
					await PlayerStore.SaveRatingAsync(DatabasePath, steamId, name, rating, score);
				}
				catch (Exception ex)
				{
					Logger.LogError("Failed to save rating: {0}", ex.Message);
				}
			});
		}

		public void Command_Top(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			int ownRating = (int)Math.Round(Arenas?.FindPlayer(player!)?.Rating ?? Config.EloSettings.StartRating);

			Task.Run(async () =>
			{
				try
				{
					List<PlayerStore.Rank> top = await PlayerStore.TopAsync(DatabasePath, 10);

					Server.NextFrame(() =>
					{
						if (player?.IsValid != true)
							return;

						string prefix = Localizer.ForPlayer(player, "k4.general.prefix");
						player.PrintToChat($" {prefix} {Localizer.ForPlayer(player, top.Count == 0 ? "k4.chat.top_empty" : "k4.chat.top_title")}");

						for (int i = 0; i < top.Count; i++)
							player.PrintToChat($" {Localizer.ForPlayer(player, "k4.chat.top_entry", i + 1, top[i].Name, (int)Math.Round(top[i].Rating), top[i].Wins, top[i].Losses, top[i].Draws)}");

						player.PrintToChat($" {prefix} {Localizer.ForPlayer(player, "k4.chat.top_self", ownRating)}");
					});
				}
				catch (Exception ex)
				{
					Logger.LogError("Failed to load the top list: {0}", ex.Message);
				}
			});
		}
	}
}
