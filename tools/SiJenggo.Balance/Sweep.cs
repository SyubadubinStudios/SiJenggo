using SiJenggo.Core.Ecs;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Balance;

internal enum RunKind : byte
{
    /// <summary>A locked arena with only the chapter's boss (fresh players, full lives).</summary>
    Boss,
    /// <summary>The full scrolling chapter: waves, then the boss.</summary>
    Chapter,
}

// <summary>Who drives the player slots.</summary>
internal enum BotKind : byte
{
    /// <summary>The in-game companion AI (<see cref="PlayerLoadout.IsAi"/>): never blocks, dodges or avoids hazards.</summary>
    Companion,
    /// <summary><see cref="SkilledBot"/>: reacts to telegraphs and hazards, a closer proxy for a competent human.</summary>
    Skilled,
}

internal enum Outcome : byte
{
    Win,
    Lose,
    Timeout,
}

/// <summary>One headless session to simulate.</summary>
internal sealed record Scenario(RunKind Kind, BotKind Bot, int Chapter, CharacterId Character, bool Coop, Difficulty Difficulty, ulong Seed)
{
    public CharacterId Partner => (CharacterId)(((int)Character + 1) % Characters.All.Length);
}

/// <summary>What happened in one simulated session.</summary>
internal sealed class RunResult
{
    public required Scenario Scenario { get; init; }
    public Outcome Outcome { get; set; }
    public int Ticks { get; set; }
    /// <summary>First tick the boss could act (intro finished); -1 if it never got there.</summary>
    public int BossStartTick { get; set; } = -1;
    /// <summary>Tick at which each phase ended (phase change or defeat); -1 if it did not.</summary>
    public int[] PhaseEndTick { get; } = [-1, -1, -1];
    public float BossHealthLeft { get; set; } = 1f;
    public float DamageTaken { get; set; }
    public float DamageFromBoss { get; set; }
    public float DamageFromEnemies { get; set; }
    public float DamageFromHazards { get; set; }
    /// <summary>Hazard damage split by <see cref="HazardKind"/> (best guess: the hazard under the victim); last slot is unattributed.</summary>
    public float[] HazardDamage { get; } = new float[HazardKinds + 1];
    /// <summary>Damage the boss dealt to players, per move id (projectiles report no move: id 0).</summary>
    public Dictionary<int, float> BossMoveDamage { get; } = [];
    public static readonly int HazardKinds = Enum.GetValues<HazardKind>().Length;
    public int LivesUsed { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }

    public int BossFightTicks => BossStartTick < 0 ? -1 : (PhaseEndTick[2] >= 0 ? PhaseEndTick[2] : Ticks) - BossStartTick;

    public int PhaseTicks(int phase)
    {
        int end = PhaseEndTick[phase];
        int start = phase == 0 ? BossStartTick : PhaseEndTick[phase - 1];
        return end < 0 || start < 0 ? -1 : end - start;
    }
}

/// <summary>Runs one scenario to completion (or the tick cap) with every player driven by the companion AI.</summary>
internal static class Sweep
{
    public static GameSession CreateSession(Scenario sc)
    {
        bool ai = sc.Bot == BotKind.Companion;
        var players = sc.Coop
            ? new[] { PlayerLoadout.Base(sc.Character, ai), PlayerLoadout.Base(sc.Partner, ai) }
            : new[] { PlayerLoadout.Base(sc.Character, ai) };
        var config = new SessionConfig
        {
            Mode = sc.Kind == RunKind.Boss ? GameModeKind.BossRush : GameModeKind.Story,
            Difficulty = sc.Difficulty,
            Players = players,
            Seed = sc.Seed,
            Lives = 3,
        };
        var chapter = Chapters.Get(sc.Chapter);
        var plan = sc.Kind == RunKind.Boss
            ? new StagePlan { Kind = StageKind.BossArena, Chapter = chapter, Boss = chapter.Boss }
            : new StagePlan { Kind = StageKind.Chapter, Chapter = chapter };
        return new GameSession(config, plan);
    }

