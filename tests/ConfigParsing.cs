// Run: dotnet run tests/ConfigParsing.cs
// Checks that config files parse the way CounterStrikeSharp parses them, and that
// mistakes (typos, wrong casing) are captured for the startup warnings instead of vanishing.
#:package CounterStrikeSharp.API@1.0.374
#:project ../src-plugin/alerena.csproj
// CounterStrikeSharp parses configs with reflection, which AOT (the default for file-based apps) disables
#:property PublishAot=false

using System.Text.Json;
using Alerena;
using Alerena.Models;
using AlerenaApi;

// Same options CounterStrikeSharp 1.0.374 uses (ConfigManager.JsonSerializerOptions)
var options = new JsonSerializerOptions { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip };
int failures = 0;
void Check(string name, bool ok)
{
	Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
	if (!ok) failures++;
}

// The default config, as CounterStrikeSharp generates it on first start
string generated = JsonSerializer.Serialize(new PluginConfig(), options);
Check("generated config has no UnknownKeys entry", !generated.Contains("UnknownKeys"));
Check("generated config writes weapon types as names", generated.Contains("\"PrimaryPreference\": \"Rifle\""));
PluginConfig roundTrip = JsonSerializer.Deserialize<PluginConfig>(generated, options)!;
Check("generated config parses back with 12 rounds", roundTrip.RoundSettings.Count == 12);
Check("generated config has no unknown keys", roundTrip.UnknownKeys is null && roundTrip.RoundSettings.All(r => r.UnknownKeys is null));

// A user config with typos, an old numeric enum, an enum by name and a round without a name
string user = """
{
  // comments are allowed
  "prevent-draws": true,
  "compatibility-settings": { "prevent-draw-rounds": false, "block-flash": true },
  "round-settings": [
    { "TranslationName": "alerena.rounds.rifle", "PrimaryPreference": 0, "primary-weapon": "weapon_ak47" },
    { "TranslationName": "alerena.rounds.awp", "PrimaryWeapon": "awp", "PrimaryPreference": "Sniper" },
    { "PrimaryWeapon": "weapon_deagle" }
  ],
  "ConfigVersion": 10
}
""";
PluginConfig config = JsonSerializer.Deserialize<PluginConfig>(user, options)!;
Check("known value applied", config.CompatibilitySettings.PreventDrawRounds == false);
Check("top-level typo captured", config.UnknownKeys?.ContainsKey("prevent-draws") == true);
Check("section typo captured", config.CompatibilitySettings.UnknownKeys?.ContainsKey("block-flash") == true);
Check("kebab-case key in a round captured", config.RoundSettings[0].UnknownKeys?.ContainsKey("primary-weapon") == true);
Check("old numeric weapon type still parses", config.RoundSettings[0].PrimaryPreference == WeaponType.Rifle);
Check("weapon type by name parses", config.RoundSettings[1].PrimaryPreference == WeaponType.Sniper);
Check("round without TranslationName parses (validator skips it)", config.RoundSettings[2].TranslationName == "");

// The shipped template (src-plugin/alerena.example.json) must parse with no ignored keys or weapons
string templatePath = Path.Combine(Path.GetDirectoryName(SourceFile())!, "..", "src-plugin", "alerena.example.json");
PluginConfig template = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(templatePath), options)!;
Check("template has no unknown keys", new[] { template.UnknownKeys, template.DatabaseSettings.UnknownKeys, template.CommandSettings.UnknownKeys,
	template.CompatibilitySettings.UnknownKeys, template.DefaultWeaponSettings.UnknownKeys, template.AllowedWeaponPreferences.UnknownKeys }
	.Concat(template.RoundSettings.Select(r => r.UnknownKeys)).All(keys => keys is null));
DefaultWeaponSettings dws = template.DefaultWeaponSettings;
Check("template weapons are all known", template.RoundSettings.SelectMany(r => new[] { r.PrimaryWeapon, r.SecondaryWeapon })
	.Concat([dws.DefaultRifle, dws.DefaultSniper, dws.DefaultSMG, dws.DefaultLMG, dws.DefaultShotgun, dws.DefaultPistol])
	.All(w => w is null || RoundType.FindEnumValueByEnumMemberValue(w) != null));
Check("template rounds all have a TranslationName", template.RoundSettings.All(r => !string.IsNullOrWhiteSpace(r.TranslationName)));
Check("template default-round matches a round", template.RoundSettings.Any(r => r.TranslationName == dws.DefaultRound));

// Weapon name lookup the validator relies on
Check("weapon_ak47 is a known weapon", RoundType.FindEnumValueByEnumMemberValue("weapon_ak47") != null);
Check("awp without prefix is unknown", RoundType.FindEnumValueByEnumMemberValue("awp") == null);

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;

static string SourceFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
