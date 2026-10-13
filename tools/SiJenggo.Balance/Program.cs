using System.Diagnostics;
using SiJenggo.Balance;
using SiJenggo.Game;
using SiJenggo.Game.Data;

// Headless balance sweep: every boss (and optionally every full chapter) x character x difficulty x solo/co-op
// x seed, all players driven by the companion AI. Sessions are independent, so they run in parallel.
//
//   dotnet run -c Release --project tools/SiJenggo.Balance -- [options]
//     --seeds N          seeds per configuration (default 8)
//     --chapters         also run every full chapter (waves + boss), slower
//     --only-chapters    run full chapters only
//     --chapter I[,J]    restrict to these chapter indices (0-based)
//     --boss-cap S       tick cap for boss arenas, in seconds (default 600)
//     --chapter-cap S    tick cap for full chapters, in seconds (default 1500)
//     --threads N        worker threads (default: all cores)
//     --out PATH         markdown report path (default docs/en/balance-report.md)
//     --csv PATH         also write one raw row per session
//     --bot LIST         skilled,companion (default both): who drives the players
//     --stdout           print the report instead of writing --out
var options = Options.Parse(args);
if (options is null)
{
    return 1;
}

var scenarios = new List<Scenario>();
var difficulties = new[] { Difficulty.Santai, Difficulty.Normal, Difficulty.Jagoan };
foreach (var kind in options.Kinds)
{
    foreach (int chapter in options.Chapters)
    {
        foreach (var character in Characters.All)
        {
            foreach (var difficulty in difficulties)
            {
                foreach (bool coop in new[] { false, true })
                {
                    foreach (var bot in options.Bots)
                    {
                        for (int seed = 0; seed < options.Seeds; seed++)
                        {
                            ulong value = 0x5EED_0000UL + (ulong)seed * 1_000_003UL + (ulong)chapter * 7919UL + (ulong)character.Id * 104_729UL;
                            scenarios.Add(new Scenario(kind, bot, chapter, character.Id, coop, difficulty, value));
                        }
                    }
                }
            }
        }
    }
}

Console.Error.WriteLine($"Simulating {scenarios.Count} sessions on {options.Threads} threads...");
var results = new RunResult[scenarios.Count];
var clock = Stopwatch.StartNew();
int done = 0;
Parallel.For(0, scenarios.Count, new ParallelOptions { MaxDegreeOfParallelism = options.Threads }, i =>
{
    var sc = scenarios[i];
    int cap = (sc.Kind == RunKind.Boss ? options.BossCapSeconds : options.ChapterCapSeconds) * 60;
    results[i] = Sweep.Run(sc, cap);
    int n = Interlocked.Increment(ref done);
    if (n % 50 == 0 || n == scenarios.Count)
    {
        Console.Error.WriteLine($"  {n}/{scenarios.Count}  ({clock.Elapsed.TotalSeconds:0}s)");
    }
});
clock.Stop();

long simTicks = results.Sum(r => (long)r.Ticks);
string report = Report.Build(results, options, clock.Elapsed, simTicks);
if (options.Stdout)
{
    Console.WriteLine(report);
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutPath))!);
    File.WriteAllText(options.OutPath, report);
    Console.Error.WriteLine($"Report written to {options.OutPath}");
}

if (options.CsvPath is { } csv)
{
    File.WriteAllText(csv, Report.Csv(results));
    Console.Error.WriteLine($"Raw rows written to {csv}");
}

Console.Error.WriteLine($"Done in {clock.Elapsed.TotalSeconds:0.0}s ({simTicks / 60.0 / 3600.0:0.0} simulated hours).");
return 0;

internal sealed class Options
{
    public int Seeds { get; private set; } = 8;
    public List<RunKind> Kinds { get; } = [RunKind.Boss];
    public List<BotKind> Bots { get; } = [BotKind.Skilled, BotKind.Companion];
    public List<int> Chapters { get; } = [];
    public int BossCapSeconds { get; private set; } = 600;
    public int ChapterCapSeconds { get; private set; } = 1500;
    public int Threads { get; private set; } = Environment.ProcessorCount;
    public string OutPath { get; private set; } = Path.Combine("docs", "en", "balance-report.md");
    public string? CsvPath { get; private set; }
    public bool Stdout { get; private set; }

    public static Options? Parse(string[] args)
    {
        var o = new Options();
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seeds": o.Seeds = int.Parse(args[++i]); break;
                    case "--chapters": o.Kinds.Add(RunKind.Chapter); break;
                    case "--only-chapters": o.Kinds.Clear(); o.Kinds.Add(RunKind.Chapter); break;
                    case "--chapter":
                        foreach (var part in args[++i].Split(','))
                        {
                            o.Chapters.Add(int.Parse(part));
                        }

                        break;
                    case "--boss-cap": o.BossCapSeconds = int.Parse(args[++i]); break;
                    case "--chapter-cap": o.ChapterCapSeconds = int.Parse(args[++i]); break;
                    case "--threads": o.Threads = Math.Max(1, int.Parse(args[++i])); break;
                    case "--out": o.OutPath = args[++i]; break;
                    case "--csv": o.CsvPath = args[++i]; break;
                    case "--stdout": o.Stdout = true; break;
                    case "--bot":
                        o.Bots.Clear();
                        foreach (var part in args[++i].Split(','))
                        {
                            o.Bots.Add(Enum.Parse<BotKind>(part, ignoreCase: true));
                        }

                        break;
                    default:
                        Console.Error.WriteLine($"Unknown option {args[i]}");
                        return null;
                }
            }
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException or ArgumentException)
        {
            Console.Error.WriteLine($"Bad arguments: {ex.Message}");
            return null;
        }

        if (o.Chapters.Count == 0)
        {
            for (int c = 0; c < SiJenggo.Game.Data.Chapters.All.Length; c++)
            {
                o.Chapters.Add(c);
            }
        }

        return o;
    }
}
