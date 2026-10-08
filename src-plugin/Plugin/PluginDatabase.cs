using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using Dapper;
using Alerena.Models;
using AlerenaApi;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Alerena;

// Player preferences in a plugin-local SQLite file. Only explicit choices are stored;
// anything a player never picked follows the config, so config changes reach them too.
public static class PlayerStore
{
	// Weapons: weapon type -> item name, null = picked "Random". Rounds: round name -> enabled.
	public sealed record Preferences(Dictionary<WeaponType, CsItem?> Weapons, Dictionary<string, bool> Rounds);

	public sealed record Rank(string Name, double Rating, long Wins, long Losses, long Draws);

	private static async Task<SqliteConnection> OpenAsync(string path)
	{
		var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
		await connection.OpenAsync();
		return connection;
	}

	public static async Task InitializeAsync(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		await using SqliteConnection connection = await OpenAsync(path);
		await connection.ExecuteAsync("""
			PRAGMA journal_mode = WAL;
			CREATE TABLE IF NOT EXISTS players (
				steamid64 INTEGER PRIMARY KEY,
				weapons TEXT NOT NULL DEFAULT '{}',
				rounds TEXT NOT NULL DEFAULT '{}',
				lastseen TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
			);
			CREATE TABLE IF NOT EXISTS ratings (
				steamid64 INTEGER PRIMARY KEY,
				name TEXT NOT NULL DEFAULT '',
				rating REAL NOT NULL,
				wins INTEGER NOT NULL DEFAULT 0,
				losses INTEGER NOT NULL DEFAULT 0,
				draws INTEGER NOT NULL DEFAULT 0
			);
			""");
	}

	public static async Task<Preferences> LoadAsync(string path, ulong steamId)
	{
		await using SqliteConnection connection = await OpenAsync(path);
		(string weapons, string rounds) = await connection.QuerySingleAsync<(string, string)>("""
			INSERT INTO players (steamid64) VALUES (@SteamId)
			ON CONFLICT (steamid64) DO UPDATE SET lastseen = CURRENT_TIMESTAMP
			RETURNING weapons, rounds;
			""", new { SteamId = (long)steamId });

		return new Preferences(ParseWeapons(weapons), ParseRounds(rounds));
	}

	public static async Task SaveAsync(string path, ulong steamId, Preferences preferences)
	{
		await using SqliteConnection connection = await OpenAsync(path);
		await connection.ExecuteAsync("""
			INSERT INTO players (steamid64, weapons, rounds) VALUES (@SteamId, @Weapons, @Rounds)
			ON CONFLICT (steamid64) DO UPDATE SET weapons = excluded.weapons, rounds = excluded.rounds, lastseen = CURRENT_TIMESTAMP;
			""", new
		{
			SteamId = (long)steamId,
			Weapons = JsonSerializer.Serialize(preferences.Weapons.ToDictionary(w => w.Key.ToString(), w => w.Value?.ToString())),
			Rounds = JsonSerializer.Serialize(preferences.Rounds)
		});
	}

	// Null when the player has no rated duel yet
	public static async Task<double?> LoadRatingAsync(string path, ulong steamId)
	{
		await using SqliteConnection connection = await OpenAsync(path);
		return await connection.QuerySingleOrDefaultAsync<double?>("SELECT rating FROM ratings WHERE steamid64 = @SteamId;", new { SteamId = (long)steamId });
	}

	// score: 1 win, 0.5 draw, 0 loss
	public static async Task SaveRatingAsync(string path, ulong steamId, string name, double rating, double score)
	{
		await using SqliteConnection connection = await OpenAsync(path);
		await connection.ExecuteAsync("""
			INSERT INTO ratings (steamid64, name, rating, wins, losses, draws) VALUES (@SteamId, @Name, @Rating, @Win, @Loss, @Draw)
			ON CONFLICT (steamid64) DO UPDATE SET name = excluded.name, rating = excluded.rating,
				wins = wins + excluded.wins, losses = losses + excluded.losses, draws = draws + excluded.draws;
			""", new { SteamId = (long)steamId, Name = name, Rating = rating, Win = score == 1 ? 1 : 0, Loss = score == 0 ? 1 : 0, Draw = score == 0.5 ? 1 : 0 });
	}

	public static async Task<List<Rank>> TopAsync(string path, int count)
	{
		await using SqliteConnection connection = await OpenAsync(path);
		return [.. await connection.QueryAsync<Rank>("SELECT name, rating, wins, losses, draws FROM ratings ORDER BY rating DESC LIMIT @Count;", new { Count = count })];
	}

	public static async Task<int> PurgeAsync(string path, int days)
	{
		if (days <= 0)
			return 0;

		await using SqliteConnection connection = await OpenAsync(path);
		int removed = await connection.ExecuteAsync("DELETE FROM players WHERE lastseen < datetime('now', @Age);", new { Age = $"-{days} days" });
		await connection.ExecuteAsync("DELETE FROM ratings WHERE steamid64 NOT IN (SELECT steamid64 FROM players);");
		return removed;
	}

	// Entries that no longer parse (renamed items, hand-edited rows) are dropped instead of failing the whole load
	private static Dictionary<WeaponType, CsItem?> ParseWeapons(string json)
	{
		Dictionary<WeaponType, CsItem?> weapons = [];
		foreach ((string type, string? item) in TryDeserialize<Dictionary<string, string?>>(json) ?? [])
		{
			if (!Enum.TryParse(type, out WeaponType weaponType))
				continue;

			if (item is null)
				weapons[weaponType] = null;
			else if (Enum.TryParse(item, out CsItem csItem))
				weapons[weaponType] = csItem;
		}
		return weapons;
	}

	private static Dictionary<string, bool> ParseRounds(string json)
		=> TryDeserialize<Dictionary<string, bool>>(json) ?? [];

	private static T? TryDeserialize<T>(string json)
	{
		try { return JsonSerializer.Deserialize<T>(json); }
		catch (JsonException) { return default; }
	}
}

public sealed partial class Plugin : BasePlugin
{
	// Next to the config: the plugin folder is replaced on every update, the config folder is not
	public string DatabasePath => Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", Path.GetFileName(ModuleDirectory), "alerena.db"));

	public async Task LoadPlayerAsync(ulong steamId)
	{
		try
		{
			PlayerStore.Preferences preferences = await PlayerStore.LoadAsync(DatabasePath, steamId);
			double? rating = await PlayerStore.LoadRatingAsync(DatabasePath, steamId);

			Server.NextFrame(() =>
			{
				ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(steamId);
				if (arenaPlayer is null)
					return;

				arenaPlayer.WeaponChoices = preferences.Weapons;
				arenaPlayer.RoundChoices = preferences.Rounds;
				arenaPlayer.Rating = rating ?? Config.EloSettings.StartRating;
				arenaPlayer.Loaded = true;
			});
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to load player preferences: {0}", ex.Message);
		}
	}

	public void SavePlayer(ArenaPlayer player)
	{
		// Saving before the load finished would overwrite the stored choices
		if (!player.Loaded)
			return;

		ulong steamId = player.SteamID;
		PlayerStore.Preferences snapshot = new(new(player.WeaponChoices), new(player.RoundChoices));

		Task.Run(async () =>
		{
			try
			{
				await PlayerStore.SaveAsync(DatabasePath, steamId, snapshot);
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to save player preferences: {0}", ex.Message);
			}
		});
	}

	public async Task PurgeDatabaseAsync()
	{
		try
		{
			await PlayerStore.PurgeAsync(DatabasePath, Config.DatabaseSettings.TablePurgeDays);
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to purge old players: {0}", ex.Message);
		}
	}
}
