namespace DeltaStreamNet.Benchmarks;

internal static class BenchmarkState
{
    public static readonly BenchmarkDto V1 = new()
        { Name = "Alice", Score = 100, Price = 9.99,  IsActive = true,  Id = 1 };

    public static readonly BenchmarkDto V2AllChanged = new()
        { Name = "Bob",   Score = 200, Price = 19.99, IsActive = false, Id = 2 };

    public static readonly BenchmarkDto V2OneChanged = new()
        { Name = "Alice", Score = 200, Price = 9.99,  IsActive = true,  Id = 1 };

    public static readonly BenchmarkDto V2NoneChanged = new()
        { Name = "Alice", Score = 100, Price = 9.99,  IsActive = true,  Id = 1 };
}