    public static RunResult Run(Scenario sc, int tickCap)
    {
        var s = CreateSession(sc);
        var result = new RunResult { Scenario = sc };
        var inputs = new InputFrame[s.PlayerCount];
        var bots = new SkilledBot?[s.PlayerCount];
        for (int i = 0; i < bots.Length && sc.Bot == BotKind.Skilled; i++)
        {
            bots[i] = new SkilledBot(i, sc.Seed);
        }

        int lastPhase = 0;

        while (s.State is not (SessionState.StageClear or SessionState.GameOver) && s.Tick < tickCap)
        {
            for (int i = 0; i < bots.Length; i++)
            {
                inputs[i] = new InputFrame(bots[i]?.Think(s) ?? InputButtons.None);
            }

            s.Step(inputs);
            var boss = s.BossEntity;
            if (s.World.IsAlive(boss) && s.World.Has<Boss>(boss))
            {
                ref var b = ref s.World.Get<Boss>(boss);
                if (result.BossStartTick < 0 && b.IntroDone)
                {
                    result.BossStartTick = s.Tick;
                }

                for (; lastPhase < b.Phase && lastPhase < 2; lastPhase++)
                {
                    result.PhaseEndTick[lastPhase] = s.Tick;
                }

                result.BossHealthLeft = s.World.Get<Health>(boss).Fraction;
            }

            Tally(s, result, ref lastPhase);
            s.Events.Clear();
        }

        result.Ticks = s.Tick;
        result.Outcome = s.State switch
        {
            SessionState.StageClear => Outcome.Win,
            SessionState.GameOver => Outcome.Lose,
            _ => Outcome.Timeout,
        };

        for (int i = 0; i < s.PlayerCount; i++)
        {
            var e = s.PlayerEntity(i);
            if (!s.World.IsAlive(e))
            {
                continue;
            }

            var p = s.World.Get<Player>(e);
            result.LivesUsed += s.Config.Lives - p.Lives + (s.World.Get<Health>(e).Dead ? 1 : 0);
        }

        return result;
    }

    /// <summary>Index of the hazard kind whose area covers <paramref name="point"/>, or the unattributed slot.</summary>
    private static int HazardAt(GameSession s, System.Numerics.Vector3 point)
    {
        var hazards = s.World.Pool<Hazard>();
        var ids = hazards.EntityIds;
        var comps = hazards.Components;
        int best = RunResult.HazardKinds;
        float bestD = float.MaxValue;
        for (int i = 0; i < ids.Length; i++)
        {
            var hp = s.World.Pool<Transform>().Get(ids[i]).Position;
            float dx = hp.X - point.X, dz = hp.Z - point.Z;
            float d = MathF.Sqrt(dx * dx + dz * dz) - comps[i].Radius;
            if (d < 0.8f && d < bestD)
            {
                bestD = d;
                best = (int)comps[i].Kind;
            }
        }

        return best;
    }

    private static void Tally(GameSession s, RunResult result, ref int lastPhase)
    {
        foreach (ref readonly var ev in s.Events.Items)
        {
            switch (ev.Kind)
            {
                case GameEventKind.Hit when s.World.IsAlive(ev.Target) && s.World.Has<Player>(ev.Target):
                    result.DamageTaken += ev.Value;
                    if (ev.Source == s.BossEntity && !ev.Source.IsNull)
                    {
                        result.DamageFromBoss += ev.Value;
                        result.BossMoveDamage[ev.Move.Value] = result.BossMoveDamage.GetValueOrDefault(ev.Move.Value) + ev.Value;
                    }
                    else if (s.World.IsAlive(ev.Source) && s.World.Has<Enemy>(ev.Source))
                    {
                        result.DamageFromEnemies += ev.Value;
                    }
                    else
                    {
                        result.DamageFromHazards += ev.Value;
                        result.HazardDamage[HazardAt(s, ev.Position)] += ev.Value;
                    }

                    break;
                case GameEventKind.BossDefeated:
                    for (; lastPhase < 3; lastPhase++)
                    {
                        result.PhaseEndTick[lastPhase] = s.Tick;
                    }

                    result.BossHealthLeft = 0;
                    break;
                case GameEventKind.PlayerDown:
                    result.Downs++;
                    break;
                case GameEventKind.PlayerRevived:
                    result.Revives++;
                    break;
            }
        }
    }
}
