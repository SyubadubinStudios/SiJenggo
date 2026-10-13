namespace SiJenggo.Core;

/// <summary>
/// Growable buffer of value-type events. The simulation appends during a tick and the
/// presentation drains it once per frame; the backing array is reused so steady state is allocation-free.
/// </summary>
public sealed class EventQueue<T> where T : struct
{
    private T[] _items;
    private int _count;

    public EventQueue(int capacity = 128) => _items = new T[capacity];

    public int Count => _count;

    public ReadOnlySpan<T> Items => new(_items, 0, _count);

    public void Push(in T item)
    {
        if (_count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[_count++] = item;
    }

    public void Clear() => _count = 0;
}
