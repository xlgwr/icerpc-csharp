// Copyright (c) ZeroC, Inc. All rights reserved.

using Demo;
using IceRpc;

// Use the ice protocol for compatibility with ZeroC Ice.
await using var connection = new ClientConnection(new Uri("ice://127.0.0.1:10000"));

// The service address URI includes the protocol to use (ice).
var hello = new HelloProxy(connection, new Uri("ice:/hello"));

Console.Write("To say hello to the server, type your name: ");

if (Console.ReadLine() is string name)
{
    Console.WriteLine(await hello.SayHelloAsync(name));
}