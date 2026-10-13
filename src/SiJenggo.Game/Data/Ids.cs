namespace SiJenggo.Game.Data;

public enum CharacterId : byte
{
    Jenggo,
    Bima,
    Rani,
    Ucup,
}

public enum EnemyKind : byte
{
    PremanGang,
    PremanPisau,
    TukangPukul,
    JagoanSilat,
    PelemparBotol,
    NinjaGaruda,
    RobotGaruda,
    BayanganPatih,
}

public enum BossId : byte
{
    BangKodir,
    KaptenBadai,
    NyiAnginLaut,
    RajaPenebang,
    JagalMerapi,
    PatihAbadi,
    JenderalGaruda,
}

public enum WeaponId : byte
{
    None,
    Golok,
    Keris,
    Kujang,
    Celurit,
    Rencong,
    TombakDayak,
    Mandau,
    PipaBesi,
}

public enum PropKind : byte
{
    Kursi,
    Motor,
    Gerobak,
    TongSampah,
    Bambu,
    Pagar,
    PetiKayu,
    BatuBesar,
}

public enum PickupKind : byte
{
    Weapon,
    /// <summary>Full heal.</summary>
    NasiPadang,
    /// <summary>Half heal.</summary>
    Sate,
    /// <summary>Small heal plus semangat.</summary>
    EsTeh,
    /// <summary>Rage boost.</summary>
    KopiTubruk,
    /// <summary>Score.</summary>
    Uang,
}

public enum WeatherKind : byte
{
    Clear,
    NightNeon,
    TropicalRain,
    Thunderstorm,
    Mist,
    VolcanicAsh,
    SandWind,
    OceanSpray,
}

public enum EnvironmentTheme : byte
{
    JakartaKumuh,
    Pelabuhan,
    PantaiSelatan,
    HutanKalimantan,
    GunungBerapi,
    CandiKuno,
    KotaFuturistik,
}

public enum MusicStyle : byte
{
    FunkRock,
    MetalBlues,
    SurfRock,
    TribalPercussion,
    GamelanMetal,
    SymphonicMetal,
}

/// <summary>Visual + audio flavour of a hit, so the presentation picks the right spark and sound.</summary>
public enum HitFx : byte
{
    Punch,
    Kick,
    Heavy,
    Slash,
    Blunt,
    Fire,
    Lightning,
    Wind,
    Shockwave,
    Gadget,
}

/// <summary>Key pose the procedural animator blends towards while a move plays.</summary>
public enum MovePose : byte
{
    Jab,
    Cross,
    Hook,
    Uppercut,
    FrontKick,
    RoundKick,
    LowSweep,
    SpinKick,
    Elbow,
    Knee,
    Headbutt,
    DashPunch,
    AirKick,
    DiveKick,
    GroundPound,
    Slam,
    Throw,
    BackThrow,
    Counter,
    Overhead,
    Thrust,
    Slash,
    WideSlash,
    Spin,
    Gadget,
    Taunt,
    Roar,
    Cast,
    Charge,
}
