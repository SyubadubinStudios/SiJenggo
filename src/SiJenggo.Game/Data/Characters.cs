namespace SiJenggo.Game.Data;

public sealed class CharacterDef
{
    public required CharacterId Id { get; init; }
    public required string Key { get; init; }
    public required LocText Name { get; init; }
    public required LocText Tagline { get; init; }
    public required LocText Bio { get; init; }
    public required LocText[] Styles { get; init; }
    public float MaxHealth { get; init; } = 100;
    public float WalkSpeed { get; init; } = 4.2f;
    public float DepthSpeed { get; init; } = 2.8f;
    public float JumpVelocity { get; init; } = 8.5f;
    public float Power { get; init; } = 1f;
    public float Defense { get; init; } = 1f;
    public float AttackSpeed { get; init; } = 1f;
    public float Weight { get; init; } = 1f;
    public float CounterWindow { get; init; } = 1f;
    /// <summary>Stat bars for the character select screen (1..5).</summary>
    public (int Power, int Speed, int Defense, int Technique) Rating { get; init; }
    public required MoveId[] LightChain { get; init; }
    public required MoveId Heavy { get; init; }
    public required MoveId Dash { get; init; }
    public required MoveId AirLight { get; init; }
    public required MoveId AirHeavy { get; init; }
    public required MoveId GrabStrike { get; init; }
    public required MoveId Throw { get; init; }
    public required MoveId Counter { get; init; }
    /// <summary>Neutral, forward and up specials.</summary>
    public required MoveId[] Specials { get; init; }
    public required MoveId Ultimate { get; init; }
    public bool UnlockedByDefault { get; init; } = true;
    public LocText UnlockHint { get; init; }
}

public static class Characters
{
    public static readonly CharacterDef Jenggo = new()
    {
        Id = CharacterId.Jenggo,
        Key = "jenggo",
        Name = new("Si Jenggo", "Si Jenggo"),
        Tagline = new("Preman berhati malaikat", "The thug with an angel's heart"),
        Bio = new(
            "Jenggo Prasetyo, mantan preman legendaris Jakarta. Kampungnya dibakar Garuda Hitam, dan sekarang dia turun ke jalan lagi.",
            "Jenggo Prasetyo, a legendary former Jakarta thug. Garuda Hitam burned his village, and now he is back on the streets."),
        Styles = [new("Petarung kuat", "Power brawler"), new("Pembanting", "Grappler"), new("Pengendali kerumunan", "Crowd control")],
        MaxHealth = 130,
        WalkSpeed = 3.8f,
        DepthSpeed = 2.5f,
        JumpVelocity = 8f,
        Power = 1.2f,
        Defense = 1.15f,
        AttackSpeed = 0.95f,
        Weight = 1.4f,
        Rating = (5, 2, 4, 3),
        LightChain = [Moves.JenggoJab, Moves.JenggoCross, Moves.JenggoHook, Moves.JenggoHeadbutt],
        Heavy = Moves.JenggoHeavy,
        Dash = Moves.JenggoDash,
        AirLight = Moves.JenggoAirLight,
        AirHeavy = Moves.JenggoAirHeavy,
        GrabStrike = Moves.JenggoGrabKnee,
        Throw = Moves.JenggoThrow,
        Counter = Moves.JenggoCounter,
        Specials = [Moves.JenggoSpin, Moves.JenggoMonas, Moves.JenggoPetir],
        Ultimate = Moves.JenggoUltimate,
    };

    public static readonly CharacterDef Bima = new()
    {
        Id = CharacterId.Bima,
        Key = "bima",
        Name = new("Bima", "Bima"),
        Tagline = new("Mantan atlet pencak silat", "Former pencak silat champion"),
        Bio = new(
            "Atlet silat nasional yang dicoret karena menolak mengalah di pertandingan yang diatur Garuda Hitam.",
            "A national silat athlete dropped from the team for refusing to throw a match fixed by Garuda Hitam."),
        Styles = [new("Kombo cepat", "Fast combos"), new("Serangan balik", "Counter attack"), new("Lincah", "Mobility")],
        MaxHealth = 105,
        WalkSpeed = 5f,
        DepthSpeed = 3.3f,
        JumpVelocity = 9.5f,
        Power = 0.9f,
        Defense = 0.9f,
        AttackSpeed = 1.15f,
        Weight = 0.9f,
        CounterWindow = 1.5f,
        Rating = (3, 5, 2, 5),
        LightChain = [Moves.BimaJab, Moves.BimaPalm, Moves.BimaElbow, Moves.BimaKick, Moves.BimaFinisher],
        Heavy = Moves.BimaHeavy,
        Dash = Moves.BimaDash,
        AirLight = Moves.BimaAirLight,
        AirHeavy = Moves.BimaAirHeavy,
        GrabStrike = Moves.BimaGrabKnee,
        Throw = Moves.BimaThrow,
        Counter = Moves.BimaCounter,
        Specials = [Moves.BimaFlurry, Moves.BimaStep, Moves.BimaSweep],
        Ultimate = Moves.BimaUltimate,
    };

