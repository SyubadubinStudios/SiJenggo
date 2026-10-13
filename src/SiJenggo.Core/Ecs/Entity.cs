namespace SiJenggo.Core.Ecs;

/// <summary>
/// A handle to an entity. <see cref="Version"/> is bumped every time the slot is recycled,
/// so stale handles held by systems (targets, grab partners, projectile owners) are detected
/// instead of silently pointing at a new entity.
/// <para><c>default(Entity)</c> is the null handle, so component fields that were never assigned
/// can never accidentally refer to entity 0.</para>
/// </summary>
public readonly record struct Entity
{
    private readonly int _idPlusOne;

    public Entity(int id, int version)
    {
        _idPlusOne = id + 1;
        Version = version;
    }

    public static readonly Entity Null = default;

    public int Id => _idPlusOne - 1;
    public int Version { get; }
    public bool IsNull => _idPlusOne <= 0;

    public override string ToString() => IsNull ? "Entity(null)" : $"Entity({Id}v{Version})";
}
