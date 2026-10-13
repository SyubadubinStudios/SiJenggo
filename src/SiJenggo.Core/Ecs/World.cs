using System.Runtime.CompilerServices;

namespace SiJenggo.Core.Ecs;

/// <summary>
/// Entity registry plus one <see cref="ComponentPool{T}"/> per component type.
/// Destruction is deferred to <see cref="Flush"/> so systems can destroy entities while iterating.
/// </summary>
public sealed class World
{
    private static int s_nextTypeId;

    private static class TypeId<T> where T : struct
    {
        public static readonly int Value = Interlocked.Increment(ref s_nextTypeId) - 1;
    }

    private IComponentPool?[] _pools = new IComponentPool?[32];
    private int[] _versions;
    private bool[] _alive;
    // Recycled ids, used as a LIFO stack. A plain array (not Stack<int>) so snapshots copy it without allocating.
    private int[] _free;
    private int _freeCount;
    private readonly List<int> _pendingDestroy = new(64);
    private int _nextId;
    private readonly int _capacity;

    public World(int capacity = 1024)
    {
        _capacity = capacity;
        _versions = new int[capacity];
        _alive = new bool[capacity];
        _free = new int[capacity];
    }

    public int AliveCount { get; private set; }

    /// <summary>Highest id ever handed out; useful for sizing per-entity side tables.</summary>
    public int IdCapacity => _versions.Length;

