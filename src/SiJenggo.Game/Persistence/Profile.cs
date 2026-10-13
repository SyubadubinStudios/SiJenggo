using System.Text.Json.Serialization;
using SiJenggo.Game.Data;

namespace SiJenggo.Game.Persistence;

public sealed class CharacterProgress
{
    public int Xp { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<EquipmentId> Equipped { get; set; } = [];
    public MoveId EquippedJurus { get; set; }

    [JsonIgnore]
    public int Level => Leveling.LevelForXp(Xp);

    [JsonIgnore]
    public int SkillPointsSpent => Skills.Sum(k => SkillTree.Find(k)?.Cost ?? 0);

    [JsonIgnore]
    public int SkillPointsAvailable => Leveling.SkillPointsForLevel(Level) - SkillPointsSpent;

    public bool CanUnlock(SkillNode node) =>
        !Skills.Contains(node.Key)
        && SkillPointsAvailable >= node.Cost
        && (node.Tier == 1 || Skills.Contains($"{node.Key[..node.Key.IndexOf('.')]}.{node.Tier - 1}"));

    public bool Unlock(SkillNode node)
    {
        if (!CanUnlock(node))
        {
            return false;
        }

        Skills.Add(node.Key);
        return true;
    }

    public void Equip(EquipmentId id)
    {
        var def = Equipment.Get(id);
        if (def is null)
        {
            return;
        }

        Equipped.RemoveAll(e => Equipment.Get(e)?.Slot == def.Slot);
        Equipped.Add(id);
    }
}

public sealed class GameSettings
{
    public Language Language { get; set; } = Language.Indonesian;
    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 0.9f;
    /// <summary>0 = low, 1 = medium, 2 = high, 3 = ultra.</summary>
    public int GraphicsQuality { get; set; } = 1;
    public bool ScreenShake { get; set; } = true;
    public bool Fullscreen { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public bool ShowFps { get; set; }
}

/// <summary>Everything persisted between sessions. Serialized as JSON by <see cref="SaveService"/>.</summary>
public sealed class Profile
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Jagoan";
    public Dictionary<CharacterId, CharacterProgress> Characters { get; set; } = [];
    /// <summary>Highest story chapter index unlocked (0-based).</summary>
    public int StoryChapterUnlocked { get; set; }
    public HashSet<int> StoryChaptersCleared { get; set; } = [];
    public HashSet<CharacterId> UnlockedCharacters { get; set; } = [CharacterId.Jenggo, CharacterId.Bima];
    public HashSet<MoveId> LearnedJurus { get; set; } = [];
    public HashSet<EquipmentId> Inventory { get; set; } = [EquipmentId.JaketDenim, EquipmentId.SabukKulit, EquipmentId.SarungTanganKain, EquipmentId.KalungTaring];
    public HashSet<string> Achievements { get; set; } = [];
    public HashSet<string> ChallengesCleared { get; set; } = [];
    public int ArcadeHighScore { get; set; }
    public int SurvivalBestWave { get; set; }
    public int BossRushBestTicks { get; set; }
    public long TotalKnockOuts { get; set; }
    public long TotalPlayTicks { get; set; }
    public GameSettings Settings { get; set; } = new();

    public CharacterProgress For(CharacterId id)
    {
        if (!Characters.TryGetValue(id, out var progress))
        {
            progress = new CharacterProgress();
            Characters[id] = progress;
        }

        return progress;
    }

    public bool IsUnlocked(CharacterId id) => UnlockedCharacters.Contains(id);
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Profile))]
internal sealed partial class ProfileJsonContext : JsonSerializerContext;
