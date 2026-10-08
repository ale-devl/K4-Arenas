// Run: dotnet run tests/Translations.cs
// Every translation key the code or the config template uses must exist in every language file,
// otherwise players see the raw key (e.g. "alerena.chat.top_title") instead of text.
#:property PublishAot=false

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourceFile())!, ".."));
int failures = 0;

// Keys are "alerena.<group>.<name>"; one-segment names like "alerena.db" are file names, not keys
var keyPattern = new Regex(@"""(alerena\.[a-z_]+\.[a-z0-9_.]+)""");
HashSet<string> used = [];
foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src-plugin"), "*.cs", SearchOption.AllDirectories)
	.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
	.Append(Path.Combine(root, "src-plugin", "alerena.example.json")))
	used.UnionWith(keyPattern.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value));

// !guns builds these from the WeaponType names
used.UnionWith(new[] { "rifle", "sniper", "smg", "lmg", "shotgun", "pistol" }.Select(t => $"alerena.rounds.{t}"));

foreach (string langFile in Directory.EnumerateFiles(Path.Combine(root, "src-plugin", "lang"), "*.json"))
{
	var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langFile))!.Keys.ToHashSet();
	List<string> missing = [.. used.Except(keys).Order()];
	Console.WriteLine($"{(missing.Count == 0 ? "PASS" : "FAIL")} {Path.GetFileName(langFile)} has all {used.Count} keys{(missing.Count == 0 ? "" : ", missing: " + string.Join(", ", missing))}");
	if (missing.Count > 0) failures++;
}

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;

static string SourceFile([CallerFilePath] string path = "") => path;
