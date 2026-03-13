namespace DeltaStreamNet;

public record struct DtoTypeInfo
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string GeneratorFullName { get; set; }
    public string DeltaFullName { get; set; }
    public bool HasProtobuf { get; set; }
}
