// Run: dotnet run tests/Rotation.cs
// Simulates sessions of rotation matchmaking and checks everyone meets everyone, fairly.
#:package CounterStrikeSharp.API@1.0.374
#:project ../src-plugin/K4-Arenas.csproj
#:property PublishAot=false

using K4Arenas.Models;

int failures = 0;
void Check(string name, bool ok)
{
	Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
	if (!ok) failures++;
}

// Plays `rounds` rounds like the plugin does: pair consecutive players, the odd one out sits out
(MatchHistory history, int backToBack, bool permutations) Simulate(ulong[] players, int rounds, int seed)
{
	var history = new MatchHistory();
	var random = new Random(seed);
	var last = new Dictionary<ulong, ulong>();
	int backToBack = 0;
	bool permutations = true;

	for (int round = 0; round < rounds; round++)
	{
		List<int> order = history.Order(players, random);
		permutations &= order.Count == players.Length && order.Distinct().Count() == players.Length;

		for (int i = 0; i + 1 < order.Count; i += 2)
		{
			ulong a = players[order[i]], b = players[order[i + 1]];
			if (last.GetValueOrDefault(a) == b)
				backToBack++;
			history.RecordMatch(a, b);
			last[a] = b;
			last[b] = a;
		}

		if (order.Count % 2 == 1)
		{
			ulong sitOut = players[order[^1]];
			history.RecordBye(sitOut);
			last.Remove(sitOut);
		}
	}
	return (history, backToBack, permutations);
}

IEnumerable<(ulong, ulong)> Pairs(ulong[] players)
	=> players.SelectMany((a, i) => players.Skip(i + 1).Select(b => (a, b)));

ulong[] six = [1, 2, 3, 4, 5, 6];
ulong[] five = [1, 2, 3, 4, 5];

for (int seed = 0; seed < 20; seed++)
{
	var (h6, b6, p6) = Simulate(six, 10, seed);
	var (h5, b5, p5) = Simulate(five, 10, seed);

	if (seed == 0)
	{
		Check("orders are permutations of the players", p6 && p5);
		Check("6 players, 5 rounds: everyone met everyone exactly once", Pairs(six).All(p => Simulate(six, 5, seed).history.Meetings(p.Item1, p.Item2) == 1));
	}

	if (!Pairs(six).All(p => h6.Meetings(p.Item1, p.Item2) >= 1) || b6 > 0 || !p6)
	{ Check($"6 players, 10 rounds, seed {seed}: all pairs met, no back-to-back rematch", false); }
	if (!Pairs(five).All(p => h5.Meetings(p.Item1, p.Item2) >= 1) || b5 > 0 || !p5)
	{ Check($"5 players, 10 rounds, seed {seed}: all pairs met, no back-to-back rematch", false); }
	if (five.Any(p => h5.Byes(p) != 2))
	{ Check($"5 players, 10 rounds, seed {seed}: everyone sat out exactly twice", false); }
}
Check("6 and 5 players over 20 seeds: all pairs met, no back-to-back rematches, sit-outs even", failures == 0);

var (two, _, _) = Simulate([7, 8], 3, 0);
Check("2 players always face each other", two.Meetings(7, 8) == 3);

Check("duplicate ids (unauthenticated players) still give a valid order",
	new MatchHistory().Order([0, 0, 0, 0], new Random(1)).OrderBy(i => i).SequenceEqual([0, 1, 2, 3]));

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;
