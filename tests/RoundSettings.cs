// Run: dotnet run tests/RoundSettings.cs
// Rebuilding rounds from config (startup and the admin menu) must keep rounds added by other plugins.
#:package CounterStrikeSharp.API@1.0.374
#:project ../src-plugin/alerena.csproj
#:property PublishAot=false

using Alerena;
using Alerena.Models;

int failures = 0;
void Check(string name, bool ok)
{
	Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
	if (!ok) failures++;
}

var config = new PluginConfig();
Plugin.ApplyRoundSettings(config);
Check("config rounds loaded", RoundType.RoundTypes.Count == config.RoundSettings.Count);

int specialId = RoundType.AddSpecialRoundType("addon.headshot", 1, true, (_, _) => { }, (_, _) => { });

RoundTypeReader knife = config.RoundSettings.First(r => r.TranslationName == "alerena.rounds.knife");
knife.EnabledByDefault = !knife.EnabledByDefault;
Plugin.ApplyRoundSettings(config);

Check("toggled EnabledByDefault takes effect", RoundType.RoundTypes.First(r => r.Name == "alerena.rounds.knife").EnabledByDefault == knife.EnabledByDefault);
Check("round added through the API survives the rebuild", RoundType.RoundTypes.Count(r => r.Name == "addon.headshot") == 1);
Check("the API round keeps its ID (RemoveSpecialRound still works)", RoundType.RoundTypes.Single(r => r.Name == "addon.headshot").ID == specialId);
int secondId = RoundType.AddSpecialRoundType("addon.nades", 1, true, (_, _) => { }, (_, _) => { });
Check("a round added after a rebuild gets a fresh ID", secondId != specialId);
Check("all round IDs stay unique", RoundType.RoundTypes.Select(r => r.ID).Distinct().Count() == RoundType.RoundTypes.Count);
RoundType.RemoveSpecialRoundType(secondId);

RoundType.RemoveSpecialRoundType(specialId);
Check("RemoveSpecialRound removes it", RoundType.RoundTypes.All(r => r.Name != "addon.headshot"));

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;
