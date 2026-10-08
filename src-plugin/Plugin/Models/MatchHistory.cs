namespace K4Arenas.Models;

// Session memory for rotation matchmaking: who met whom, who each player faced last, who sat out
public sealed class MatchHistory
{
	// Random orderings tried per round; the cheapest wins. Plenty for friend-group sizes.
	private const int Attempts = 50;
	private const int BackToBackPenalty = 10;

	private readonly Dictionary<(ulong, ulong), int> meetings = [];
	private readonly Dictionary<ulong, ulong> lastOpponent = [];
	private readonly Dictionary<ulong, int> byes = [];

	private static (ulong, ulong) Key(ulong a, ulong b) => a < b ? (a, b) : (b, a);

	public int Meetings(ulong a, ulong b) => meetings.GetValueOrDefault(Key(a, b));

	public int Byes(ulong player) => byes.GetValueOrDefault(player);

	public void RecordMatch(ulong a, ulong b)
	{
		meetings[Key(a, b)] = Meetings(a, b) + 1;
		lastOpponent[a] = b;
		lastOpponent[b] = a;
	}

	public void RecordBye(ulong player)
	{
		byes[player] = Byes(player) + 1;
		lastOpponent.Remove(player);
	}

	public void Clear()
	{
		meetings.Clear();
		lastOpponent.Clear();
		byes.Clear();
	}

	// Returns an order of indices into `players` where consecutive pairs (0-1, 2-3, ...) are this round's matches:
	// least-met opponents first, no back-to-back rematch when avoidable. With an odd count the last index sits out,
	// going to whoever sat out least. Indices (not ids) so duplicate ids can't break the mapping back.
	public List<int> Order(IReadOnlyList<ulong> players, Random random)
	{
		List<int> best = [.. Enumerable.Range(0, players.Count)];
		int bestCost = int.MaxValue;

		for (int attempt = 0; attempt < Attempts && bestCost > 0; attempt++)
		{
			List<int> pool = [.. Enumerable.Range(0, players.Count).OrderBy(_ => random.Next())];

			int? sitOut = null;
			if (pool.Count % 2 == 1)
			{
				sitOut = pool.MinBy(i => Byes(players[i]));
				pool.Remove(sitOut.Value);
			}

			List<int> order = [];
			int cost = 0;
			while (pool.Count > 0)
			{
				int player = pool[0];
				pool.RemoveAt(0);

				int opponent = pool.MinBy(o => Cost(players[player], players[o]));
				pool.Remove(opponent);

				cost += Cost(players[player], players[opponent]);
				order.Add(player);
				order.Add(opponent);
			}

			if (sitOut is int last)
				order.Add(last);

			if (cost < bestCost)
			{
				best = order;
				bestCost = cost;
			}
		}

		return best;
	}

	private int Cost(ulong a, ulong b)
		=> Meetings(a, b) + (lastOpponent.TryGetValue(a, out ulong last) && last == b ? BackToBackPenalty : 0);
}
