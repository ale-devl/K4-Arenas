# alerena

**1v1 arenas for Counter-Strike 2, built for a group of friends.** Everyone faces everyone, an Elo rating gives you something to play for, and it all runs from one plugin folder: no database server, no web services.

```
round 1   Alex vs Sam     Kim vs Jo     Max vs Lee
round 2   Alex vs Kim     Sam vs Max    Jo vs Lee      ← nobody gets the same opponent twice in a row
...       after 5 rounds with 6 players, usually everyone has met everyone once

chat      [alerena] Elo +14 → 1047
```

## Features

- **Rotation matchmaking.** Each round you face whoever you've played least this session. Sitting out (or facing a bot) rotates fairly with odd player counts.
- **Elo, just for show.** It's on the scoreboard and in chat after every duel, and `!top` shows the ranking. It never changes who you play.
- **Your loadout.** `!guns` sets your weapon per category, `!rounds` picks which round types you want.
- **Settings in-game.** Admins use `!arenaconfig`: no file editing, no restart.
- **Works on any map.** Spawn points are grouped into arenas automatically. Also included: 2v2 / 3v3 rounds, `!duel` challenges, AFK handling, and a bot for whoever has no opponent.

## Install

You need a CS2 dedicated server with [Metamod:Source](https://www.sourcemm.net/) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) **1.0.374 or newer** (the `with-runtime` package).

1. Download `alerena.zip` from the [releases](https://github.com/ale-devl/alerena/releases)
2. Extract it into `game/csgo/addons/counterstrikesharp/`
3. Restart the server

That's all: the menu plugins ([CSSUniversalMenuAPI](https://github.com/CSGALS/CSSUniversalMenuAPI) and [SharpModMenu](https://github.com/CSGALS/SharpModMenu)) are in the zip, and so is the bots addon: with an odd player count, whoever has no opponent gets a bot. With an even count it does nothing.

**Updating:** extract the new zip over the old one. Your `alerena.json` and `alerena.db` are never touched.

**Coming from K4-Arenas:** delete `plugins/K4-Arenas`, `plugins/K4-Arenas-Bots` and `shared/K4-ArenaSharedApi` first, so both plugins don't run at once. Old K4-Arenas addons need rebuilding against `alerena-api` before they connect.

**Versions:** `-alpha` and `-beta` are untested development builds (GitHub prereleases). A release candidate (`-rc.N`) is the installable build being tested on a live server. A plain version like `3.0.0` has passed that test.

## Commands

| Command | |
|---|---|
| `!guns` | Pick your weapon per category, or Random |
| `!rounds` | Turn round types on or off for yourself |
| `!top` / `!elo` | Best 10 by Elo, plus your own rating |
| `!duel <name>` / `!challenge <name>` | Challenge someone for next round; they answer with `!caccept` or `!cdecline` |
| `!afk` | Sit out until you use it again |
| `!queue` | Your place in the waiting queue |
| `!arenaconfig` | Admins (`@css/config`): the settings menu |

**Menus:** W/S to move, E to select, A to go back, R to exit. You can't move while a menu is open. If you prefer number keys, bind `bind 1 "slot1;css_1"` … `bind 0 "slot0;css_0"`.

## How it plays

- **Matchmaking:** `rotation` (default) pairs least-met opponents. `ladder` is the classic system: arena 1 is the top, winners move up, losers move down.
- **Round type:** a random pick from the rounds both players have on. If they share none, the `default-round` is used.
- **Elo:** start 1000, K-factor 32.
  - Beating a stronger player earns more, and a draw counts as half a win.
  - Team rounds compare averages.
  - Challenges count. Warmup and bots don't.

## Configuration

Everything lives in `addons/counterstrikesharp/configs/plugins/alerena/`:

| File | |
|---|---|
| `alerena.json` | Settings. Delete it to start over from the template |
| `alerena.example.json` | The template, used when `alerena.json` doesn't exist |
| `alerena.db` | Preferences and Elo |

`gameconfig.cfg` in the plugin folder holds server cvars and is applied at every map start. Put `mp_*` changes there, not in `server.cfg`.

**Watch out for:**
- Round entries in `round-settings` use **PascalCase** keys (`PrimaryWeapon`). Everything else uses kebab-case (`prevent-draw-rounds`).
- Weapons need the `weapon_` prefix: `weapon_ak47`, `weapon_usp_silencer`.
- `"PrimaryWeapon": null` with `"UsePreferredPrimary": true` means each player's own `!guns` choice. If they haven't picked one, they get the `default-*` weapon. A fixed weapon overrides everyone's choice.
- At startup the console names every ignored key or unknown weapon, and logs which config was loaded.

| Setting | Default | |
|---|---|---|
| `matchmaking` | `rotation` | `rotation` or `ladder` |
| `round-settings` | 12 rounds | Per round: `TranslationName`, `TeamSize`, `PrimaryWeapon`, `SecondaryWeapon`, `UsePreferredPrimary`, `PrimaryPreference` (`Rifle`, `Sniper`, `SMG`, `LMG`, `Shotgun`, `Pistol`, `Unknown` = random), `UsePreferredSecondary`, `Armor`, `Helmet`, `EnabledByDefault` |
| `default-weapon-settings` | AK-47, AWP, Deagle | `default-rifle` … `default-pistol` until a player picks; `default-round` when two players share no round |
| `elo-settings` | `32` / `1000` | `k-factor` and `start-rating` |
| `compatibility-settings` | | `prevent-draw-rounds` (random winner instead of a draw), `block-damage-of-not-opponent` (a single lethal hit still kills), `block-flash-of-not-opponent`, `give-knife-by-default`, `force-arena-clantags`, `disable-clantags` |
| `allowed-weapon-prefs` | all `true` | Categories shown in `!guns` |
| `command-settings` | see Commands | Command names. `center-announce-mode: false` turns off the round-start announcement |
| `database-settings.table-purge-days` | `0` | Forget players not seen for this many days. `0` keeps them forever |
| `use-predefined-config` | `true` | Apply `gameconfig.cfg` at map start |

## Development

```sh
dotnet publish src-plugin/alerena.csproj -c Release   # → src-plugin/bin/alerena/
dotnet run tests/Rotation.cs                          # every tests/*.cs is a standalone check
```

- **You need** the .NET 10 SDK.
- **Every pull request** is built and tested by CI, which publishes a prerelease named after `ModuleVersion` in `src-plugin/Plugin/PluginManifest.cs`. Bump that version in every PR.
- **The backlog** is in the [issues](https://github.com/ale-devl/alerena/issues), with the roadmap in #10.
- **Addons** can add round types through `alerena-api` (capability `alerena:api`). See `src-plugin/AddonExample.cs`.

## Credits and license

alerena started as a fork of [K4-Arenas](https://github.com/KitsuneLab-Development/K4-Arenas) by K4ryuu / KitsuneLab and its contributors, and stays under the same **GPL-3.0** license ([`LICENSE.md`](LICENSE.md)). The bots addon is by Cruze. The menus come from [CS:GALS](https://github.com/CSGALS) (MIT, licenses included in the release).
