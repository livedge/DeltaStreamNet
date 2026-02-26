using System.Collections.Generic;

namespace DeltaStreamNet;

public record struct ContextInfo
{
    public string? Namespace { get; set; }
    public string Name { get; set; }
    public List<DtoTypeInfo> DtoTypes { get; set; }
}
