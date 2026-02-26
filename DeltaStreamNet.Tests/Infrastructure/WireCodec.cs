using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaStreamNet.Tests;

/// <summary>
/// Serializes Frame&lt;T&gt; to/from JSON bytes using a tagged envelope.
/// This mirrors what a real system would do: serialize a discriminator + concrete types
/// so the consumer can reconstruct the Frame on the other side.
/// </summary>
public static class WireCodec
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WireMessage EncodeKeyFrame<T>(KeyFrame<T> keyFrame)
    {
        var envelope = new { f = keyFrame.Type, keyFrame.EncoderUuid, keyFrame.Version, keyFrame.Timestamp, keyFrame.Value };
        return new WireMessage(JsonSerializer.SerializeToUtf8Bytes(envelope, WireOptions));
    }

    public static WireMessage EncodeDeltaFrame<T>(DeltaFrame<T> delta, object concretePatch)
    {
        var envelope = new { f = delta.Type, delta.EncoderUuid, delta.Version, delta.Timestamp, Patch = concretePatch };
        return new WireMessage(JsonSerializer.SerializeToUtf8Bytes(envelope, WireOptions));
    }

    public static Frame<T> Decode<T, TDelta>(WireMessage message)
        where TDelta : IDeltaPatch<T>
    {
        using var doc = JsonDocument.Parse(message.Payload);
        var root = doc.RootElement;

        var frameType = (FrameType)root.GetProperty("f").GetByte();
        var uuid = root.GetProperty("EncoderUuid").GetGuid();
        var version = root.GetProperty("Version").GetUInt64();
        var timestamp = root.GetProperty("Timestamp").GetDateTime();

        if (frameType == FrameType.Key)
        {
            var value = root.GetProperty("Value").Deserialize<T>(WireOptions)!;
            return new KeyFrame<T>
            {
                Type = FrameType.Key,
                EncoderUuid = uuid,
                Version = version,
                Timestamp = timestamp,
                Value = value
            };
        }
        else
        {
            var patch = root.GetProperty("Patch").Deserialize<TDelta>(WireOptions)!;
            return new DeltaFrame<T>
            {
                Type = FrameType.Delta,
                EncoderUuid = uuid,
                Version = version,
                Timestamp = timestamp,
                Patch = patch
            };
        }
    }
}
