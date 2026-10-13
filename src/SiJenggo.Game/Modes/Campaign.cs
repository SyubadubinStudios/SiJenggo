using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Game.Persistence;

namespace SiJenggo.Game;

public sealed class PlayerResult
{
    public required int Slot { get; init; }
    public required CharacterId Character { get; init; }
    public required int Score { get; init; }
    public required int KnockOuts { get; init; }
    public required int BestCombo { get; init; }
    public required int Xp { get; init; }
    public int LevelBefore { get; init; }
    public int LevelAfter { get; init; }
}

/// <summary>What a finished stage gave the player, shown on the results screen.</summary>
public sealed class StageResult
{
    public required bool Cleared { get; init; }
    public required SessionStats Stats { get; init; }
    public required IReadOnlyList<PlayerResult> Players { get; init; }
    public List<LocText> Unlocks { get; } = [];
    public List<AchievementDef> NewAchievements { get; } = [];
    public int TotalScore => Players.Sum(p => p.Score);
    public TimeSpan Time => TimeSpan.FromSeconds(Stats.Ticks / 60.0);
}

/// <summary>Who is playing and how: one entry per player slot.</summary>
public readonly record struct PlayerSlot(CharacterId Character, bool IsAi);

/// <summary>
/// Runs a game mode as a sequence of stages (one chapter in Story, all seven in Arcade, seven arenas in
/// Boss Rush...), builds each <see cref="GameSession"/>, and writes rewards back into the <see cref="Profile"/>.
/// </summary>
public sealed class Campaign
{
    private readonly List<StagePlan> _stages = [];
    private int _stageIndex;

    public Campaign(GameModeKind mode, Profile profile, IReadOnlyList<PlayerSlot> players, Difficulty difficulty, int chapter = 0, ChallengeDef? challenge = null, ulong seed = 0)
    {
        Mode = mode;
        Profile = profile;
        Players = players;
        Difficulty = difficulty;
        Challenge = challenge;
        Seed = seed == 0 ? (ulong)Environment.TickCount64 : seed;

        switch (mode)
        {
            case GameModeKind.Story:
                _stages.Add(new StagePlan { Kind = StageKind.Chapter, Chapter = Chapters.Get(chapter) });
                break;
            case GameModeKind.Arcade:
                for (int i = 0; i < Chapters.All.Length; i++)
                {
                    _stages.Add(new StagePlan { Kind = StageKind.Chapter, Chapter = Chapters.Get(i), Scaling = 1f + i * 0.04f });
                }

                break;
            case GameModeKind.Survival:
                _stages.Add(new StagePlan { Kind = StageKind.SurvivalArena, Chapter = Chapters.Get(chapter) });
                break;
            case GameModeKind.BossRush:
                for (int i = 0; i < Bosses.All.Length; i++)
                {
                    _stages.Add(new StagePlan { Kind = StageKind.BossArena, Chapter = Chapters.Get(i), Boss = (BossId)i, Scaling = 0.85f + i * 0.03f });
                }

                break;
            case GameModeKind.Challenge:
                ArgumentNullException.ThrowIfNull(challenge);
                _stages.Add(new StagePlan { Kind = challenge.Stage, Chapter = Chapters.Get(challenge.Chapter), Boss = challenge.Boss, Challenge = challenge });
                break;
        }
    }

    public GameModeKind Mode { get; }
    public Profile Profile { get; }
    public IReadOnlyList<PlayerSlot> Players { get; }
    public Difficulty Difficulty { get; }
    public ChallengeDef? Challenge { get; }
    public ulong Seed { get; }
    public int StageIndex => _stageIndex;
    public int StageCount => _stages.Count;
    public StagePlan CurrentStage => _stages[_stageIndex];
    public bool HasNextStage => _stageIndex + 1 < _stages.Count;
    public int TotalScore { get; private set; }
    public int TotalTicks { get; private set; }

    /// <summary>Progression (levels, skills, gear) applies everywhere except Arcade, which is pure skill.</summary>
    public bool UsesProgression => Mode != GameModeKind.Arcade;

    public SessionConfig BuildConfig()
    {
        var loadouts = new PlayerLoadout[Players.Count];
        for (int i = 0; i < Players.Count; i++)
        {
            var slot = Players[i];
            loadouts[i] = UsesProgression
                ? StatsBuilder.Build(slot.Character, Profile.For(slot.Character), slot.IsAi)
                : PlayerLoadout.Base(slot.Character, slot.IsAi);
        }

        return new SessionConfig
        {
            Mode = Mode,
            Difficulty = Difficulty,
            Players = loadouts,
            Seed = Seed + (ulong)_stageIndex * 7919UL,
            Lives = Mode == GameModeKind.Challenge ? 0 : Mode == GameModeKind.Survival ? 1 : 3,
        };
    }

