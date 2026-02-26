namespace DeltaStreamNet;

public interface IDeltaFrameGenerator<T>
{
    public IDeltaPatch<T> GeneratePatch(T previous, T current);
}
