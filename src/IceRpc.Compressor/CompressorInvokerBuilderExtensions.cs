// Copyright (c) ZeroC, Inc. All rights reserved.

using IceRpc.Compressor;
using System.IO.Compression;

namespace IceRpc.Builder;

/// <summary>This class provides extension methods to add the compressor interceptor to an <see cref="IInvokerBuilder" />.
/// </summary>
public static class CompressInvokerBuilderExtensions
{
    /// <summary>Adds a <see cref="CompressorInterceptor" /> to the builder.</summary>
    /// <param name="builder">The builder being configured.</param>
    /// <param name="compressionFormat">The compression format for the compress operation.</param>
    /// <param name="compressionLevel">The compression level for the compress operation.</param>
    /// <returns>The builder being configured.</returns>
    public static IInvokerBuilder UseCompressor(
        this IInvokerBuilder builder,
        CompressionFormat compressionFormat,
        CompressionLevel compressionLevel = CompressionLevel.Fastest) =>
        builder.Use(next => new CompressorInterceptor(next, compressionFormat, compressionLevel));
}