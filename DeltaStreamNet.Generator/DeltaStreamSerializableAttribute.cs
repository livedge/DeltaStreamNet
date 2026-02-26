using System;

namespace DeltaStreamNet;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DeltaStreamSerializableAttribute : Attribute
{
    public DeltaStreamSerializableAttribute(Type type) { }
}