    public GameSession CreateSession() => new(BuildConfig(), CurrentStage);

    public void Advance() => _stageIndex++;

    /// <summary>Records the outcome of the current stage in the profile and returns what to show the player.</summary>
    public StageResult Complete(GameSession session)
    {
        bool cleared = session.State == SessionState.StageClear;
        var players = new List<PlayerResult>();
        for (int s = 0; s < session.PlayerCount; s++)
        {
            var e = session.PlayerEntity(s);
            if (!session.World.IsAlive(e))
            {
                continue;
            }

            var p = session.World.Get<Player>(e);
            var progress = Profile.For(p.Character);
            int before = progress.Level;
            if (UsesProgression && !p.IsAi)
            {
                progress.Xp += p.Xp;
            }

            players.Add(new PlayerResult
            {
                Slot = s,
                Character = p.Character,
                Score = p.Score,
                KnockOuts = p.KnockOuts,
                BestCombo = p.BestCombo,
                Xp = p.Xp,
                LevelBefore = before,
                LevelAfter = progress.Level,
            });
        }

        var result = new StageResult { Cleared = cleared, Stats = session.Stats, Players = players };
        TotalScore += result.TotalScore;
        TotalTicks += session.Stats.Ticks;
        Profile.TotalKnockOuts += session.Stats.KnockOuts;
        Profile.TotalPlayTicks += session.Stats.Ticks;

        if (cleared)
        {
            ApplyClearRewards(session, result);
        }

        switch (Mode)
        {
            case GameModeKind.Arcade:
                Profile.ArcadeHighScore = Math.Max(Profile.ArcadeHighScore, TotalScore);
                break;
            case GameModeKind.Survival:
                Profile.SurvivalBestWave = Math.Max(Profile.SurvivalBestWave, session.Stats.WavesSurvived);
                break;
            case GameModeKind.BossRush when cleared && !HasNextStage:
                Profile.BossRushBestTicks = Profile.BossRushBestTicks == 0 ? TotalTicks : Math.Min(Profile.BossRushBestTicks, TotalTicks);
                break;
        }

        foreach (var achievement in Achievements.Evaluate(Profile, this, result))
        {
            result.NewAchievements.Add(achievement);
        }

        return result;
    }

    private void ApplyClearRewards(GameSession session, StageResult result)
    {
        var stage = CurrentStage;
        if (Mode == GameModeKind.Challenge && Challenge is not null)
        {
            if (Profile.ChallengesCleared.Add(Challenge.Key))
            {
                result.Unlocks.Add(new LocText($"Tantangan selesai: {Challenge.Name.Id}", $"Challenge cleared: {Challenge.Name.En}"));
            }

            return;
        }

        if (Mode != GameModeKind.Story)
        {
            return;
        }

        int index = stage.Chapter.Index;
        Profile.StoryChaptersCleared.Add(index);
        if (index + 1 < Chapters.All.Length && Profile.StoryChapterUnlocked < index + 1)
        {
            Profile.StoryChapterUnlocked = index + 1;
            var next = Chapters.Get(index + 1);
            result.Unlocks.Add(new LocText($"Bab baru: {next.Name.Id}", $"New chapter: {next.Name.En}"));
        }

        var jurus = stage.Chapter.JurusReward;
        if (!jurus.IsNone && Profile.LearnedJurus.Add(jurus))
        {
            result.Unlocks.Add(new LocText($"Jurus daerah: {jurus.Def.Name.Id}", $"Regional style: {jurus.Def.Name.En}"));
            foreach (var slot in Players)
            {
                var progress = Profile.For(slot.Character);
                if (progress.EquippedJurus.IsNone)
                {
                    progress.EquippedJurus = jurus;
                }
            }
        }

        var drop = Bosses.Get(stage.Chapter.Boss).Drop;
        if (drop != EquipmentId.None && Profile.Inventory.Add(drop))
        {
            var item = Equipment.Get(drop)!;
            result.Unlocks.Add(new LocText($"Perlengkapan: {item.Name.Id}", $"Gear: {item.Name.En}"));
        }

        void UnlockCharacter(CharacterId id)
        {
            if (Profile.UnlockedCharacters.Add(id))
            {
                var c = Characters.Get(id);
                result.Unlocks.Add(new LocText($"Karakter baru: {c.Name.Id}", $"New fighter: {c.Name.En}"));
            }
        }

        if (index >= 2)
        {
            UnlockCharacter(CharacterId.Rani);
        }

        if (index >= 4)
        {
            UnlockCharacter(CharacterId.Ucup);
        }
    }
}
