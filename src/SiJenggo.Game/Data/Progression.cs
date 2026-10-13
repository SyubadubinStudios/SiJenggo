namespace SiJenggo.Game.Data;

public enum SkillBranch : byte
{
    Power,
    Speed,
    Defense,
    Technique,
    Rage,
}

public sealed class SkillNode
{
    public required string Key { get; init; }
    public required SkillBranch Branch { get; init; }
    public required int Tier { get; init; }
    public required LocText Name { get; init; }
    public required LocText Effect { get; init; }
    public int Cost { get; init; } = 1;
    public float Value { get; init; }
}

/// <summary>Five branches of four tiers. A tier needs the previous tier of the same branch.</summary>
public static class SkillTree
{
    public static readonly SkillNode[] All =
    [
        new() { Key = "power.1", Branch = SkillBranch.Power, Tier = 1, Name = new("Otot Kuli", "Porter Muscles"), Effect = new("+6% kerusakan", "+6% damage"), Value = 0.06f },
        new() { Key = "power.2", Branch = SkillBranch.Power, Tier = 2, Name = new("Tinju Batu", "Stone Fist"), Effect = new("+8% kerusakan", "+8% damage"), Value = 0.08f },
        new() { Key = "power.3", Branch = SkillBranch.Power, Tier = 3, Name = new("Hantaman Palu", "Hammer Blow"), Effect = new("+10% kerusakan", "+10% damage"), Cost = 2, Value = 0.10f },
        new() { Key = "power.4", Branch = SkillBranch.Power, Tier = 4, Name = new("Tenaga Banteng", "Strength of the Bull"), Effect = new("+14% kerusakan", "+14% damage"), Cost = 3, Value = 0.14f },

        new() { Key = "speed.1", Branch = SkillBranch.Speed, Tier = 1, Name = new("Langkah Ringan", "Light Feet"), Effect = new("+6% kecepatan jalan", "+6% walk speed"), Value = 0.06f },
        new() { Key = "speed.2", Branch = SkillBranch.Speed, Tier = 2, Name = new("Tangan Kilat", "Lightning Hands"), Effect = new("+5% kecepatan serang", "+5% attack speed"), Value = 0.05f },
        new() { Key = "speed.3", Branch = SkillBranch.Speed, Tier = 3, Name = new("Angin Ribut", "Gale"), Effect = new("+8% kecepatan jalan", "+8% walk speed"), Cost = 2, Value = 0.08f },
        new() { Key = "speed.4", Branch = SkillBranch.Speed, Tier = 4, Name = new("Kilat Nusantara", "Archipelago Lightning"), Effect = new("+8% kecepatan serang", "+8% attack speed"), Cost = 3, Value = 0.08f },

        new() { Key = "defense.1", Branch = SkillBranch.Defense, Tier = 1, Name = new("Kulit Badak", "Rhino Hide"), Effect = new("-6% kerusakan diterima", "-6% damage taken"), Value = 0.06f },
        new() { Key = "defense.2", Branch = SkillBranch.Defense, Tier = 2, Name = new("Kuda-kuda Kokoh", "Firm Stance"), Effect = new("+25% tahan dorongan", "+25% knockback resistance"), Value = 0.25f },
        new() { Key = "defense.3", Branch = SkillBranch.Defense, Tier = 3, Name = new("Badan Besi", "Iron Body"), Effect = new("+15 darah maksimum", "+15 max health"), Cost = 2, Value = 15f },
        new() { Key = "defense.4", Branch = SkillBranch.Defense, Tier = 4, Name = new("Benteng Kampung", "Village Fortress"), Effect = new("-12% kerusakan diterima", "-12% damage taken"), Cost = 3, Value = 0.12f },

        new() { Key = "technique.1", Branch = SkillBranch.Technique, Tier = 1, Name = new("Mata Elang", "Eagle Eye"), Effect = new("Jendela kombo +20%", "+20% combo window"), Value = 0.2f },
        new() { Key = "technique.2", Branch = SkillBranch.Technique, Tier = 2, Name = new("Tangkisan Halus", "Smooth Parry"), Effect = new("Jendela tangkis +25%", "+25% counter window"), Value = 0.25f },
        new() { Key = "technique.3", Branch = SkillBranch.Technique, Tier = 3, Name = new("Semangat Membara", "Burning Spirit"), Effect = new("Semangat pulih +30%", "+30% spirit regeneration"), Cost = 2, Value = 0.3f },
        new() { Key = "technique.4", Branch = SkillBranch.Technique, Tier = 4, Name = new("Pendekar Sejati", "True Warrior"), Effect = new("Biaya jurus spesial -20%", "-20% special cost"), Cost = 3, Value = 0.2f },

        new() { Key = "rage.1", Branch = SkillBranch.Rage, Tier = 1, Name = new("Darah Panas", "Hot Blood"), Effect = new("Rage terisi +15%", "+15% rage gain"), Value = 0.15f },
        new() { Key = "rage.2", Branch = SkillBranch.Rage, Tier = 2, Name = new("Amarah Panjang", "Lasting Fury"), Effect = new("Durasi Rage +20%", "+20% rage duration"), Value = 0.2f },
        new() { Key = "rage.3", Branch = SkillBranch.Rage, Tier = 3, Name = new("Api Nusantara", "Archipelago Fire"), Effect = new("Rage terisi +20%", "+20% rage gain"), Cost = 2, Value = 0.2f },
        new() { Key = "rage.4", Branch = SkillBranch.Rage, Tier = 4, Name = new("Murka Abadi", "Eternal Wrath"), Effect = new("Durasi Rage +30%", "+30% rage duration"), Cost = 3, Value = 0.3f },
    ];

