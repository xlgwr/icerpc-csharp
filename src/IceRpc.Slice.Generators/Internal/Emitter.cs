// Copyright (c) ZeroC, Inc.

namespace IceRpc.Slice.Generators.Internal;

internal class Emitter
{
    internal string Emit(IReadOnlyList<ServiceClass> serviceClasses, CancellationToken cancellationToken)
    {
        var generatedClasses = new List<string>();
        foreach (ServiceClass serviceClass in serviceClasses)
        {
            // stop if we're asked to.
            cancellationToken.ThrowIfCancellationRequested();

            string methodModifier =
                serviceClass.HasBaseServiceClass ? "public override" :
                serviceClass.IsSealed ? "public" : "public virtual";

            string dispatchImplementation;
            if (serviceClass.ServiceMethods.Count > 0)
            {
                dispatchImplementation = "";
                foreach (ServiceMethod serviceMethod in serviceClass.ServiceMethods)
                {
                    string operationName = serviceMethod.OperationName;
                    string dispatchMethodName = serviceMethod.DispatchMethodName;
                    dispatchImplementation +=
                        $"\"{operationName}\" => global::{dispatchMethodName}(this, request, cancellationToken),\n";
                }

                if (serviceClass.HasBaseServiceClass)
                {
                    dispatchImplementation += "_ => base.DispatchAsync(request, cancellationToken)";
                }
                else
                {
                    dispatchImplementation +=
                        "_ => new(new IceRpc.OutgoingResponse(request, IceRpc.StatusCode.NotImplemented))";
                }

                dispatchImplementation = @$"
return request.Operation switch
{{
    {dispatchImplementation.WithIndent("    ")}
}};".Trim();
            }
            else
            {
                dispatchImplementation = serviceClass.HasBaseServiceClass ?
                    "return base.DispatchAsync(request, cancellationToken);" :
                    "return new(new IceRpc.OutgoingResponse(request, IceRpc.StatusCode.NotImplemented));";
            }

            string dispatcherClass = $@"
/// <summary>Implements <see cref=""IceRpc.IDispatcher"" /> for the Slice interface(s) implemented by this class.
/// </summary>
partial {serviceClass.Keyword} {serviceClass.Name} : IceRpc.IDispatcher
{{
    /// <summary>Dispatches an incoming request to a method of {serviceClass.Name} based on the operation name carried
    /// by the request.</summary>
    /// <param name=""request"">The incoming request.</param>
    /// <param name=""cancellationToken"">A cancellation token that receives the cancellation requests.</param>
    /// <returns>The outgoing response.</returns>
    /// <exception cref=""IceRpc.DispatchException"">Thrown if the operation name carried by the request does not
    /// correspond to any method implemented by this class. The exception status code is
    /// <see cref=""IceRpc.StatusCode.NotImplemented"" /> in this case.</exception>
    {methodModifier} global::System.Threading.Tasks.ValueTask<IceRpc.OutgoingResponse> DispatchAsync(
        IceRpc.IncomingRequest request,
        global::System.Threading.CancellationToken cancellationToken)
    {{
        {dispatchImplementation.WithIndent("        ")}
    }}
}}";

            string container = dispatcherClass;
            ContainerDefinition? containerDefinition = serviceClass;
            while (containerDefinition.Enclosing is ContainerDefinition enclosing)
            {
                container = GenerateContainer($"partial {enclosing.Keyword} {enclosing.Name}", container);
                containerDefinition = enclosing;
            }

            if (serviceClass.ContainingNamespace is not null)
            {
                container = GenerateContainer($"namespace {serviceClass.ContainingNamespace}", container);
            }
            generatedClasses.Add(container);
        }

        string generated = @$"
// <auto-generated/>
// IceRpc.Slice.Generators version: {typeof(ServiceGenerator).Assembly.GetName().Version!.ToString(3)}
#nullable enable

#pragma warning disable CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0619 // Type or member is obsolete

";
        generated += string.Join("\n\n", generatedClasses).Trim();
        generated += "\n";
        return generated.ReplaceLineEndings();
    }

    private static string GenerateContainer(string header, string body)
    {
        return $@"
{header}
{{
    {body.WithIndent("    ")}
}}".Trim();
    }
}
