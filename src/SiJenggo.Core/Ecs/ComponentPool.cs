using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SiJenggo.Core.Ecs;

internal interface IComponentPool
{
    int Count { get; }
    bool Has(int entityId);
    void Remove(int entityId);
    void Clear();

    /// <summary>An empty pool of the same component type (used once per pool to warm up a snapshot world).</summary>
    IComponentPool CreateEmpty(int capacity);

    /// <summary>Makes <paramref name="target"/> (same component type) an exact copy of this pool, reusing its arrays.</summary>
    void CopyTo(IComponentPool target);

    /// <summary>Folds the full pool contents (ids and raw component bytes) into an FNV-style hash.</summary>
    ulong Hash(ulong h);
}

/// <summary>
/// Sparse-set storage for one component type. Components live packed in <see cref="_dense"/>,
/// so iteration is a linear walk over contiguous memory and add/remove are O(1) (swap-back).
/// No allocation happens after the arrays have grown to the world's working size.
/// </summary>
public sealed class ComponentPool<T> : IComponentPool where T : struct
{
    private int[] _sparse;          // entity id -> dense index + 1 (0 = absent)
    private T[] _dense;
    private int[] _denseEntities;   // dense index -> entity id
    private int _count;

    internal ComponentPool(int capacity)
    {
        _sparse = new int[capacity];
        _dense = new T[Math.Max(16, capacity / 4)];
        _denseEntities = new int[_dense.Length];
    }

    public int Count => _count;

    /// <summary>Entity ids in dense order. Valid until the next structural change of this pool.</summary>
    public ReadOnlySpan<int> EntityIds => new(_denseEntities, 0, _count);

    /// <summary>Components in dense order, matching <see cref="EntityIds"/>.</summary>
    public Span<T> Components => new(_dense, 0, _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(int entityId) => (uint)entityId < (uint)_sparse.Length && _sparse[entityId] != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(int entityId) => ref _dense[_sparse[entityId] - 1];

    public ref T Add(int entityId, scoped in T value)
    {
        if (entityId >= _sparse.Length)
        {
            Array.Resize(ref _sparse, Math.Max(entityId + 1, _sparse.Length * 2));
        }

        int slot = _sparse[entityId];
        if (slot != 0)
        {
            _dense[slot - 1] = value;
            return ref _dense[slot - 1];
        }

        if (_count == _dense.Length)
        {
            Array.Resize(ref _dense, _dense.Length * 2);
            Array.Resize(ref _denseEntities, _dense.Length);
        }

        _dense[_count] = value;
        _denseEntities[_count] = entityId;
        _sparse[entityId] = ++_count;
        return ref _dense[_count - 1];
    }

    public void Remove(int entityId)
    {
        if (!Has(entityId))
        {
            return;
        }

        int index = _sparse[entityId] - 1;
        int last = _count - 1;
        if (index != last)
        {
            _dense[index] = _dense[last];
            int movedEntity = _denseEntities[last];
            _denseEntities[index] = movedEntity;
            _sparse[movedEntity] = index + 1;
        }

        _dense[last] = default;
        _sparse[entityId] = 0;
        _count--;
    }

    public void Clear()
    {
        for (int i = 0; i < _count; i++)
        {
            _sparse[_denseEntities[i]] = 0;
        }

        Array.Clear(_dense, 0, _count);
        _count = 0;
    }

    IComponentPool IComponentPool.CreateEmpty(int capacity) => new ComponentPool<T>(capacity);

    void IComponentPool.CopyTo(IComponentPool target)
    {
        var dst = (ComponentPool<T>)target;
        if (dst._sparse.Length < _sparse.Length)
        {
            dst._sparse = new int[_sparse.Length];
        }
        else if (dst._sparse.Length > _sparse.Length)
        {
            Array.Clear(dst._sparse, _sparse.Length, dst._sparse.Length - _sparse.Length);
        }

        Array.Copy(_sparse, dst._sparse, _sparse.Length);

        if (dst._dense.Length < _count)
        {
            dst._dense = new T[_dense.Length];
            dst._denseEntities = new int[_dense.Length];
        }
        else if (dst._count > _count)
        {
            Array.Clear(dst._dense, _count, dst._count - _count);
        }

        Array.Copy(_dense, dst._dense, _count);
        Array.Copy(_denseEntities, dst._denseEntities, _count);
        dst._count = _count;
    }

    ulong IComponentPool.Hash(ulong h)
    {
        h = Mix(h, (ulong)_count);
        for (int i = 0; i < _count; i++)
        {
            h = Mix(h, (ulong)_denseEntities[i]);
            h = Mix(h, (ulong)_sparse[_denseEntities[i]]);
        }

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            foreach (byte b in MemoryMarshal.AsBytes(new ReadOnlySpan<T>(_dense, 0, _count)))
            {
                h = Mix(h, b);
            }
        }

        return h;
    }

    internal static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
}
