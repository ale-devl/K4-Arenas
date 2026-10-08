// Run: dotnet run tests/PlayerStore.cs
// Exercises the SQLite preference store against a real database file.
#:package CounterStrikeSharp.API@1.0.374
#:project ../src-plugin/K4-Arenas.csproj
// Dapper maps rows with reflection, which AOT (the default for file-based apps) disables
#:property PublishAot=false

using CounterStrikeSharp.API.Modules.Entities.Constants;
using Dapper;
using K4Arenas;
using K4ArenaSharedApi;
using Microsoft.Data.Sqlite;

int failures = 0;
void Check(string name, bool ok)
{
	Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
	if (!ok) failures++;
}

string dir = Path.Combine(Path.GetTempPath(), $"k4-arenas-test-{Guid.NewGuid():N}");
string db = Path.Combine(dir, "k4-arenas.db");
const ulong Alice = 76561198000000001, Bob = 76561198000000002;

try
{
	await PlayerStore.InitializeAsync(db);
	await PlayerStore.InitializeAsync(db); // second start must not fail
	Check("database file created", File.Exists(db));

	await using (var connection = new SqliteConnection($"Data Source={db}"))
		Check("WAL mode enabled", await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode;") == "wal");

	PlayerStore.Preferences fresh = await PlayerStore.LoadAsync(db, Alice);
	Check("new player has no choices", fresh.Weapons.Count == 0 && fresh.Rounds.Count == 0);

	await PlayerStore.SaveAsync(db, Alice, new(
		new() { [WeaponType.Rifle] = CsItem.M4A1S, [WeaponType.Pistol] = null },
		new() { ["k4.rounds.knife"] = true, ["k4.rounds.awp"] = false }));
	PlayerStore.Preferences saved = await PlayerStore.LoadAsync(db, Alice);
	Check("weapon choice round-trips", saved.Weapons.GetValueOrDefault(WeaponType.Rifle) == CsItem.M4A1S);
	Check("\"Random\" (null) round-trips", saved.Weapons.ContainsKey(WeaponType.Pistol) && saved.Weapons[WeaponType.Pistol] is null);
	Check("untouched weapon types stay unset", !saved.Weapons.ContainsKey(WeaponType.Sniper));
	Check("round toggles round-trip by name", saved.Rounds["k4.rounds.knife"] && !saved.Rounds["k4.rounds.awp"]);

	Check("no rating before the first rated duel", await PlayerStore.LoadRatingAsync(db, Alice) is null);
	await PlayerStore.SaveRatingAsync(db, Alice, "Alice", 1016, 1);
	await PlayerStore.SaveRatingAsync(db, Alice, "Alice", 1016, 0.5);
	await PlayerStore.SaveRatingAsync(db, Alice, "Alice (renamed)", 1002, 0);
	await PlayerStore.SaveRatingAsync(db, Bob, "Bob", 1050, 1);
	Check("rating round-trips", await PlayerStore.LoadRatingAsync(db, Alice) == 1002);
	List<PlayerStore.Rank> top = await PlayerStore.TopAsync(db, 10);
	Check("top list is sorted by rating and uses the latest name", top.Select(r => r.Name).SequenceEqual(["Bob", "Alice (renamed)"]));
	Check("wins, losses and draws add up", top[1] is { Wins: 1, Losses: 1, Draws: 1 });

	await using (var connection = new SqliteConnection($"Data Source={db}"))
	{
		// Unknown names (e.g. renamed items) and broken JSON must not break loading
		await connection.ExecuteAsync("""UPDATE players SET weapons = '{"Rifle":"NoSuchGun","Sniper":"AWP","Bogus":"AK47"}', rounds = 'not json' WHERE steamid64 = @Id""", new { Id = (long)Alice });
		await PlayerStore.LoadAsync(db, Bob);
		await connection.ExecuteAsync("UPDATE players SET lastseen = datetime('now', '-40 days') WHERE steamid64 = @Id", new { Id = (long)Bob });
	}

	PlayerStore.Preferences messy = await PlayerStore.LoadAsync(db, Alice);
	Check("unknown weapon entries are dropped", !messy.Weapons.ContainsKey(WeaponType.Rifle) && messy.Weapons.Count == 1);
	Check("valid entries next to broken ones survive", messy.Weapons.GetValueOrDefault(WeaponType.Sniper) == CsItem.AWP);
	Check("broken round JSON loads as no choices", messy.Rounds.Count == 0);

	Check("purge days 0 keeps everyone", await PlayerStore.PurgeAsync(db, 0) == 0);
	Check("purge removes only players older than the limit", await PlayerStore.PurgeAsync(db, 30) == 1);
	Check("recent player kept after purge", (await PlayerStore.LoadAsync(db, Alice)).Weapons.Count == 1);
	Check("purge also drops the ratings of removed players", (await PlayerStore.TopAsync(db, 10)).Select(r => r.Name).SequenceEqual(["Alice (renamed)"]));
}
finally
{
	SqliteConnection.ClearAllPools();
	Directory.Delete(dir, recursive: true);
}

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;