    public static readonly CharacterDef Rani = new()
    {
        Id = CharacterId.Rani,
        Key = "rani",
        Name = new("Mbak Rani", "Mbak Rani"),
        Tagline = new("Mantan anggota intel", "Former intelligence agent"),
        Bio = new(
            "Dulu menyamar di dalam Garuda Hitam. Sekarang dia tahu semua rahasia mereka, dan semua titik lemahnya.",
            "She once worked undercover inside Garuda Hitam. Now she knows all their secrets, and every weak point."),
        Styles = [new("Taktis", "Tactical"), new("Jebakan", "Traps"), new("Gadget", "Gadgets")],
        MaxHealth = 110,
        WalkSpeed = 4.5f,
        DepthSpeed = 3f,
        JumpVelocity = 9f,
        Power = 1.05f,
        Defense = 1f,
        AttackSpeed = 1.05f,
        Weight = 1f,
        Rating = (3, 4, 3, 4),
        LightChain = [Moves.RaniJab, Moves.RaniElbow, Moves.RaniKnee, Moves.RaniFinisher],
        Heavy = Moves.RaniHeavy,
        Dash = Moves.RaniDash,
        AirLight = Moves.RaniAirLight,
        AirHeavy = Moves.RaniAirHeavy,
        GrabStrike = Moves.RaniGrabKnee,
        Throw = Moves.RaniThrow,
        Counter = Moves.RaniCounter,
        Specials = [Moves.RaniMine, Moves.RaniTaser, Moves.RaniSmoke],
        Ultimate = Moves.RaniUltimate,
        UnlockedByDefault = false,
        UnlockHint = new("Selesaikan Bab 3 di Mode Cerita", "Clear Chapter 3 in Story Mode"),
    };

    public static readonly CharacterDef Ucup = new()
    {
        Id = CharacterId.Ucup,
        Key = "ucup",
        Name = new("Ustad Ucup", "Ustad Ucup"),
        Tagline = new("Ceramahnya bikin musuh pingsan", "His sermons knock enemies out"),
        Bio = new(
            "Ustad kampung yang sabarnya habis waktu masjidnya mau digusur. Senjatanya apa saja yang ada di dekatnya.",
            "A village preacher whose patience ran out when they tried to bulldoze his mosque. His weapon is whatever is lying around."),
        Styles = [new("Petarung nyeleneh", "Weird fighter"), new("Senjata seadanya", "Improvised weapons")],
        MaxHealth = 110,
        WalkSpeed = 4f,
        DepthSpeed = 2.7f,
        JumpVelocity = 8f,
        Power = 1.05f,
        Defense = 1.05f,
        AttackSpeed = 1f,
        Weight = 1.1f,
        Rating = (4, 3, 4, 2),
        LightChain = [Moves.UcupSandal, Moves.UcupSarung, Moves.UcupTasbih],
        Heavy = Moves.UcupHeavy,
        Dash = Moves.UcupDash,
        AirLight = Moves.UcupAirLight,
        AirHeavy = Moves.UcupAirHeavy,
        GrabStrike = Moves.UcupGrabKnee,
        Throw = Moves.UcupThrow,
        Counter = Moves.UcupCounter,
        Specials = [Moves.UcupTornado, Moves.UcupTeko, Moves.UcupBedug],
        Ultimate = Moves.UcupUltimate,
        UnlockedByDefault = false,
        UnlockHint = new("Selesaikan Bab 5 di Mode Cerita", "Clear Chapter 5 in Story Mode"),
    };

    public static readonly CharacterDef[] All = [Jenggo, Bima, Rani, Ucup];

    public static CharacterDef Get(CharacterId id) => All[(int)id];
}
