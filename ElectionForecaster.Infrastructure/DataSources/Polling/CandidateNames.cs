using System.Text.RegularExpressions;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Matches a candidate name as a poll table prints it against the name the model carries for them.
/// Wikipedia's matchup headers are written by hand, so the same person appears as "Matt Dunlap" and
/// "Matthew Dunlap", or with a generational suffix the roster omits — a miss here silently drops a
/// race's polling, so the comparison is deliberately forgiving.
/// </summary>
public static class CandidateNames
{
    /// <summary>True when both names plausibly denote the same person.</summary>
    public static bool Match(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var x = Normalize(a);
        var y = Normalize(b);
        if (x.Length == 0 || y.Length == 0) return false;
        if (x.Contains(y) || y.Contains(x)) return true;
        return SameNameLoose(Tokens(x), Tokens(y));
    }

    /// <summary>Same surname, with first names agreeing by prefix ("matt"/"matthew") or as a known diminutive.</summary>
    private static bool SameNameLoose(string[] a, string[] b)
    {
        if (a.Length == 0 || b.Length == 0) return false;
        if (a[^1] != b[^1]) return false;
        var (fa, fb) = (a[0], b[0]);
        if (fa.StartsWith(fb, StringComparison.Ordinal) || fb.StartsWith(fa, StringComparison.Ordinal)) return true;
        return (Diminutives.TryGetValue(fa, out var formal) && formal == fb)
            || (Diminutives.TryGetValue(fb, out var formal2) && formal2 == fa);
    }

    // Short → formal first names that aren't plain prefixes (those the StartsWith check covers).
    private static readonly Dictionary<string, string> Diminutives = new()
    {
        ["bob"] = "robert", ["bill"] = "william", ["jim"] = "james", ["dick"] = "richard",
        ["rick"] = "richard", ["ted"] = "edward", ["jack"] = "john", ["chuck"] = "charles",
        ["hank"] = "henry", ["tony"] = "anthony", ["steve"] = "stephen", ["andy"] = "andrew",
        ["greg"] = "gregory", ["ken"] = "kenneth", ["mike"] = "michael", ["liz"] = "elizabeth",
        ["beth"] = "elizabeth", ["betty"] = "elizabeth", ["peggy"] = "margaret", ["kate"] = "katherine",
        ["katie"] = "katherine", ["becca"] = "rebecca", ["abby"] = "abigail",
    };

    /// <summary>Name tokens with punctuation and generational suffixes stripped.</summary>
    private static string[] Tokens(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t is not ("jr" or "sr" or "ii" or "iii" or "iv" or "v"))
            .ToArray();

    private static string Normalize(string s) =>
        Regex.Replace(s.Trim().ToLowerInvariant().Replace(".", "").Replace(",", ""), @"\s+", " ");
}
