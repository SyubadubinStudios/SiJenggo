using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>Running totals for one stage; challenges and achievements read these.</summary>
public struct SessionStats
{
    public int Ticks;
    public int KnockOuts;
    public int WeaponKnockOuts;
    public int PropKnockOuts;
    public int ThrowKnockOuts;
    public int MaxCombo;
    public int DamageTaken;
    public int UltimatesUsed;
    public int CountersLanded;
    public int CoopMoves;
    public int JurusUsed;
    public int BossesDefeated;
    public int WavesSurvived;
    public int Hits;
}

public enum ChallengeGoal : byte
{
    KnockOutsInTime,
    WeaponKnockOuts,
    PropKnockOuts,
    ComboLength,
    BossNoDamage,
    BossInTime,
    Counters,
    SurviveWaves,
}

public sealed class ChallengeDef
{
    public required string Key { get; init; }
    public required LocText Name { get; init; }
    public required LocText Description { get; init; }
    public required ChallengeGoal Goal { get; init; }
    public required int Target { get; init; }
    /// <summary>Time limit in ticks; 0 = none.</summary>
    public int TimeLimit { get; init; }
    public required int Chapter { get; init; }
    public StageKind Stage { get; init; } = StageKind.SurvivalArena;
    public BossId? Boss { get; init; }
    public CharacterId? ForcedCharacter { get; init; }

    /// <summary>Returns true when the goal is met, false when it can no longer be met, null while undecided.</summary>
    public bool? Evaluate(in SessionStats s, bool bossDefeated) => Goal switch
    {
        ChallengeGoal.KnockOutsInTime => s.KnockOuts >= Target ? true : TimedOut(s) ? false : null,
        ChallengeGoal.WeaponKnockOuts => s.WeaponKnockOuts >= Target ? true : TimedOut(s) ? false : null,
        ChallengeGoal.PropKnockOuts => s.PropKnockOuts >= Target ? true : TimedOut(s) ? false : null,
        ChallengeGoal.ComboLength => s.MaxCombo >= Target ? true : TimedOut(s) ? false : null,
        ChallengeGoal.Counters => s.CountersLanded >= Target ? true : TimedOut(s) ? false : null,
        ChallengeGoal.SurviveWaves => s.WavesSurvived >= Target ? true : null,
        ChallengeGoal.BossNoDamage => s.DamageTaken > 0 ? false : bossDefeated ? true : null,
        ChallengeGoal.BossInTime => bossDefeated ? true : TimedOut(s) ? false : null,
        _ => null,
    };

    private bool TimedOut(in SessionStats s) => TimeLimit > 0 && s.Ticks >= TimeLimit;
}

public static class Challenges
{
    private const int Sec = 60;

    public static readonly ChallengeDef[] All =
    [
        new() { Key = "ch.keroyokan", Name = new("Keroyokan", "Mobbed"), Description = new("Jatuhkan 25 musuh dalam 90 detik.", "Knock out 25 enemies in 90 seconds."), Goal = ChallengeGoal.KnockOutsInTime, Target = 25, TimeLimit = 90 * Sec, Chapter = 0 },
        new() { Key = "ch.pandai-besi", Name = new("Pandai Besi", "Blacksmith"), Description = new("Jatuhkan 10 musuh dengan senjata tradisional.", "Knock out 10 enemies with traditional weapons."), Goal = ChallengeGoal.WeaponKnockOuts, Target = 10, TimeLimit = 150 * Sec, Chapter = 3 },
        new() { Key = "ch.lempar-kursi", Name = new("Lempar Kursi", "Chair Toss"), Description = new("Jatuhkan 5 musuh dengan properti lingkungan.", "Knock out 5 enemies with environmental props."), Goal = ChallengeGoal.PropKnockOuts, Target = 5, TimeLimit = 120 * Sec, Chapter = 0 },
        new() { Key = "ch.juggle", Name = new("Juggling Pasar Malam", "Night Market Juggle"), Description = new("Buat kombo 20 pukulan.", "Land a 20-hit combo."), Goal = ChallengeGoal.ComboLength, Target = 20, TimeLimit = 120 * Sec, Chapter = 2, ForcedCharacter = CharacterId.Bima },
        new() { Key = "ch.tangkis", Name = new("Tangan Besi", "Iron Guard"), Description = new("Lakukan 6 tangkisan balik.", "Land 6 counter attacks."), Goal = ChallengeGoal.Counters, Target = 6, TimeLimit = 120 * Sec, Chapter = 1 },
        new() { Key = "ch.kodir-bersih", Name = new("Tanpa Lecet", "Not a Scratch"), Description = new("Kalahkan Bang Kodir tanpa terkena serangan.", "Defeat Bang Kodir without taking a hit."), Goal = ChallengeGoal.BossNoDamage, Target = 1, Chapter = 0, Stage = StageKind.BossArena, Boss = BossId.BangKodir },
        new() { Key = "ch.merapi-cepat", Name = new("Lahar Kilat", "Lava Rush"), Description = new("Kalahkan Jagal Merapi dalam 75 detik.", "Defeat the Merapi Butcher within 75 seconds."), Goal = ChallengeGoal.BossInTime, Target = 1, TimeLimit = 75 * Sec, Chapter = 4, Stage = StageKind.BossArena, Boss = BossId.JagalMerapi },
        new() { Key = "ch.bertahan", Name = new("Bertahan di Candi", "Hold the Temple"), Description = new("Bertahan 8 gelombang di Candi Kuno.", "Survive 8 waves at the Ancient Temple."), Goal = ChallengeGoal.SurviveWaves, Target = 8, Chapter = 5 },
    ];

    public static ChallengeDef? Find(string key) => Array.Find(All, c => c.Key == key);
}
