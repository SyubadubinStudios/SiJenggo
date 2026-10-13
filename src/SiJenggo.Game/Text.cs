namespace SiJenggo.Game;

/// <summary>
/// Player-facing languages. Indonesian and English are written inline in every <see cref="LocText"/>;
/// every later value is table based: a JSON file <c>assets/lang/&lt;code&gt;.json</c> maps the English source text
/// to the translation and is handed to <see cref="Localization.Register"/> at startup.
/// Adding a language = a new value here + its code in <see cref="Localization.Code"/> + the JSON file.
/// </summary>
public enum Language : byte
{
    Indonesian,
    English,
    Malay,
}

/// <summary>A piece of player-facing text. Indonesian and English inline; other languages via <see cref="Localization"/>.</summary>
public readonly record struct LocText(string Id, string En)
{
    public string this[Language language] => language switch
    {
        Language.Indonesian => Id,
        Language.English => En,
        _ => Localization.Translate(language, En),
    };

    public override string ToString() => Id;

    public static LocText Same(string text) => new(text, text);
}

/// <summary>
/// Translation tables for the table-based languages (everything after <see cref="Language.English"/>).
/// The Game project stays IO-free: the App reads the JSON files and calls <see cref="Register"/>.
/// Lookups are plain dictionary reads (no allocation); a missing entry falls back to English.
/// </summary>
public static class Localization
{
    private static readonly Language[] s_all = Enum.GetValues<Language>();
    private static readonly Dictionary<string, string>?[] s_tables = new Dictionary<string, string>?[s_all.Length];

    /// <summary>Every language the game knows about, in enum order.</summary>
    public static IReadOnlyList<Language> All => s_all;

    /// <summary>ISO 639-1 code, also the JSON file name (<c>assets/lang/ms.json</c>).</summary>
    public static string Code(Language language) => language switch
    {
        Language.Indonesian => "id",
        Language.English => "en",
        Language.Malay => "ms",
        _ => "en",
    };

    /// <summary>The language's own name, for the settings picker.</summary>
    public static string NativeName(Language language) => language switch
    {
        Language.Indonesian => "Bahasa Indonesia",
        Language.English => "English",
        Language.Malay => "Bahasa Melayu",
        _ => Code(language).ToUpperInvariant(),
    };

    public static bool TryParseCode(string code, out Language language)
    {
        foreach (var l in s_all)
        {
            if (string.Equals(Code(l), code, StringComparison.OrdinalIgnoreCase))
            {
                language = l;
                return true;
            }
        }

        language = Language.English;
        return false;
    }

    /// <summary>True when the language's text lives in a table rather than inline in <see cref="LocText"/>.</summary>
    public static bool IsTableBased(Language language) => language > Language.English;

    /// <summary>Inline languages are always available; table-based ones once their table is registered.</summary>
    public static bool IsAvailable(Language language) =>
        !IsTableBased(language) || ((int)language < s_tables.Length && s_tables[(int)language] is not null);

    /// <summary>Installs (or replaces) the English → translation table of a table-based language.</summary>
    public static void Register(Language language, IReadOnlyDictionary<string, string> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!IsTableBased(language) || (int)language >= s_tables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Only table-based languages take a translation table.");
        }

        var copy = new Dictionary<string, string>(table.Count, StringComparer.Ordinal);
        foreach (var (en, text) in table)
        {
            if (!string.IsNullOrEmpty(text))
            {
                copy[en] = text;
            }
        }

        s_tables[(int)language] = copy;
    }

    /// <summary>Removes a registered table (tests).</summary>
    public static void Unregister(Language language)
    {
        if ((int)language < s_tables.Length)
        {
            s_tables[(int)language] = null;
        }
    }

    /// <summary>Translates English source text; falls back to the English text when there is no entry.</summary>
    public static string Translate(Language language, string en)
    {
        if ((int)language >= s_tables.Length || s_tables[(int)language] is not { } table)
        {
            return en;
        }

        if (table.TryGetValue(en, out var text))
        {
            return text;
        }

        // Composed text such as "New chapter: Borneo Rainforest" (Campaign unlocks): translate both halves.
        // Only reached for strings that are not in the table, so the allocation stays off the common path.
        int colon = en.IndexOf(": ", StringComparison.Ordinal);
        if (colon > 0)
        {
            bool headFound = table.TryGetValue(en[..colon], out var head);
            bool tailFound = table.TryGetValue(en[(colon + 2)..], out var tail);
            if (headFound || tailFound)
            {
                return string.Concat(headFound ? head : en[..colon], ": ", tailFound ? tail : en[(colon + 2)..]);
            }
        }

        return en;
    }
}