    public static SkillNode? Find(string key) => Array.Find(All, n => n.Key == key);
}

public enum EquipmentSlot : byte
{
    Jaket,
    Sabuk,
    SarungTangan,
    Kalung,
}

public enum EquipmentId : byte
{
    None,
    JaketDenim,
    SabukKulit,
    SarungTanganKain,
    KalungTaring,
    JaketKulitKodir,
    SabukJangkar,
    KalungMutiara,
    SarungTanganRotan,
    SabukMagma,
    KalungPusakaMajapahit,
    JaketGaruda,
}

public sealed class EquipmentDef
{
    public required EquipmentId Id { get; init; }
    public required EquipmentSlot Slot { get; init; }
    public required LocText Name { get; init; }
    public required LocText Description { get; init; }
    public float Power { get; init; }
    public float Defense { get; init; }
    public float Speed { get; init; }
    public float Health { get; init; }
    public float RageGain { get; init; }
}

public static class Equipment
{
    public static readonly EquipmentDef[] All =
    [
        null!,
        new() { Id = EquipmentId.JaketDenim, Slot = EquipmentSlot.Jaket, Name = new("Jaket Denim Lusuh", "Worn Denim Jacket"), Description = new("Jaket pertama Jenggo dari Pasar Senen.", "Jenggo's first jacket, from Pasar Senen."), Defense = 0.03f, Health = 5 },
        new() { Id = EquipmentId.SabukKulit, Slot = EquipmentSlot.Sabuk, Name = new("Sabuk Kulit", "Leather Belt"), Description = new("Kuat, sederhana, dan bisa buat nyabet.", "Sturdy, simple, and good for a swing."), Power = 0.03f },
        new() { Id = EquipmentId.SarungTanganKain, Slot = EquipmentSlot.SarungTangan, Name = new("Sarung Tangan Kain", "Cloth Wraps"), Description = new("Lilitan kain untuk melindungi buku jari.", "Cloth wraps to protect the knuckles."), Power = 0.02f, Speed = 0.02f },
        new() { Id = EquipmentId.KalungTaring, Slot = EquipmentSlot.Kalung, Name = new("Kalung Taring", "Fang Necklace"), Description = new("Jimat murah dari pinggir jalan.", "A cheap roadside charm."), RageGain = 0.05f },
        new() { Id = EquipmentId.JaketKulitKodir, Slot = EquipmentSlot.Jaket, Name = new("Jaket Kulit Bang Kodir", "Bang Kodir's Leather Jacket"), Description = new("Rampasan dari Raja Preman Jakarta.", "Taken from the King of the Jakarta Thugs."), Defense = 0.08f, Health = 10 },
        new() { Id = EquipmentId.SabukJangkar, Slot = EquipmentSlot.Sabuk, Name = new("Sabuk Jangkar", "Anchor Belt"), Description = new("Gesper berbentuk jangkar milik Kapten Badai.", "Captain Badai's anchor-shaped buckle."), Power = 0.06f, Defense = 0.03f },
        new() { Id = EquipmentId.KalungMutiara, Slot = EquipmentSlot.Kalung, Name = new("Kalung Mutiara Selatan", "Southern Pearl Necklace"), Description = new("Mutiara yang berdenyut seperti ombak.", "A pearl that pulses like the waves."), RageGain = 0.12f, Speed = 0.03f },
        new() { Id = EquipmentId.SarungTanganRotan, Slot = EquipmentSlot.SarungTangan, Name = new("Sarung Tangan Rotan", "Rattan Gauntlets"), Description = new("Anyaman rotan Kalimantan, ringan dan keras.", "Woven Borneo rattan, light and hard."), Power = 0.08f, Speed = 0.03f },
        new() { Id = EquipmentId.SabukMagma, Slot = EquipmentSlot.Sabuk, Name = new("Sabuk Magma", "Magma Belt"), Description = new("Masih hangat dari kawah Merapi.", "Still warm from Merapi's crater."), Power = 0.1f, RageGain = 0.05f },
        new() { Id = EquipmentId.KalungPusakaMajapahit, Slot = EquipmentSlot.Kalung, Name = new("Kalung Pusaka Majapahit", "Majapahit Heirloom Necklace"), Description = new("Titipan Patih Abadi untuk sang ksatria.", "The Eternal Patih's gift to the warrior."), Power = 0.06f, Defense = 0.06f, RageGain = 0.15f },
        new() { Id = EquipmentId.JaketGaruda, Slot = EquipmentSlot.Jaket, Name = new("Jaket Garuda Merah Putih", "Red-and-White Garuda Jacket"), Description = new("Lambang Garuda yang direbut kembali untuk rakyat.", "The Garuda emblem, taken back for the people."), Defense = 0.12f, Power = 0.05f, Health = 20 },
    ];

    public static EquipmentDef? Get(EquipmentId id) => id == EquipmentId.None ? null : All[(int)id];
}

public static class Leveling
{
    public const int MaxLevel = 30;

    /// <summary>Total XP needed to reach <paramref name="level"/> (level 1 needs 0).</summary>
    public static int XpForLevel(int level) => level <= 1 ? 0 : 60 * (level - 1) * (level - 1) + 90 * (level - 1);

    public static int LevelForXp(int xp)
    {
        int level = 1;
        while (level < MaxLevel && xp >= XpForLevel(level + 1))
        {
            level++;
        }

        return level;
    }

    /// <summary>One skill point per level, two every fifth level.</summary>
    public static int SkillPointsForLevel(int level) => level - 1 + (level / 5);
}
