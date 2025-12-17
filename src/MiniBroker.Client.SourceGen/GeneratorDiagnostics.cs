using Microsoft.CodeAnalysis;

namespace MiniBroker.Client.SourceGen;

public static class GeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor MissingResource = new(
        id: "MBG001",
        title: "Missing Embedded Resource",
        messageFormat: "Embedded resource '{0}' not found in assembly '{1}'",
        category: "MiniBroker.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}