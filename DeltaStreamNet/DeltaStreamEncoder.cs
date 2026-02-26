namespace DeltaStreamNet;

public class DeltaStreamEncoder<T>
{
    private readonly Guid _uuid;
    private readonly IDeltaFrameGenerator<T> _patchGenerator;

    public KeyFrame<T> MainFrame { get; private set; }

    public DeltaStreamEncoder(T initialValue, IDeltaFrameGenerator<T> patchGenerator)
    {
        _uuid = Guid.NewGuid();
        _patchGenerator = patchGenerator;

        MainFrame = new KeyFrame<T>
        {
            Type = FrameType.Key,
            EncoderUuid = _uuid,
            Version = 0,
            Timestamp = DateTime.UtcNow,
            Value = initialValue
        };
    }

    public DeltaFrame<T> EncodeChanges(T value)
    {
        var patch = _patchGenerator.GeneratePatch(MainFrame.Value, value);

        var deltaFrame = new DeltaFrame<T>
        {
            Type = FrameType.Delta,
            EncoderUuid = _uuid,
            Version = MainFrame.Version + 1,
            Timestamp = DateTime.UtcNow,
            Patch = patch
        };

        MainFrame = new KeyFrame<T>
        {
            Type = FrameType.Key,
            EncoderUuid = _uuid,
            Version = deltaFrame.Version,
            Timestamp = deltaFrame.Timestamp,
            Value = value
        };

        return deltaFrame;
    }
}