    public Entity Create()
    {
        int id;
        if (_freeCount > 0)
        {
            id = _free[--_freeCount];
        }
        else
        {
            id = _nextId++;
            if (id >= _versions.Length)
            {
                Array.Resize(ref _versions, _versions.Length * 2);
                Array.Resize(ref _alive, _versions.Length);
            }
        }

        _alive[id] = true;
        AliveCount++;
        return new Entity(id, _versions[id]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(Entity e) => !e.IsNull && (uint)e.Id < (uint)_alive.Length && _alive[e.Id] && _versions[e.Id] == e.Version;

    /// <summary>Returns the current handle for a raw id coming from a pool iteration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity Handle(int id) => new(id, _versions[id]);

    /// <summary>Marks the entity for destruction at the next <see cref="Flush"/>.</summary>
    public void Destroy(Entity e)
    {
        if (IsAlive(e) && !_pendingDestroy.Contains(e.Id))
        {
            _pendingDestroy.Add(e.Id);
        }
    }

    public void Flush()
    {
        foreach (int id in _pendingDestroy)
        {
            foreach (var pool in _pools)
            {
                pool?.Remove(id);
            }

            _alive[id] = false;
            _versions[id]++;
            PushFree(id);
            AliveCount--;
        }

        _pendingDestroy.Clear();
    }

    public void Clear()
    {
        foreach (var pool in _pools)
        {
            pool?.Clear();
        }

        for (int i = 0; i < _nextId; i++)
        {
            if (_alive[i])
            {
                _alive[i] = false;
                _versions[i]++;
            }
        }

        _freeCount = 0;
        for (int i = _nextId - 1; i >= 0; i--)
        {
            PushFree(i);
        }

        _pendingDestroy.Clear();
        AliveCount = 0;
    }

    private void PushFree(int id)
    {
        if (_freeCount == _free.Length)
        {
            Array.Resize(ref _free, Math.Max(16, _free.Length * 2));
        }

        _free[_freeCount++] = id;
    }

    /// <summary>
    /// Makes this world an exact copy of <paramref name="source"/>: entity versions, alive flags, the free-id
    /// stack, pending destroys and every component pool. Used for rollback snapshots in both directions
    /// (live → snapshot and snapshot → live). Arrays are reused, so after the first copy (warm-up) it does not
    /// allocate unless the source grew. Handles stay valid because ids and versions are copied verbatim.
    /// </summary>
    public void CopyFrom(World source)
    {
        if (ReferenceEquals(source, this))
        {
            return;
        }

        CopyArray(source._versions, ref _versions);
        CopyArray(source._alive, ref _alive);
        if (_free.Length < source._freeCount)
        {
            _free = new int[source._free.Length];
        }

        Array.Copy(source._free, _free, source._freeCount);
        _freeCount = source._freeCount;

        _pendingDestroy.Clear();
        for (int i = 0; i < source._pendingDestroy.Count; i++)
        {
            _pendingDestroy.Add(source._pendingDestroy[i]);
        }

        _nextId = source._nextId;
        AliveCount = source.AliveCount;

        if (_pools.Length < source._pools.Length)
        {
            Array.Resize(ref _pools, source._pools.Length);
        }

        for (int i = 0; i < _pools.Length; i++)
        {
            var src = i < source._pools.Length ? source._pools[i] : null;
            if (src is null)
            {
                _pools[i]?.Clear();
                continue;
            }

            src.CopyTo(_pools[i] ??= src.CreateEmpty(_capacity));
        }

        static void CopyArray<TItem>(TItem[] src, ref TItem[] dst)
        {
            if (dst.Length < src.Length)
            {
                dst = new TItem[src.Length];
            }
            else if (dst.Length > src.Length)
            {
                Array.Clear(dst, src.Length, dst.Length - src.Length);
            }

            Array.Copy(src, dst, src.Length);
        }
    }

    /// <summary>
    /// 64-bit hash of the complete world state (registry plus raw component bytes of every pool).
    /// Much stricter than a gameplay checksum; meant for tests and desync debugging, not per tick.
    /// </summary>
    public ulong ComputeStateHash()
    {
        ulong h = 14695981039346656037UL;
        h = ComponentPool<int>.Mix(h, (ulong)_nextId);
        h = ComponentPool<int>.Mix(h, (ulong)AliveCount);
        for (int i = 0; i < _nextId; i++)
        {
            h = ComponentPool<int>.Mix(h, (ulong)_versions[i]);
            h = ComponentPool<int>.Mix(h, _alive[i] ? 1UL : 0UL);
        }

        for (int i = 0; i < _freeCount; i++)
        {
            h = ComponentPool<int>.Mix(h, (ulong)_free[i]);
        }

        for (int i = 0; i < _pendingDestroy.Count; i++)
        {
            h = ComponentPool<int>.Mix(h, (ulong)_pendingDestroy[i]);
        }

        for (int i = 0; i < _pools.Length; i++)
        {
            // Absent and empty pools hash the same, so a snapshot world that warmed up extra pools still matches.
            if (_pools[i] is { Count: > 0 } pool)
            {
                h = ComponentPool<int>.Mix(h, (ulong)i);
                h = pool.Hash(h);
            }
        }

        return h;
    }

    public ComponentPool<T> Pool<T>() where T : struct
    {
        int id = TypeId<T>.Value;
        if (id >= _pools.Length)
        {
            Array.Resize(ref _pools, Math.Max(id + 1, _pools.Length * 2));
        }

        return (ComponentPool<T>)(_pools[id] ??= new ComponentPool<T>(_capacity));
    }

    public ref T Add<T>(Entity e, scoped in T component) where T : struct => ref Pool<T>().Add(e.Id, component);

    public ref T Add<T>(Entity e) where T : struct => ref Pool<T>().Add(e.Id, default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>(Entity e) where T : struct => ref Pool<T>().Get(e.Id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>(Entity e) where T : struct => IsAlive(e) && Pool<T>().Has(e.Id);

    public void Remove<T>(Entity e) where T : struct => Pool<T>().Remove(e.Id);

    /// <summary>Returns a ref to the component or a ref to a scratch default when missing.</summary>
    public ref T TryGet<T>(Entity e, out bool found) where T : struct
    {
        var pool = Pool<T>();
        found = IsAlive(e) && pool.Has(e.Id);
        if (found)
        {
            return ref pool.Get(e.Id);
        }

        return ref Scratch<T>.Value;
    }

    private static class Scratch<T> where T : struct
    {
        [ThreadStatic]
        private static T s_value;
        public static ref T Value
        {
            get
            {
                s_value = default;
                return ref s_value;
            }
        }
    }

    /// <summary>Iterates entities that have both components, driving the walk from the smaller pool.</summary>
    public Query<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct => new(this, Pool<T1>(), Pool<T2>());
}

/// <summary>Allocation-free two-component join. Iterate it with <c>foreach</c>; ids are snapshot-safe for removals of the current entity only.</summary>
public readonly ref struct Query<T1, T2> where T1 : struct where T2 : struct
{
    private readonly World _world;
    private readonly ComponentPool<T1> _a;
    private readonly ComponentPool<T2> _b;

    internal Query(World world, ComponentPool<T1> a, ComponentPool<T2> b)
    {
        _world = world;
        _a = a;
        _b = b;
    }

    public Enumerator GetEnumerator() => new(_a, _b, _world);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _a;
        private readonly ComponentPool<T2> _b;
        private readonly World _world;
        private readonly bool _driveA;
        private int _index;

        internal Enumerator(ComponentPool<T1> a, ComponentPool<T2> b, World world)
        {
            _a = a;
            _b = b;
            _world = world;
            _driveA = a.Count <= b.Count;
            _index = (_driveA ? a.Count : b.Count);
        }

        public Entity Current { get; private set; }

        public bool MoveNext()
        {
            // Walk backwards so removing the current entity from either pool does not skip anything.
            while (--_index >= 0)
            {
                int id = _driveA ? _a.EntityIds[_index] : _b.EntityIds[_index];
                if (_driveA ? _b.Has(id) : _a.Has(id))
                {
                    Current = _world.Handle(id);
                    return true;
                }
            }

            return false;
        }
    }
}
