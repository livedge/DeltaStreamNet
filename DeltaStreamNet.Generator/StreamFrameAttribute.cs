using System;

namespace DeltaStreamNet;

[AttributeUsage(AttributeTargets.Class)]
public class StreamFrameAttribute : Attribute
{
    public bool PropagateAttributes { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class StreamFieldAttribute : Attribute { }
