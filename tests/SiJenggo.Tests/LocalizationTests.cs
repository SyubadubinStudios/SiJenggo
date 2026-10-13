using System.Text.Json;
using System.Text.RegularExpressions;
using SiJenggo.Game;
using SiJenggo.Game.Data;

namespace SiJenggo.Tests;

/// <summary>
/// Table-based languages (assets/lang/*.json, keyed by the English source text) must cover every inline
/// <see cref="LocText"/> in the game data and every interface string in the App's Strings.cs.
/// </summary>
public sealed partial class LocalizationTests
{
    private const string Lit = "\"((?:[^\"\\\\\\n]|\\\\.)*)\"";

    // new("Indonesian", "English") or new LocText("Indonesian", "English"); LocText.Same(..) is language-neutral.
    [GeneratedRegex(@"new(?:\s+LocText)?\(\s*" + Lit + @"\s*,\s*" + Lit + @"\s*\)")]
    private static partial Regex LocTextLiteral();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SiJenggo.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static Dictionary<string, string> LoadTable(string code)
    {
        string path = Path.Combine(RepoRoot(), "assets", "lang", code + ".json");
        Assert.True(File.Exists(path), $"missing {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            Assert.Equal(JsonValueKind.String, prop.Value.ValueKind);
            table[prop.Name] = prop.Value.GetString()!;
        }

        return table;
    }

    /// <summary>Every English source string written inline in Game sources and App/Ui/Strings.cs.</summary>
    private static List<(string En, string Where)> SourceStrings()
    {
        string root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "SiJenggo.Game"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Append(Path.Combine(root, "src", "SiJenggo.App", "Ui", "Strings.cs"));

        var result = new List<(string, string)>();
        foreach (string file in files)
        {
            foreach (Match m in LocTextLiteral().Matches(File.ReadAllText(file)))
            {
                result.Add((Regex.Unescape(m.Groups[2].Value), Path.GetFileName(file)));
            }
        }

        return result;
    }

    [Fact]
    public void Malay_table_covers_every_english_string()
    {
        var table = LoadTable(Localization.Code(Language.Malay));
        var strings = SourceStrings();
        Assert.True(strings.Count > 400, $"extractor found only {strings.Count} strings");

        var missing = strings.Where(s => !table.ContainsKey(s.En)).Select(s => $"{s.Where}: {s.En}").Distinct().ToList();
        Assert.True(missing.Count == 0, "Missing in ms.json:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Malay_table_has_no_empty_entries()
    {
        var empty = LoadTable("ms").Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
        Assert.True(empty.Count == 0, "Empty in ms.json:\n" + string.Join("\n", empty));
    }

    [Fact]
    public void Registered_table_translates_and_falls_back_to_english()
    {
        Localization.Register(Language.Malay, LoadTable("ms"));
        Assert.True(Localization.IsAvailable(Language.Malay));

        var tagline = Characters.Jenggo.Tagline;
        Assert.Equal("Samseng berhati malaikat", tagline[Language.Malay]);
        Assert.Equal(tagline.En, tagline[Language.English]);
        Assert.Equal(tagline.Id, tagline[Language.Indonesian]);

        var unknown = new LocText("Tidak ada", "Not in any table");
        Assert.Equal("Not in any table", unknown[Language.Malay]);

        // Campaign composes "Head: name" at runtime; both halves are translated.
        var composed = new LocText("Bab baru: Hutan Kalimantan", "New chapter: Borneo Rainforest");
        Assert.Equal("Bab baharu: Hutan Hujan Kalimantan", composed[Language.Malay]);
    }

    [Fact]
    public void Language_codes_round_trip()
    {
        foreach (var language in Localization.All)
        {
            Assert.True(Localization.TryParseCode(Localization.Code(language), out var parsed));
            Assert.Equal(language, parsed);
        }

        Assert.False(Localization.IsTableBased(Language.English));
        Assert.True(Localization.IsTableBased(Language.Malay));
    }
}
