namespace SiJenggo.Game.Data;

/// <summary>Arena-wide behaviour a boss phase adds on top of its attack pattern.</summary>
public enum BossMechanic : byte
{
    None,
    SummonMinions,
    LightningStorm,
    FallingRocks,
    LavaEruption,
    FallingTrees,
    CrateDrop,
    ShadowClones,
    /// <summary>Boss becomes invulnerable for a while; the shield flickers and breaks on a timer.</summary>
    MysticShield,
    AirStrike,
    WildBoars,
}

public readonly record struct BossAttack(MoveId Move, float Weight, float MinRange, float MaxRange);

public sealed class BossPhase
{
    public required BossAttack[] Attacks { get; init; }
    public BossMechanic Mechanic { get; init; }
    /// <summary>Ticks between mechanic triggers.</summary>
    public int MechanicInterval { get; init; } = 600;
    public float SpeedMultiplier { get; init; } = 1f;
    public int AttackCooldown { get; init; } = 60;
    public EnemyKind MinionKind { get; init; } = EnemyKind.PremanGang;
    public LocText Taunt { get; init; }
}

public sealed class BossDef
{
    public required BossId Id { get; init; }
    public required LocText Name { get; init; }
    public required LocText Title { get; init; }
    public required LocText IntroLine { get; init; }
    public required LocText DefeatLine { get; init; }
    public float Health { get; init; } = 600;
    public float WalkSpeed { get; init; } = 2.8f;
    public float Power { get; init; } = 1f;
    public float Weight { get; init; } = 3f;
    public float Scale { get; init; } = 1.45f;
    public float Armor { get; init; } = 4f;
    /// <summary>Exactly three phases; transitions at 66% and 33% health.</summary>
    public required BossPhase[] Phases { get; init; }
    public EquipmentId Drop { get; init; }
    public int Score { get; init; } = 5000;
    public int Xp { get; init; } = 200;
}

