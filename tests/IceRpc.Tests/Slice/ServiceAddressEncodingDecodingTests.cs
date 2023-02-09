// Copyright (c) ZeroC, Inc.

using IceRpc.Slice;
using IceRpc.Slice.Internal;
using NUnit.Framework;

namespace IceRpc.Tests.Slice;

/// <summary>Test encoding/decoding of service address with Slice1 and Slice2.</summary>
[Parallelizable(scope: ParallelScope.All)]
public class ServiceAddressEncodingDecodingTests
{
    [TestCase("icerpc://hello.zeroc.com/hello", SliceEncoding.Slice2, null)]
    [TestCase("icerpc://hello.zeroc.com/hello", SliceEncoding.Slice1, null)]
    [TestCase("ice://hello.zeroc.com/hello?transport=tcp#facet", SliceEncoding.Slice1, null)]
    [TestCase("ice://hello.zeroc.com/hello", SliceEncoding.Slice1, "ice://hello.zeroc.com/hello?transport=tcp")]
    [TestCase(
        "ice://hello.zeroc.com/hello?alt-server=[::1]?transport=ssl",
        SliceEncoding.Slice1,
        "ice://hello.zeroc.com/hello?transport=tcp&alt-server=[::1]?transport=ssl")]
    public void Encode_decode_service_address(
        ServiceAddress value,
        SliceEncoding sliceEncoding,
        ServiceAddress? expectedValue)
    {
        var buffer = new byte[256];
        var bufferWriter = new MemoryBufferWriter(buffer);
        var encoder = new SliceEncoder(bufferWriter, sliceEncoding);
        encoder.EncodeServiceAddress(value);
        var decoder = new SliceDecoder(buffer.AsMemory()[0..bufferWriter.WrittenMemory.Length], sliceEncoding);

        // Act/Assert
        Assert.That(decoder.DecodeServiceAddress(), Is.EqualTo(expectedValue ?? value));
    }

    [TestCase(null)]
    [TestCase("icerpc://hello.zeroc.com/hello")]
    [TestCase("ice://hello.zeroc.com/hello?transport=tcp#facet")]
    [TestCase("ice://hello.zeroc.com/hello?transport=tcp&alt-server=[::1]?transport=ssl")]
    public void Encode_decode_nullable_service_address(ServiceAddress? value)
    {
        var buffer = new byte[256];
        var bufferWriter = new MemoryBufferWriter(buffer);
        var encoder = new SliceEncoder(bufferWriter, SliceEncoding.Slice1);
        encoder.EncodeNullableServiceAddress(value);
        var decoder = new SliceDecoder(buffer.AsMemory()[0..bufferWriter.WrittenMemory.Length], SliceEncoding.Slice1);

        // Act/Assert
        Assert.That(decoder.DecodeNullableServiceAddress(), Is.EqualTo(value));
    }
}
