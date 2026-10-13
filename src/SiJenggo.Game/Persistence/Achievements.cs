using SiJenggo.Game.Data;

namespace SiJenggo.Game.Persistence;

public sealed class AchievementDef
{
    public required string Key { get; init; }
    public required LocText Name { get; init; }
    public required LocText Description { get; init; }
    public required Func<Profile, Campaign, StageResult, bool> Condition { get; init; }
}

/// <summary>Achievements are checked after every stage and stored in the profile once earned.</summary>
public static class Achievements
{
    public static readonly AchievementDef[] All =
    [
        new() { Key = "turun-gunung", Name = new("Turun Gunung", "Back in Action"), Description = new("Selesaikan Bab 1.", "Clear Chapter 1."), Condition = (p, _, _) => p.StoryChaptersCleared.Contains(0) },
        new() { Key = "raja-jalanan", Name = new("Raja Jalanan Baru", "New King of the Streets"), Description = new("Kalahkan Bang Kodir.", "Defeat Bang Kodir."), Condition = (_, c, r) => r.Cleared && c.CurrentStage.Chapter.Boss == BossId.BangKodir && r.Stats.BossesDefeated > 0 },
        new() { Key = "garuda-tumbang", Name = new("Garuda Tumbang", "The Garuda Falls"), Description = new("Selesaikan Mode Cerita.", "Finish Story Mode."), Condition = (p, _, _) => p.StoryChaptersCleared.Contains(6) },
        new() { Key = "kombo-30", Name = new("Tangan Seribu", "A Thousand Hands"), Description = new("Buat kombo 30 pukulan.", "Land a 30-hit combo."), Condition = (_, _, r) => r.Stats.MaxCombo >= 30 },
        new() { Key = "kombo-60", Name = new("Badai Pukulan", "Storm of Fists"), Description = new("Buat kombo 60 pukulan.", "Land a 60-hit combo."), Condition = (_, _, r) => r.Stats.MaxCombo >= 60 },
        new() { Key = "ko-100", Name = new("Seratus Preman", "A Hundred Thugs"), Description = new("Jatuhkan 100 musuh total.", "Knock out 100 enemies in total."), Condition = (p, _, _) => p.TotalKnockOuts >= 100 },
        new() { Key = "ko-1000", Name = new("Legenda Kampung", "Village Legend"), Description = new("Jatuhkan 1000 musuh total.", "Knock out 1000 enemies in total."), Condition = (p, _, _) => p.TotalKnockOuts >= 1000 },
        new() { Key = "tanpa-lecet", Name = new("Tanpa Lecet", "Untouched"), Description = new("Selesaikan satu bab tanpa terkena serangan.", "Clear a chapter without taking damage."), Condition = (_, c, r) => r.Cleared && c.CurrentStage.Kind == StageKind.Chapter && r.Stats.DamageTaken == 0 },
        new() { Key = "gotong-royong", Name = new("Gotong Royong", "Teamwork"), Description = new("Lakukan 10 jurus co-op dalam satu tahap.", "Perform 10 co-op moves in one stage."), Condition = (_, _, r) => r.Stats.CoopMoves >= 10 },
        new() { Key = "murka", Name = new("Murka Nusantara", "Wrath Unleashed"), Description = new("Gunakan jurus pamungkas 5 kali dalam satu tahap.", "Use your ultimate 5 times in one stage."), Condition = (_, _, r) => r.Stats.UltimatesUsed >= 5 },
        new() { Key = "senjata", Name = new("Ahli Senjata Pusaka", "Heirloom Master"), Description = new("Jatuhkan 20 musuh dengan senjata dalam satu tahap.", "Knock out 20 enemies with weapons in one stage."), Condition = (_, _, r) => r.Stats.WeaponKnockOuts >= 20 },
        new() { Key = "kursi", Name = new("Lempar Kursi Plastik", "Plastic Chair Diplomacy"), Description = new("Jatuhkan 10 musuh dengan properti dalam satu tahap.", "Knock out 10 enemies with props in one stage."), Condition = (_, _, r) => r.Stats.PropKnockOuts >= 10 },
        new() { Key = "bertahan-10", Name = new("Tak Tergoyahkan", "Unshakeable"), Description = new("Bertahan 10 gelombang di Mode Bertahan.", "Survive 10 waves in Survival."), Condition = (p, _, _) => p.SurvivalBestWave >= 10 },
        new() { Key = "rush", Name = new("Pemburu Bos", "Boss Hunter"), Description = new("Selesaikan Boss Rush.", "Finish Boss Rush."), Condition = (_, c, r) => c.Mode == GameModeKind.BossRush && r.Cleared && !c.HasNextStage },
        new() { Key = "empat-sekawan", Name = new("Empat Sekawan", "The Fantastic Four"), Description = new("Buka semua karakter.", "Unlock every fighter."), Condition = (p, _, _) => p.UnlockedCharacters.Count >= Characters.All.Length },
        new() { Key = "semua-jurus", Name = new("Pendekar Nusantara", "Archipelago Master"), Description = new("Pelajari semua jurus daerah.", "Learn every regional style."), Condition = (p, _, _) => p.LearnedJurus.Count >= 6 },
        new() { Key = "tangkis", Name = new("Tembok Silat", "Silat Wall"), Description = new("Lakukan 10 tangkisan balik dalam satu tahap.", "Land 10 counters in one stage."), Condition = (_, _, r) => r.Stats.CountersLanded >= 10 },
    ];

    public static IEnumerable<AchievementDef> Evaluate(Profile profile, Campaign campaign, StageResult result)
    {
        foreach (var a in All)
        {
            if (!profile.Achievements.Contains(a.Key) && a.Condition(profile, campaign, result))
            {
                profile.Achievements.Add(a.Key);
                yield return a;
            }
        }
    }
}
