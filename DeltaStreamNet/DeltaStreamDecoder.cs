namespace DeltaStreamNet;

public class DeltaStreamDecoder<T>(KeyFrame<T> keyFrame)
{
    public T? DecodeFrame(Frame<T> frame)
    {
        if (frame.EncoderUuid != KeyFrame.EncoderUuid)
            throw new ArgumentException("Frame encoder UUID does not match the main frame encoder UUID");
        
        if (frame.Version < KeyFrame.Version)
            throw new ArgumentException("Frame version is lower or equal to the main frame version");

        switch (frame)
        {
            case KeyFrame<T> kf:
                KeyFrame = kf;
                break;
            case DeltaFrame<T> df:
            {
                var newValue = df.Patch.ApplyPatch(KeyFrame.Value);

                KeyFrame = KeyFrame with
                {
                    Version = df.Version,
                    Timestamp = df.Timestamp,
                    Value = newValue
                };
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame, null);
        }

        return KeyFrame.Value;
    }
    private KeyFrame<T> KeyFrame { get; set; } = keyFrame;
}