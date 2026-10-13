using System.Numerics;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

public enum GameEventKind : byte
{
    Hit,
    Blocked,
    Whiff,
    KnockOut,
    Jump,
    Land,
    RageOn,
    RageOff,
    Ultimate,
    Special,
    NotEnoughSpirit,
    Counter,
    Grab,
    Throw,
    BossIntro,
    BossPhase,
    BossDefeated,
    BossTaunt,
    PropBroken,
    PickupTaken,
    WeaponBroken,
    PlayerDown,
    PlayerRevived,
    PlayerRespawn,
    PlayerOut,
    WaveStart,
    Go,
    StageClear,
    GameOver,
    HazardWarn,
    HazardStrike,
    ComboMilestone,
    /// <summary>Param: <see cref="CoopMove"/>.</summary>
    CoopMove,
    AreaEntered,
    LevelUp,
    ArmorUp,
    Footstep,
    Projectile,
}

public enum CoopMove : byte
{
    DoubleGrab,
    AirLaunchCombo,
    TeamFinisher,
    Revive,
}

/// <summary>
/// Something the presentation should react to (spark, sound, camera shake, HUD toast).
/// The simulation never depends on these, so dropping them is always safe.
/// </summary>
public struct GameEvent
{
    public GameEventKind Kind;
    public Entity Source;
    public Entity Target;
    public Vector3 Position;
    public float Value;
    public int Param;
    public HitFx Fx;
    public MoveId Move;
}
