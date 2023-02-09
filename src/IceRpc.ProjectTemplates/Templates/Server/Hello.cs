using IceRpc.Features;
using IceRpc.Slice;

namespace HelloExample;

/// <summary>Implements the IHelloService interface generated by the Slice compiler.</summary>
internal class Hello : Service, IHelloService
{
    public ValueTask<string> SayHelloAsync(
        string name,
        IFeatureCollection features,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{name} says hello!");
        return new($"Hello, {name}!");
    }
}
