# K4-Arenas (friend-group fork)

A 1v1 arena gamemode for Counter-Strike 2, built for a regular group of friends on a private server. Everyone faces everyone, a visible Elo gives you something to play for, and nothing needs an external service.

This is a fork of [K4-Arenas](https://github.com/KitsuneLab-Development/K4-Arenas) by K4ryuu / KitsuneLab, which is archived. The roadmap and backlog live in [issue #10](https://github.com/ale-devl/K4-Arenas/issues/10).

## Features

- **Arenas on any map.** Spawn points are grouped into arenas automatically.
- **Rotation matchmaking.** Each round you face the opponent you've met least this session, never the same one twice in a row when it can be avoided. With an odd player count, sitting out (or facing a bot) rotates fairly. The classic winner-up / loser-down ladder is still available.
- **Elo, for show.** Shown on the scoreboard and after every duel (`Elo +14 → 1047`), with `!top` for the ranking. It never affects who you play against.
- **Your loadout.** `!guns` picks your weapon per category, `!rounds` picks which round types you want.
- **In-game settings.** `!arenaconfig` for admins: no JSON editing, no restart.
- **Plugin-local storage.** One SQLite file next to the config, no database server.
- 2v2 / 3v3 rounds, challenges (`!duel`), AFK handling, bot support, and a plugin API for custom rounds.

## Requirements

- A CS2 dedicated server with [Metamod:Source](https://www.sourcemm.net/)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) **1.0.374 or newer**, using the `with-runtime` package (it brings .NET 10)

The menu plugins ([CSSUniversalMenuAPI](https://github.com/CSGALS/CSSUniversalMenuAPI) and [SharpModMenu](https://github.com/CSGALS/SharpModMenu)) are included in the release zip.

## Install and update

1. Download `K4-Arenas.zip` from the [latest release](https://github.com/ale-devl/K4-Arenas/releases). `K4-Arenas-Bots.zip` is optional: it adds a bot to any arena where a player has no opponent.
2. Extract it into `game/csgo/addons/counterstrikesharp/`.
3. Restart the server.

Updating works the same way. Your `K4-Arenas.json` and the database are never overwritten; only the template next to them is.

Versions: `3.0.0-alpha.N` and `-beta.N` are test builds, published as prereleases. `3.0.0` is the first release that has been tested on a live server.

## Commands

| Command | What it does |
|---|---|
| `!guns` | Weapon preferences: pick a weapon per category, or Random |
| `!rounds` | Turn round types on or off for yourself |
| `!top`, `!elo` | Best 10 players by Elo, plus your own rating |
| `!duel <name>`, `!challenge <name>` | Challenge a player to a 1v1 next round |
| `!caccept`, `!cdecline` | Answer a challenge |
| `!afk` | Sit out until you use it again |
| `!queue` | Your position in the waiting queue |
| `!arenaconfig` | Admins (`@css/config`): arena settings menu |

All command names can be changed in `command-settings`.

**Menu controls (SharpModMenu):** W/S move, E select, A back, R exit. You can't move while a menu is open. If you bind `bind 1 "slot1;css_1"` up to `bind 0 "slot0;css_0"`, you get classic number-key menus instead.

## How matches are made

- **Rotation (default):** players are paired with whoever they've faced least since the server was last empty. With 6 players, 5 rounds are usually a complete round-robin.
- **Ladder:** the original K4-Arenas system. Arena 1 is the top; winners move up an arena, losers move down.
- **Round type:** picked at random from the rounds both players have enabled. If they share none, the `default-round` is used.

**Elo** starts at 1000 with a K-factor of 32 (both configurable):
- Beating a stronger player earns more.
- A draw counts as half a win.
- Team rounds compare team averages.
- Challenges count. Warmup, bots and sitting out don't.

## Configuration

| File | Purpose |
|---|---|
| `addons/counterstrikesharp/configs/plugins/K4-Arenas/K4-Arenas.json` | Plugin settings |
| `.../configs/plugins/K4-Arenas/K4-Arenas.example.json` | Template, copied to `K4-Arenas.json` when that file doesn't exist |
| `.../configs/plugins/K4-Arenas/k4-arenas.db` | Player preferences and Elo |
| `.../plugins/K4-Arenas/gameconfig.cfg` | Server cvars applied at every map start |

**Behavior:**
- **To start over,** delete `K4-Arenas.json` and restart. It's recreated from the template.
- **Missing keys** fall back to built-in defaults.
- **At startup,** the console names every key or value that is ignored (typos, unknown weapons) and logs which config folder was loaded.
- **The config folder is named after the plugin folder.** Renaming `plugins/K4-Arenas` also changes where the config is read from.
- **A `K4-Arenas.toml`** in the same folder takes priority over the `.json`.

**Format gotchas:**
- Entries inside `round-settings` use **PascalCase** keys (`TranslationName`, `PrimaryWeapon`). Everything else uses kebab-case.
- Weapons need the `weapon_` prefix: `weapon_ak47`, `weapon_awp`, `weapon_usp_silencer`.
- A round with `"PrimaryWeapon": null` and `"UsePreferredPrimary": true` gives each player their own choice from `!guns`. If they haven't picked one, they get the matching `default-*` weapon. A fixed `PrimaryWeapon` always overrides the player's choice.
- When `use-predefined-config` is on, `gameconfig.cfg` re-applies its cvars at every map start. Put `mp_*` changes there, not in `server.cfg`.

### Settings

| Key | Default | Meaning |
|---|---|---|
| `matchmaking` | `rotation` | `rotation` or `ladder` |
| `use-predefined-config` | `true` | Apply `gameconfig.cfg` at every map start |
| `database-settings.table-purge-days` | `0` | Delete players not seen for this many days. `0` keeps them forever |
| `round-settings` | 12 rounds | Round types. Per entry: `TranslationName`, `TeamSize`, `PrimaryWeapon`, `SecondaryWeapon`, `UsePreferredPrimary`, `PrimaryPreference` (`Rifle`, `Sniper`, `SMG`, `LMG`, `Shotgun`, `Pistol`, or `Unknown` = random), `UsePreferredSecondary`, `Armor`, `Helmet`, `EnabledByDefault` |
| `default-weapon-settings.default-*` | template: AK-47, AWP, Deagle | Weapon used until a player picks one. `null` = random |
| `default-weapon-settings.default-round` | `k4.rounds.rifle` | Round used when two players share no enabled round |
| `elo-settings.k-factor` / `start-rating` | `32` / `1000` | Elo tuning |
| `compatibility-settings.prevent-draw-rounds` | `true` | Pick a random winning team instead of a draw when alive counts are equal |
| `compatibility-settings.block-damage-of-not-opponent` | template: `true` | Undo damage from other arenas. A single lethal hit still kills |
| `compatibility-settings.block-flash-of-not-opponent` | template: `true` | Ignore flashes from other arenas |
| `compatibility-settings.give-knife-by-default` | `true` | Everyone gets a knife |
| `compatibility-settings.force-arena-clantags` | `false` | Re-apply the arena clan tag every second, for when another plugin overwrites it |
| `compatibility-settings.disable-clantags` | `false` | Never touch players' clan tags |
| `allowed-weapon-prefs.*` | `true` | Which categories appear in `!guns` |
| `command-settings.*` | see Commands | Command names. `center-announce-mode` announces arena, opponent and round type at round start (center screen and chat); `false` turns the announcement off |

## Development

```sh
dotnet publish src-plugin/K4-Arenas.csproj -c Release   # → src-plugin/bin/K4-Arenas/
dotnet run tests/ConfigParsing.cs                       # each tests/*.cs is a standalone check
```

You need the .NET 10 SDK. Every pull request is built by CI. CI runs all `tests/*.cs`, bundles the menu plugins, and publishes a prerelease named after the plugin version (`ModuleVersion` in `src-plugin/Plugin/PluginManifest.cs`), so bump that version in every PR.

Other plugins can add round types and query arena state through the shared API (`src-shared/K4-ArenaSharedApi.cs`, capability `k4-arenas:sharedapi`). `src-plugin/K4-Arenas-Example.cs` shows a custom round.

## Credits and license

- Original plugin: [K4ryuu / KitsuneLab](https://github.com/KitsuneLab-Development/K4-Arenas), with community contributors.
- Menus: CSSUniversalMenuAPI and SharpModMenu by [CS:GALS](https://github.com/CSGALS) (MIT, licenses included in the release).

Distributed under the GPL-3.0 license. See [`LICENSE.md`](LICENSE.md).
