using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MiniBroker.Client.SourceGen.Infrastructure;

public class AttributeContext<T>
{
    public string ClassName { get; set; } = null!;
    public string ClassNamespace { get; set; } = null!;
    public string PreferredNamespace { get; set; } = null!;
    public ITypeSymbol Symbol { get; set; } = null!;
    public T DeclarationSyntax { get; set; } = default!;
    public Dictionary<string, TypedConstant?>? AttributeArguments { get; set; }
}