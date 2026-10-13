using System.Numerics;
using SiJenggo.Game.Components;

namespace SiJenggo.Game.Data;

/// <summary>Index into <see cref="Moves"/>. Zero means "no move". Serialized as a plain number.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(MoveIdJsonConverter))]
public readonly record struct MoveId(ushort Value)
{
    public static readonly MoveId None = default;
    public bool IsNone => Value == 0;
    public MoveDef Def => Moves.Get(this);
}

[Flags]
public enum MoveFlags : ushort
{
    None = 0,
    Knockdown = 1 << 0,
    Launch = 1 << 1,
    /// <summary>Hitbox is centred on the attacker and hits all around.</summary>
    Area = 1 << 2,
    Grab = 1 << 3,
    Throw = 1 << 4,
    Counter = 1 << 5,
    AirOnly = 1 << 6,
    /// <summary>Attacker cannot be interrupted while the move plays.</summary>
    SuperArmor = 1 << 7,
    Ultimate = 1 << 8,
    /// <summary>Spawns a projectile at the first active tick.</summary>
    Projectile = 1 << 9,
    /// <summary>Hits targets lying on the ground.</summary>
    HitsDowned = 1 << 10,
    Special = 1 << 11,
    /// <summary>Spawns a hazard at the first active tick (traps, lightning, shockwaves).</summary>
    SpawnsHazard = 1 << 12,
}

/// <summary>
/// Frame data for one attack. All timings are simulation ticks at 60 Hz, all distances in metres.
/// Hitbox offset is in the attacker's facing space: X forward, Y up, Z depth.
/// </summary>
public sealed class MoveDef
{
    public MoveId Id { get; internal set; }
    public required string Key { get; init; }
    public required LocText Name { get; init; }
    public MovePose Pose { get; init; } = MovePose.Jab;
    public HitFx Fx { get; init; } = HitFx.Punch;
    public int Startup { get; init; } = 5;
    public int Active { get; init; } = 3;
    public int Recovery { get; init; } = 10;
    public float Damage { get; init; } = 5;
    public Vector3 HitboxOffset { get; init; } = new(0.9f, 1.1f, 0);
    public Vector3 HitboxHalfSize { get; init; } = new(0.55f, 0.45f, 0.55f);
    /// <summary>Knockback in units/s: X away from the attacker, Y upwards.</summary>
    public Vector2 Knockback { get; init; } = new(1.5f, 0);
    public int HitStun { get; init; } = 16;
    public int HitStop { get; init; } = 4;
    public MoveFlags Flags { get; init; }
    /// <summary>Forward velocity the attacker gets during startup/active frames.</summary>
    public float Lunge { get; init; }
    public float Lift { get; init; }
    /// <summary>Hit the same target again every N ticks while active (0 = once).</summary>
    public int RehitInterval { get; init; }
    public float RageGain { get; init; } = 4;
    public float SemangatCost { get; init; }
    /// <summary>Tick after which the next combo input is accepted.</summary>
    public int CancelFrom { get; init; } = -1;
    public int InvulnFrom { get; init; } = -1;
    public int InvulnTo { get; init; } = -1;
    public float ScreenShake { get; init; }
    public ProjectileKind Shot { get; init; }
    public float ProjectileSpeed { get; init; } = 12f;
    public HazardKind Hazard { get; init; }
    public float HazardRadius { get; init; } = 1.5f;

    public int TotalTicks => Startup + Active + Recovery;
    public bool Has(MoveFlags f) => (Flags & f) != 0;
    public int EffectiveCancelFrom => CancelFrom >= 0 ? CancelFrom : Startup + Active;
}

public sealed class MoveIdJsonConverter : System.Text.Json.Serialization.JsonConverter<MoveId>
{
    public override MoveId Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetUInt16());

    public override void Write(System.Text.Json.Utf8JsonWriter writer, MoveId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);

    public override MoveId ReadAsPropertyName(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(ushort.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture));

    public override void WriteAsPropertyName(System.Text.Json.Utf8JsonWriter writer, MoveId value, System.Text.Json.JsonSerializerOptions options) => writer.WritePropertyName(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
