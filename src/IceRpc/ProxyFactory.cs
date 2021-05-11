// Copyright (c) ZeroC, Inc. All rights reserved.

using IceRpc.Internal;
using IceRpc.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace IceRpc
{
    public static class ProxyFactory
    {
        /// <summary>Creates a proxy from a connection.</summary>
        /// <paramtype name="T">The type of the new service proxy.</paramtype>
        /// <param name="factory">This proxy factory. Use INamePrx.Factory for this parameter, where INamePrx is the
        /// proxy type.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="path">The path of the service.</param>
        /// <returns>The new proxy.</returns>
        public static T Create<T>(this IProxyFactory<T> factory, Connection connection, string path)
            where T : class, IServicePrx
        {
            return factory.Create(path,
                                  connection.Protocol,
                                  connection.Protocol.GetEncoding(),
                                  connection.IsIncoming ? null : connection.RemoteEndpoint,
                                  ImmutableList<Endpoint>.Empty,
                                  connection,
                                  connection.Server?.Invoker);
        }

        /// <summary>Creates a proxy from a string and invoker.</summary>
        public static T Parse<T>(this IProxyFactory<T> factory, string s, IInvoker invoker)
            where T : class, IServicePrx
        {
            string proxyString = s.Trim();
            if (proxyString.Length == 0)
            {
                throw new FormatException("an empty string does not represent a proxy");
            }

            if (Internal.UriParser.IsProxyUri(proxyString))
            {
                var args = Internal.UriParser.ParseProxy(proxyString);
                Protocol protocol = args.Endpoint?.Protocol ?? Protocol.Ice2;
                return factory.Create(args.Path,
                                      protocol,
                                      args.Encoding,
                                      args.Endpoint,
                                      args.AltEndpoints,
                                      connection: null,
                                      invoker);
            }
            else
            {
                var args = Ice1Parser.ParseProxy(proxyString);
                return factory.Create(args.Identity,
                                      args.Facet,
                                      args.Encoding,
                                      args.Endpoint,
                                      args.AltEndpoints,
                                      connection: null,
                                      invoker);
            }
        }

        /// <summary>Reads a proxy from the input stream.</summary>
        /// <paramtype name="T">The type of the new service proxy.</paramtype>
        /// <param name="factory">This proxy factory. Use INamePrx.Factory for this parameter, where INamePrx is the
        /// proxy type.</param>
        /// <param name="istr">The input stream to read from.</param>
        /// <returns>The non-null proxy read from the stream.</returns>
        public static T Read<T>(this IProxyFactory<T> factory, InputStream istr)
            where T : class, IServicePrx =>
            ReadNullable(factory, istr) ?? throw new InvalidDataException("read null for a non-nullable proxy");

        /// <summary>Reads a nullable proxy from the input stream.</summary>
        /// <paramtype name="T">The type of the new service proxy.</paramtype>
        /// <param name="factory">This proxy factory. Use INamePrx.Factory for this parameter, where INamePrx is the
        /// proxy type.</param>
        /// <param name="istr">The input stream to read from.</param>
        /// <returns>The proxy read from the stream, or null.</returns>
        public static T? ReadNullable<T>(this IProxyFactory<T> factory, InputStream istr)
            where T : class, IServicePrx
        {
            if (istr.Encoding == Encoding.V11)
            {
                var identity = new Identity(istr);
                if (identity.Name.Length == 0) // such identity means received a null proxy with the 1.1 encoding
                {
                    return null;
                }

                var proxyData = new ProxyData11(istr);

                if ((byte)proxyData.Protocol == 0)
                {
                    throw new InvalidDataException("received proxy with protocol set to 0");
                }
                if (proxyData.ProtocolMinor != 0)
                {
                    throw new InvalidDataException(
                        $"received proxy with invalid protocolMinor value: {proxyData.ProtocolMinor}");
                }

                // The min size for an Endpoint with the 1.1 encoding is: transport (short = 2 bytes) + encapsulation
                // header (6 bytes), for a total of 8 bytes.
                Endpoint[] endpointArray =
                    istr.ReadArray(minElementSize: 8, istr => istr.ReadEndpoint11(proxyData.Protocol));

                Endpoint? endpoint = null;
                IEnumerable<Endpoint> altEndpoints;

                if (endpointArray.Length == 0)
                {
                    string adapterId = istr.ReadString();
                    if (adapterId.Length > 0)
                    {
                        endpoint = LocEndpoint.Create(adapterId, proxyData.Protocol);
                    }
                    altEndpoints = ImmutableList<Endpoint>.Empty;
                }
                else
                {
                    endpoint = endpointArray[0];
                    altEndpoints = endpointArray[1..];
                }

                if (proxyData.Protocol == Protocol.Ice1)
                {
                    if (proxyData.FacetPath.Length > 1)
                    {
                        throw new InvalidDataException(
                            $"received proxy with {proxyData.FacetPath.Length} elements in its facet path");
                    }

                    return CreateIce1Proxy(proxyData.Encoding,
                                           endpoint,
                                           altEndpoints,
                                           proxyData.FacetPath.Length == 1 ? proxyData.FacetPath[0] : "",
                                           identity);
                }
                else
                {
                    if (proxyData.FacetPath.Length > 0)
                    {
                        throw new InvalidDataException(
                            $"received proxy for protocol {proxyData.Protocol.GetName()} with facet");
                    }
                    if (proxyData.InvocationMode != InvocationMode.Twoway)
                    {
                        throw new InvalidDataException(
                            $"received proxy for protocol {proxyData.Protocol.GetName()} with invocation mode set");
                    }

                    Connection? connection = null;
                    if (endpoint == null)
                    {
                        // Use the connection endpoint if the connection is an outgoing connection
                        endpoint = istr.Connection?.IsIncoming ?? true ? null : istr.Connection.RemoteEndpoint;
                        connection = istr.Connection;
                    }

                    try
                    {
                        return factory.Create(identity.ToPath(),
                                              connection?.Protocol ?? proxyData.Protocol,
                                              proxyData.Encoding,
                                              endpoint,
                                              altEndpoints,
                                              connection,
                                              istr.Invoker);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("received invalid proxy", ex);
                    }
                }
            }
            else
            {
                Debug.Assert(istr.Encoding == Encoding.V20);

                var proxyData = new ProxyData20(istr);

                if (proxyData.Path == null)
                {
                    return null;
                }

                Protocol protocol = proxyData.Protocol ?? Protocol.Ice2;
                var endpoint = proxyData.Endpoint?.ToEndpoint(protocol);
                ImmutableList<Endpoint> altEndpoints =
                    proxyData.AltEndpoints?.Select(
                        data => data.ToEndpoint(protocol))?.ToImmutableList() ?? ImmutableList<Endpoint>.Empty;

                if (endpoint == null && altEndpoints.Count > 0)
                {
                    throw new InvalidDataException("received proxy with only alt endpoints");
                }

                if (protocol == Protocol.Ice1)
                {
                    string facet;
                    Identity identity;

                    int hashIndex = proxyData.Path.IndexOf('#');
                    if (hashIndex == -1)
                    {
                        identity = Identity.FromPath(proxyData.Path);
                        facet = "";
                    }
                    else
                    {
                        identity = Identity.FromPath(proxyData.Path[0..hashIndex]);
                        facet = proxyData.Path[(hashIndex + 1)..];
                    }

                    if (identity.Name.Length == 0)
                    {
                        throw new InvalidDataException($"received invalid ice1 identity '{proxyData.Path}'");
                    }

                    return CreateIce1Proxy(proxyData.Encoding ?? Encoding.V20,
                                           endpoint,
                                           altEndpoints,
                                           facet,
                                           identity);
                }
                else
                {
                    Connection? connection = null;
                    if (endpoint == null)
                    {
                        // Use the connection endpoint if the connection is an outgoing connection
                        endpoint = istr.Connection?.IsIncoming ?? true ? null : istr.Connection.RemoteEndpoint;
                        connection = istr.Connection;
                    }

                    try
                    {
                        return factory.Create(proxyData.Path,
                                              connection?.Protocol ?? protocol,
                                              proxyData.Encoding ?? Encoding.V20,
                                              endpoint,
                                              altEndpoints,
                                              connection,
                                              istr.Invoker);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("received invalid proxy", ex);
                    }
                }
            }

            // Creates an ice1 proxy
            T CreateIce1Proxy(
                Encoding encoding,
                Endpoint? endpoint,
                IEnumerable<Endpoint> altEndpoints,
                string facet,
                Identity identity)
            {
                // For interop with ZeroC Ice, an ice1 endpointless proxy is unmarshaled as an endpointless and
                // connectionless proxy - a "well-known proxy".

                try
                {
                    return factory.Create(identity, facet, encoding, endpoint, altEndpoints, connection: null, istr.Invoker);
                }
                catch (InvalidDataException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("received invalid proxy", ex);
                }
            }
        }

        /// <summary>Reads a tagged proxy from the input stream.</summary>
        /// <paramtype name="T">The type of the new service proxy.</paramtype>
        /// <param name="factory">This proxy factory. Use INamePrx.Factory for this parameter, where INamePrx is the
        /// proxy type.</param>
        /// <param name="istr">The input stream to read from.</param>
        /// <param name="tag">The tag.</param>
        /// <returns>The proxy read from the stream, or null.</returns>
        public static T? ReadTagged<T>(this IProxyFactory<T> factory, InputStream istr, int tag)
            where T : class, IServicePrx =>
            istr.ReadTaggedProxyHeader(tag) ? Read(factory, istr) : null;
    }
}