public static class Bosses
{
    public static readonly BossDef[] All =
    [
        new()
        {
            Id = BossId.BangKodir,
            Name = new("Bang Kodir", "Bang Kodir"),
            Title = new("Raja Preman Jakarta", "King of the Jakarta Thugs"),
            IntroLine = new("Jenggo! Lu pikir lu bisa pensiun? Jalanan ini punya gua sekarang!", "Jenggo! You think you can just retire? These streets are mine now!"),
            DefeatLine = new("Ampun, Bang... Garuda Hitam yang nyuruh gua...", "Mercy, man... Garuda Hitam put me up to it..."),
            Health = 520,
            Power = 1f,
            Drop = EquipmentId.JaketKulitKodir,
            Phases =
            [
                new() { Attacks = [new(Moves.KodirPunch, 3, 0, 2.2f), new(Moves.KodirCharge, 2, 3f, 12f)], AttackCooldown = 70 },
                new() { Attacks = [new(Moves.KodirPunch, 2, 0, 2.2f), new(Moves.KodirCharge, 2, 3f, 12f), new(Moves.KodirStomp, 2, 0, 3f)], Mechanic = BossMechanic.SummonMinions, MechanicInterval = 720, MinionKind = EnemyKind.PremanGang, AttackCooldown = 55, Taunt = new("Anak-anak! Keroyok dia!", "Boys! Get him!") },
                new() { Attacks = [new(Moves.KodirPunch, 2, 0, 2.2f), new(Moves.KodirCharge, 3, 3f, 12f), new(Moves.KodirStomp, 3, 0, 3.5f)], Mechanic = BossMechanic.SummonMinions, MechanicInterval = 600, MinionKind = EnemyKind.PremanPisau, SpeedMultiplier = 1.25f, AttackCooldown = 40, Taunt = new("Gua belum habis!", "I'm not done yet!") },
            ],
        },
        new()
        {
            Id = BossId.KaptenBadai,
            Name = new("Kapten Badai", "Captain Badai"),
            Title = new("Penguasa Pelabuhan Tanjung Harapan", "Master of Tanjung Harapan Harbour"),
            IntroLine = new("Kontainer-kontainer ini isinya rahasia. Dan lu nggak bakal keluar hidup-hidup buat cerita.", "These containers hold secrets. And you won't leave alive to tell anyone."),
            DefeatLine = new("Ombak... sudah berbalik...", "The tide... has turned..."),
            Health = 640,
            Power = 1.05f,
            Drop = EquipmentId.SabukJangkar,
            Phases =
            [
                new() { Attacks = [new(Moves.BadaiKick, 3, 0, 2f), new(Moves.BadaiAnchor, 2, 0, 3.2f), new(Moves.BadaiHarpoon, 2, 4f, 14f)], AttackCooldown = 65 },
                new() { Attacks = [new(Moves.BadaiAnchor, 3, 0, 3.2f), new(Moves.BadaiHarpoon, 3, 3f, 14f)], Mechanic = BossMechanic.CrateDrop, MechanicInterval = 420, AttackCooldown = 50, Taunt = new("Turunkan muatannya!", "Drop the cargo!") },
                new() { Attacks = [new(Moves.BadaiAnchor, 3, 0, 3.2f), new(Moves.BadaiHarpoon, 3, 3f, 14f), new(Moves.BadaiKick, 2, 0, 2f)], Mechanic = BossMechanic.CrateDrop, MechanicInterval = 300, SpeedMultiplier = 1.2f, AttackCooldown = 38, Taunt = new("Badai belum reda!", "The storm isn't over!") },
            ],
        },
        new()
        {
            Id = BossId.NyiAnginLaut,
            Name = new("Nyi Angin Laut", "Nyi Angin Laut"),
            Title = new("Penyihir Pantai Selatan", "Witch of the Southern Shore"),
            IntroLine = new("Laut selatan tidak suka tamu yang berisik.", "The southern sea does not welcome noisy guests."),
            DefeatLine = new("Angin... meninggalkanku...", "The wind... abandons me..."),
            Health = 560,
            WalkSpeed = 3.4f,
            Weight = 1.8f,
            Scale = 1.3f,
            Armor = 3f,
            Drop = EquipmentId.KalungMutiara,
            Phases =
            [
                new() { Attacks = [new(Moves.NyiWhip, 3, 0, 3.6f), new(Moves.NyiVortex, 0.5f, 0, 3f)], Mechanic = BossMechanic.LightningStorm, MechanicInterval = 600, AttackCooldown = 60 },
                new() { Attacks = [new(Moves.NyiWhip, 2, 0, 3.6f), new(Moves.NyiStorm, 1, 2f, 14f), new(Moves.NyiVortex, 0.75f, 0, 3f)], Mechanic = BossMechanic.LightningStorm, MechanicInterval = 450, AttackCooldown = 50, Taunt = new("Rasakan murka langit!", "Feel the fury of the sky!") },
                new() { Attacks = [new(Moves.NyiWhip, 2, 0, 3.6f), new(Moves.NyiStorm, 2, 2f, 14f), new(Moves.NyiVortex, 1, 0, 3.2f)], Mechanic = BossMechanic.LightningStorm, MechanicInterval = 330, SpeedMultiplier = 1.25f, AttackCooldown = 36, Taunt = new("Ombak, telan mereka!", "Waves, swallow them!") },
            ],
        },
        new()
        {
            Id = BossId.RajaPenebang,
            Name = new("Raja Penebang Ilegal", "The Illegal Logging King"),
            Title = new("Perusak Hutan Kalimantan", "Destroyer of the Borneo Forest"),
            IntroLine = new("Setiap pohon di sini ada harganya. Termasuk nyawamu.", "Every tree here has a price. Your life included."),
            DefeatLine = new("Hutan... membalas...", "The forest... strikes back..."),
            Health = 720,
            Power = 1.05f,
            Scale = 1.55f,
            Drop = EquipmentId.SarungTanganRotan,
            Phases =
            [
                new() { Attacks = [new(Moves.PenebangAxe, 2, 0, 2.6f), new(Moves.PenebangSaw, 1, 1.8f, 5f)], AttackCooldown = 75 },
                new() { Attacks = [new(Moves.PenebangAxe, 2, 0, 2.6f), new(Moves.PenebangSaw, 1, 1.8f, 6f), new(Moves.PenebangTimber, 2, 3f, 14f)], Mechanic = BossMechanic.WildBoars, MechanicInterval = 600, AttackCooldown = 60, Taunt = new("Tebang semuanya!", "Cut it all down!") },
                new() { Attacks = [new(Moves.PenebangAxe, 2, 0, 2.6f), new(Moves.PenebangSaw, 2, 1.8f, 6f), new(Moves.PenebangTimber, 3, 2f, 14f)], Mechanic = BossMechanic.FallingTrees, MechanicInterval = 420, SpeedMultiplier = 1.2f, AttackCooldown = 46, Taunt = new("Gergajiku belum tumpul!", "My saw isn't dull yet!") },
            ],
        },
        new()
        {
            Id = BossId.JagalMerapi,
            Name = new("Jagal Merapi", "The Merapi Butcher"),
            Title = new("Algojo Gunung Berapi", "Executioner of the Volcano"),
            IntroLine = new("Di sini panasnya bisa melelehkan besi. Kamu cuma daging.", "The heat here melts iron. You're just meat."),
            DefeatLine = new("Lahar... dingin...", "The lava... runs cold..."),
            Health = 620,
            Power = 1.05f,
            Scale = 1.6f,
            Armor = 5f,
            Drop = EquipmentId.SabukMagma,
            Phases =
            [
                new() { Attacks = [new(Moves.MerapiHammer, 2, 0, 3f), new(Moves.MerapiBoulder, 2, 4f, 14f)], Mechanic = BossMechanic.FallingRocks, MechanicInterval = 720, AttackCooldown = 100 },
                new() { Attacks = [new(Moves.MerapiHammer, 2, 0, 3f), new(Moves.MerapiBoulder, 2, 4f, 14f), new(Moves.MerapiCharge, 2, 0, 12f)], Mechanic = BossMechanic.LavaEruption, MechanicInterval = 660, AttackCooldown = 80, Taunt = new("Gunung ini bangun!", "The mountain wakes!") },
                new() { Attacks = [new(Moves.MerapiHammer, 3, 0, 3f), new(Moves.MerapiBoulder, 2, 4f, 14f), new(Moves.MerapiCharge, 3, 0, 12f)], Mechanic = BossMechanic.FallingRocks, MechanicInterval = 420, SpeedMultiplier = 1.2f, AttackCooldown = 65, Taunt = new("Terbakar kalian semua!", "Burn, all of you!") },
            ],
        },
        new()
        {
            Id = BossId.PatihAbadi,
            Name = new("Patih Abadi", "The Eternal Patih"),
            Title = new("Penjaga Candi Kuno", "Guardian of the Ancient Temple"),
            IntroLine = new("Seribu tahun aku menjaga candi ini. Garuda Hitam membangunkanku. Kau akan menidurkanku?", "A thousand years I have guarded this temple. Garuda Hitam woke me. Will you put me back to sleep?"),
            DefeatLine = new("Akhirnya... aku boleh beristirahat. Pergilah, ksatria.", "At last... I may rest. Go, warrior."),
            Health = 940,
            WalkSpeed = 3.2f,
            Power = 1.2f,
            Scale = 1.5f,
            Drop = EquipmentId.KalungPusakaMajapahit,
            Phases =
            [
                new() { Attacks = [new(Moves.PatihSlash, 3, 0, 2.8f), new(Moves.PatihThrust, 2, 2f, 9f)], AttackCooldown = 60 },
                new() { Attacks = [new(Moves.PatihSlash, 2, 0, 2.8f), new(Moves.PatihThrust, 2, 2f, 9f), new(Moves.PatihWave, 2, 0, 4f)], Mechanic = BossMechanic.ShadowClones, MechanicInterval = 600, MinionKind = EnemyKind.BayanganPatih, AttackCooldown = 50, Taunt = new("Bayanganku, bangkitlah!", "My shadows, rise!") },
                new() { Attacks = [new(Moves.PatihSlash, 2, 0, 2.8f), new(Moves.PatihThrust, 3, 2f, 9f), new(Moves.PatihWave, 3, 0, 4f)], Mechanic = BossMechanic.MysticShield, MechanicInterval = 540, SpeedMultiplier = 1.25f, AttackCooldown = 36, Taunt = new("Pusaka ini tidak akan jatuh!", "This heirloom will not fall!") },
            ],
        },
        new()
        {
            Id = BossId.JenderalGaruda,
            Name = new("Jenderal Garuda Hitam", "General Garuda Hitam"),
            Title = new("Pemimpin Garuda Hitam", "Leader of Garuda Hitam"),
            IntroLine = new("Preman kampung sampai juga di puncak menaraku. Mari kita lihat, siapa yang benar-benar menguasai negeri ini.", "A village thug made it to the top of my tower. Let's see who truly rules this country."),
            DefeatLine = new("Tidak mungkin... dikalahkan... rakyat jelata...", "Impossible... beaten... by common folk..."),
            Health = 900,
            WalkSpeed = 3.4f,
            Power = 1.25f,
            Scale = 1.5f,
            Armor = 5f,
            Drop = EquipmentId.JaketGaruda,
            Score = 10000,
            Xp = 400,
            Phases =
            [
                new() { Attacks = [new(Moves.GarudaPunch, 3, 0, 2.4f), new(Moves.GarudaLaser, 2, 4f, 15f)], Mechanic = BossMechanic.SummonMinions, MechanicInterval = 720, MinionKind = EnemyKind.NinjaGaruda, AttackCooldown = 55 },
                new() { Attacks = [new(Moves.GarudaPunch, 2, 0, 2.4f), new(Moves.GarudaLaser, 2, 4f, 15f), new(Moves.GarudaStrike, 1, 2f, 15f)], Mechanic = BossMechanic.AirStrike, MechanicInterval = 600, AttackCooldown = 45, Taunt = new("Pasukan udara, serang!", "Air squadron, attack!") },
                new() { Attacks = [new(Moves.GarudaPunch, 2, 0, 2.4f), new(Moves.GarudaDive, 3, 2f, 12f), new(Moves.GarudaLaser, 2, 4f, 15f), new(Moves.GarudaStrike, 1, 2f, 15f)], Mechanic = BossMechanic.SummonMinions, MechanicInterval = 600, MinionKind = EnemyKind.RobotGaruda, SpeedMultiplier = 1.3f, AttackCooldown = 32, Taunt = new("Sayap Garuda takkan patah!", "The Garuda's wings will never break!") },
            ],
        },
    ];

    public static BossDef Get(BossId id) => All[(int)id];
}
