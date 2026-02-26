using System;
using System.Linq;
using ProtoBuf;
using Xunit;

namespace DeltaStreamNet.Tests;

public class ProtobufAttributePropagationTests
{
    [Fact]
    public void KeyFrame_PropagatesProtoMemberToNameProperty()
    {
        var prop = typeof(ProtobufAnnotatedDtoKeyFrame).GetProperty(nameof(ProtobufAnnotatedDtoKeyFrame.Name))!;
        var attr = prop.GetCustomAttributes(typeof(ProtoMemberAttribute), inherit: false)
                       .Cast<ProtoMemberAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal(1, attr!.Tag);
    }

    [Fact]
    public void KeyFrame_PropagatesProtoMemberToScoreProperty()
    {
        var prop = typeof(ProtobufAnnotatedDtoKeyFrame).GetProperty(nameof(ProtobufAnnotatedDtoKeyFrame.Score))!;
        var attr = prop.GetCustomAttributes(typeof(ProtoMemberAttribute), inherit: false)
                       .Cast<ProtoMemberAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal(2, attr!.Tag);
    }

    [Fact]
    public void KeyFrame_PropagatesProtoContractClassAttribute()
    {
        var attr = Attribute.GetCustomAttribute(typeof(ProtobufAnnotatedDtoKeyFrame), typeof(ProtoContractAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void DeltaFrame_PropagatesProtoContractClassAttribute()
    {
        var attr = Attribute.GetCustomAttribute(typeof(ProtobufAnnotatedDtoDeltaFrame), typeof(ProtoContractAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void DeltaFrame_PropagatesProtoMemberToNameProperty()
    {
        var prop = typeof(ProtobufAnnotatedDtoDeltaFrame).GetProperty(nameof(ProtobufAnnotatedDtoDeltaFrame.Name))!;
        var attr = prop.GetCustomAttributes(typeof(ProtoMemberAttribute), inherit: false)
                       .Cast<ProtoMemberAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal(1, attr!.Tag);
    }

    [Fact]
    public void DeltaFrame_PropagatesProtoMemberToScoreProperty()
    {
        var prop = typeof(ProtobufAnnotatedDtoDeltaFrame).GetProperty(nameof(ProtobufAnnotatedDtoDeltaFrame.Score))!;
        var attr = prop.GetCustomAttributes(typeof(ProtoMemberAttribute), inherit: false)
                       .Cast<ProtoMemberAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal(2, attr!.Tag);
    }

    [Fact]
    public void KeyFrame_CanSerializeWithAutoProtobuf()
    {
        // With [ProtoContract] propagated, Serializer.Serialize works without RuntimeTypeModel
        var keyFrame = ProtobufAnnotatedDtoKeyFrame.From(
            new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });

        using var ms = new System.IO.MemoryStream();
        Serializer.Serialize(ms, keyFrame);
        ms.Position = 0;
        var deserialized = Serializer.Deserialize<ProtobufAnnotatedDtoKeyFrame>(ms);

        Assert.Equal("Alice", deserialized.Name);
        Assert.Equal(100, deserialized.Score);
    }

    [Fact]
    public void RoundTrip_ProtobufSerializedKeyFrame_PreservesData()
    {
        var initial = new ProtobufAnnotatedDto { Name = "Bob", Score = 42 };
        var keyFrame = ProtobufAnnotatedDtoKeyFrame.From(initial);

        using var ms = new System.IO.MemoryStream();
        Serializer.Serialize(ms, keyFrame);
        ms.Position = 0;
        var deserialized = Serializer.Deserialize<ProtobufAnnotatedDtoKeyFrame>(ms);

        var dto = deserialized.ToDto();
        Assert.Equal(initial.Name, dto.Name);
        Assert.Equal(initial.Score, dto.Score);
    }
}
