using Microsoft.CodeAnalysis;

namespace DeltaStreamNet;

public record struct PropertyInfo
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public Accessibility Accessibility { get; set; }
    public bool IsStreamFrame { get; set; }
    public string? DeltaTypeName { get; set; }
    public string? KeyFrameTypeName { get; set; }
    public string AttributeBlock { get; set; }
    public string DeltaAttributeBlock { get; set; }
    public bool IsKeyedCollection { get; set; }
    public string? CollectionDeltaTypeName { get; set; }
    public string? JsonMinifiedName { get; set; }
    public bool HasProtoMember { get; set; }
    public bool IsStatic { get; set; }
}
