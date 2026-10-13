using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

public enum GameModeKind : byte
{
    Story,
    Arcade,
    Survival,
    BossRush,
    Challenge,
}

public enum Difficulty : byte
{
    Santai,
    Normal,
    Jagoan,
}

/// <summary>Everything that decides how strong one player's fighter is at spawn.</summary>
public sealed class PlayerLoadout
{
    public required CharacterId Character { get; init; }
    public bool IsAi { get; init; }
    public FighterStats Stats { get; init; }
    public float MaxHealth { get; init; }
    public MoveId Jurus { get; init; }
    public float SpecialCostMultiplier { get; init; } = 1f;
    public float SemangatRegen { get; init; } = 1f;

    /// <summary>Base stats with no progression applied (Arcade, quick play).</summary>
    public static PlayerLoadout Base(CharacterId character, bool isAi = false) => StatsBuilder.Build(character, null, isAi);
}

public sealed class SessionConfig
{
    public GameModeKind Mode { get; init; } = GameModeKind.Story;
    public Difficulty Difficulty { get; init; } = Difficulty.Normal;
    public required PlayerLoadout[] Players { get; init; }
    public ulong Seed { get; init; } = 0x5EED_1E66_0u;
    public int Lives { get; init; } = 3;
}

public enum StageKind : byte
{
    /// <summary>A full scrolling chapter: waves then the boss.</summary>
    Chapter,
    /// <summary>A locked arena with endless waves (Survival).</summary>
    SurvivalArena,
    /// <summary>A locked arena with only the boss (Boss Rush).</summary>
    BossArena,
}

public sealed class StagePlan
{
    public required StageKind Kind { get; init; }
    public required ChapterDef Chapter { get; init; }
    public BossId? Boss { get; init; }
    /// <summary>Enemy health/damage multiplier on top of the difficulty setting.</summary>
    public float Scaling { get; init; } = 1f;
    public ChallengeDef? Challenge { get; init; }
}
