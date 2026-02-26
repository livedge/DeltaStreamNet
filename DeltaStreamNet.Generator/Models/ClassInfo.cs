using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DeltaStreamNet;

public record struct ClassInfo
{
    public string? Namespace { get; set; }
    public string Name { get; set; }
    public Accessibility Accessibility { get; set; }
    public IEnumerable<PropertyInfo> Properties { get; set; }
    public bool HasStreamKey { get; set; }
    public string? StreamKeyPropertyName { get; set; }
    public string? StreamKeyTypeName { get; set; }
    public string? CollectionKeyedDeltaClassName { get; set; }
    public string ClassAttributeBlock { get; set; }
    public bool PropagateAttributes { get; set; }
    public bool MinifyJson { get; set; }

    #region Equality members

    public readonly bool Equals(ClassInfo other)
    {
        return Namespace == other.Namespace && Name == other.Name && Accessibility == other.Accessibility &&
               Properties.SequenceEqual(other.Properties) &&
               HasStreamKey == other.HasStreamKey &&
               StreamKeyPropertyName == other.StreamKeyPropertyName &&
               StreamKeyTypeName == other.StreamKeyTypeName &&
               CollectionKeyedDeltaClassName == other.CollectionKeyedDeltaClassName &&
               ClassAttributeBlock == other.ClassAttributeBlock &&
               PropagateAttributes == other.PropagateAttributes &&
               MinifyJson == other.MinifyJson;
    }

    public readonly override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Namespace != null ? Namespace.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Name.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)Accessibility;
            hashCode = (hashCode * 397) ^
                       Properties.Aggregate(hashCode, (current, property) => current ^ property.GetHashCode());
            hashCode = (hashCode * 397) ^ HasStreamKey.GetHashCode();
            hashCode = (hashCode * 397) ^ (StreamKeyPropertyName != null ? StreamKeyPropertyName.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (StreamKeyTypeName != null ? StreamKeyTypeName.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ ClassAttributeBlock.GetHashCode();
            hashCode = (hashCode * 397) ^ PropagateAttributes.GetHashCode();
            hashCode = (hashCode * 397) ^ MinifyJson.GetHashCode();
            return hashCode;
        }
    }

    #endregion
}
