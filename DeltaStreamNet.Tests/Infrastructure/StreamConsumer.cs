using System;

namespace DeltaStreamNet.Tests;

/// <summary>
/// A realistic consumer that wraps DeltaStreamDecoder with version-gap detection,
/// duplicate rejection, UUID validation, and recovery signaling — the kind of logic
/// you'd build on top of the codec in a real Kafka/WebSocket consumer.
/// </summary>
public class StreamConsumer<T>
{
    private DeltaStreamDecoder<T>? _decoder;

    public T? CurrentValue { get; private set; }
    public ulong CurrentVersion { get; private set; }
    public Guid? ExpectedUuid { get; private set; }
    public int FramesApplied { get; private set; }
    public int FramesRejected { get; private set; }
    public bool NeedsRecovery { get; private set; }

    public void ApplyFrame(Frame<T> frame)
    {
        // First frame must be a KeyFrame to bootstrap the decoder
        if (_decoder is null)
        {
            if (frame is not KeyFrame<T> kf)
            {
                FramesRejected++;
                NeedsRecovery = true;
                return;
            }

            _decoder = new DeltaStreamDecoder<T>(kf);
            ExpectedUuid = kf.EncoderUuid;
            CurrentValue = kf.Value;
            CurrentVersion = kf.Version;
            FramesApplied++;
            NeedsRecovery = false;
            return;
        }

        // UUID mismatch — wrong producer
        if (frame.EncoderUuid != ExpectedUuid)
        {
            FramesRejected++;
            return;
        }

        // Stale or duplicate version
        if (frame.Version <= CurrentVersion)
        {
            FramesRejected++;
            return;
        }

        // Version gap — delta arrived but we're missing intermediate versions
        if (frame is DeltaFrame<T> && frame.Version != CurrentVersion + 1)
        {
            FramesRejected++;
            NeedsRecovery = true;
            return;
        }

        // All checks passed — apply through the decoder
        // (KeyFrames can jump versions; deltas must be sequential)
        CurrentValue = _decoder.DecodeFrame(frame);
        CurrentVersion = frame.Version;
        FramesApplied++;
        NeedsRecovery = false;
    }

    /// <summary>Reset state to accept a fresh KeyFrame (recovery).</summary>
    public void Reset()
    {
        _decoder = null;
        CurrentValue = default;
        CurrentVersion = 0;
        ExpectedUuid = null;
        NeedsRecovery = false;
    }
}
