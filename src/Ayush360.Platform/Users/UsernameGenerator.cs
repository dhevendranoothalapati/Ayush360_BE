using System.Text;

namespace Ayush360.Platform.Users;

/// <summary>
/// Builds usernames: initials + user code + hospital id.
/// Dhevendra Noothalapati, user code 101, Swetha (tenant 10) -> "DN10110".
/// Initials = first letters of the first and last name; a one-word name uses its first two letters (Hari -> "HA").
/// Titles ("Dr.", "Mr.", ...) and anything in brackets are ignored.
/// </summary>
public static class UsernameGenerator
{
    private static readonly HashSet<string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        "DR", "MR", "MRS", "MS", "MISS", "PROF", "SRI", "SMT",
    };

    public static string Build(string displayName, int userCode, long tenantId) =>
        $"{Initials(displayName)}{userCode}{tenantId}";

    public static string Initials(string displayName)
    {
        // Drop "(Casualty)" style notes, then split into words of letters only.
        var withoutNotes = new StringBuilder();
        var depth = 0;
        foreach (var c in displayName)
        {
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0) withoutNotes.Append(c);
        }

        var words = withoutNotes.ToString()
            .Split(new[] { ' ', '.', ',', '-', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetter).ToArray()))
            .Where(w => w.Length > 0 && !Titles.Contains(w))
            .ToArray();

        if (words.Length == 0)
        {
            return "US";
        }

        var initials = words.Length == 1
            ? words[0][..Math.Min(2, words[0].Length)]
            : $"{words[0][0]}{words[^1][0]}";

        return initials.ToUpperInvariant().PadRight(2, 'X');
    }

    /// <summary>Upper-case form used for unique checks and login lookups.</summary>
    public static string Normalize(string username) => username.Trim().ToUpperInvariant();
}
