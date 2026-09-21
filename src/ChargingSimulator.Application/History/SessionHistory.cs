using ChargingSimulator.Application.Abstractions;
using ChargingSimulator.Application.Sessions;

namespace ChargingSimulator.Application.History;

public sealed class SessionHistory(int capacity) : IBoundedHistory<HistoryEntry>
{
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Queue<HistoryEntry> _entries = new();
    private readonly Lock _gate = new();

    public void Add(HistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.Enqueue(entry);
            if (_entries.Count > _capacity)
            {
                _ = _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<HistoryEntry> After(long sequence)
    {
        lock (_gate)
        {
            return [.. _entries.Where(entry => entry.Sequence > sequence)];
        }
    }
}
