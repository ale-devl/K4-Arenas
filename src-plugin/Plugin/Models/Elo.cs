namespace Alerena.Models;

// Standard Elo. Display only: ratings never influence matchmaking.
public static class Elo
{
	// Chance (0-1) that a side rated `rating` beats one rated `opponent`
	public static double Expected(double rating, double opponent)
		=> 1 / (1 + Math.Pow(10, (opponent - rating) / 400));

	// Rating change for the `rating` side; score is 1 for a win, 0.5 for a draw, 0 for a loss.
	// The other side changes by the negative of this.
	public static double Change(double rating, double opponent, double score, double kFactor)
		=> kFactor * (score - Expected(rating, opponent));
}
