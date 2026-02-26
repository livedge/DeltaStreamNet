using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;

namespace DeltaStreamNet.Tests;

/// <summary>
/// Simulates a message broker (Kafka topic / WebSocket channel).
/// <c>Publish</c> appends to a persistent log (like Kafka).
/// <c>Deliver</c>/<c>Replay</c> push messages to the consumer channel.
/// This separation lets tests control exactly which messages the consumer sees.
/// </summary>
public class SimulatedTransport
{
    private readonly List<WireMessage> _log = [];
    private readonly Channel<WireMessage> _channel = Channel.CreateUnbounded<WireMessage>();

    /// <summary>The persistent message log (append-only, like a Kafka partition).</summary>
    public IReadOnlyList<WireMessage> Log => _log;

    /// <summary>
    /// Publish a message to the log AND deliver it to the consumer channel.
    /// Simulates normal real-time delivery.
    /// </summary>
    public void Publish(WireMessage message)
    {
        _log.Add(message);
        _channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Append a message to the log WITHOUT delivering it.
    /// Simulates a message the consumer won't receive (e.g., published during a partition).
    /// </summary>
    public void PublishSilent(WireMessage message) => _log.Add(message);

    /// <summary>Drain all available messages from the consumer channel.</summary>
    public List<WireMessage> DrainAll()
    {
        var result = new List<WireMessage>();
        while (_channel.Reader.TryRead(out var msg))
            result.Add(msg);
        return result;
    }

    /// <summary>Replay messages from the log into the consumer channel (like Kafka seek).</summary>
    public void Replay(int fromIndex, int count)
    {
        foreach (var msg in _log.Skip(fromIndex).Take(count))
            _channel.Writer.TryWrite(msg);
    }

    /// <summary>Re-deliver a single log entry (simulates at-least-once duplicate).</summary>
    public void RedeliverAt(int index) =>
        _channel.Writer.TryWrite(_log[index]);

    /// <summary>Deliver log entries in a custom order (simulates reordering).</summary>
    public void DeliverInOrder(params int[] indices)
    {
        foreach (var i in indices)
            _channel.Writer.TryWrite(_log[i]);
    }
}
