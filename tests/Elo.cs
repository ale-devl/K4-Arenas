// Run: dotnet run tests/Elo.cs
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
bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

Check("equal ratings: a win is worth half the K-factor", Near(Elo.Change(1000, 1000, 1, 32), 16));
Check("equal ratings: a draw changes nothing", Near(Elo.Change(1000, 1000, 0.5, 32), 0));
Check("beating a stronger player is worth more than beating a weaker one", Elo.Change(1000, 1200, 1, 32) > Elo.Change(1200, 1000, 1, 32));
Check("a draw against a stronger player gains rating", Elo.Change(1000, 1200, 0.5, 32) > 0);
Check("zero-sum: the other side loses exactly what this side gains", Near(Elo.Change(1100, 950, 1, 32), -Elo.Change(950, 1100, 0, 32)));
Check("expected scores of both sides add up to 1", Near(Elo.Expected(1234, 987) + Elo.Expected(987, 1234), 1));
Check("400 points apart: the favourite is expected to win 10 of 11", Near(Elo.Expected(1400, 1000), 10.0 / 11));

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;
