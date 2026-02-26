namespace DeltaStreamNet;

public interface IDeltaPatch<T>
{
    public T ApplyPatch(T value);
}