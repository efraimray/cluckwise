using System.Text.RegularExpressions;

namespace CluckWise;

/// <summary>
/// Wake-up challenges that must be solved before an alarm can be dismissed (snoozing stays free).
/// The puzzle logic lives here so it can be tested; RingWindow draws it.
/// </summary>
public static class WakeChallenge
{
    public record MathsProblem(string Question, int Answer);

    // Original CluckWise phrases.
    private static readonly string[] Phrases =
    {
        "Good morning, today is a fresh start",
        "I am awake and ready to rise wise",
        "One small step now, a big day later",
        "My feet are on the floor and my eyes are open",
        "The early bird gets the best start",
        "Today I will do the most important thing first",
        "Up and at it, the day is waiting",
        "Stretch, breathe, and get going",
    };

    public static string Describe(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Maths => "Solve a maths problem",
        ChallengeKind.TypePhrase => "Type a phrase",
        ChallengeKind.TapTiles => "Tap numbers in order",
        _ => "Off",
    };

    /// <summary>How many maths problems in a row: easy 1, normal 2, hard 3.</summary>
    public static int MathsCount(int level) => Math.Clamp(level, 1, 3);

    public static MathsProblem Maths(int level, Random rnd)
    {
        switch (Math.Clamp(level, 1, 3))
        {
            case 1:
            {
                int a = rnd.Next(4, 21), b = rnd.Next(3, 20);
                return new($"{a} + {b}", a + b);
            }
            case 2:
            {
                int a = rnd.Next(3, 13), b = rnd.Next(3, 10), c = rnd.Next(5, 41);
                return new($"{a} × {b} + {c}", a * b + c);
            }
            default:
            {
                int a = rnd.Next(6, 20), b = rnd.Next(6, 13), c = rnd.Next(10, 60);
                return new($"{a} × {b} − {c}", a * b - c);
            }
        }
    }

    /// <summary>Easy: a short phrase. Normal: any phrase. Hard: two phrases joined.</summary>
    public static string Phrase(int level, Random rnd)
    {
        var shortOnes = Phrases.Where(p => p.Length <= 40).ToArray();
        return Math.Clamp(level, 1, 3) switch
        {
            1 => shortOnes[rnd.Next(shortOnes.Length)],
            2 => Phrases[rnd.Next(Phrases.Length)],
            _ => Phrases[rnd.Next(Phrases.Length)] + ", " + Phrases[rnd.Next(Phrases.Length)].ToLowerInvariant(),
        };
    }

    /// <summary>Typing check that forgives capital letters, extra spaces and a final full stop.</summary>
    public static bool PhraseMatches(string expected, string typed)
    {
        static string Norm(string s) => Regex.Replace(s.Trim().TrimEnd('.', '!'), @"\s+", " ").ToLowerInvariant();
        return Norm(expected) == Norm(typed);
    }

    /// <summary>Easy 6 tiles, normal 9, hard 12; numbers are random and must be tapped smallest first.</summary>
    public static int[] Tiles(int level, Random rnd)
    {
        int count = Math.Clamp(level, 1, 3) switch { 1 => 6, 2 => 9, _ => 12 };
        var numbers = new HashSet<int>();
        while (numbers.Count < count) numbers.Add(rnd.Next(1, 100));
        return numbers.OrderBy(_ => rnd.Next()).ToArray();
    }

    public static int TileColumns(int count) => count <= 6 ? 3 : count <= 9 ? 3 : 4;
}
