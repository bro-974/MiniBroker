using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MiniBroker.Client.SourceGen.Infrastructure;

/// <summary>
///     Detect a specific Attribute Type and generate source code
///     The attribute is expected to be on a class declaration and the same attribute is unique per class.
/// </summary>
/// <typeparam name="TAttribute"></typeparam>
/// <typeparam name="TAttributeType"></typeparam>
public abstract class AAttributeIncrementalGenerator<TAttribute, TAttributeType> : IIncrementalGenerator
    where TAttribute : Attribute
    where TAttributeType : BaseTypeDeclarationSyntax
{
    private readonly Type _targetAttribute = typeof(TAttribute);
    private INamedTypeSymbol? _targetAttributeSymbol;
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        context.RegisterSourceOutput(compilationProvider, (spc, compilation) =>
        {
            _targetAttributeSymbol = compilation.GetTypeByMetadataName(typeof(TAttribute).FullName!);
        });

        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(IsSyntaxTargetForGeneration, GetSemanticTargetForGeneration)
            .Where(type => type != null)
            .Collect();

        context.RegisterSourceOutput(provider, GenerateCode);
    }

    protected abstract void GenerateCode(SourceProductionContext context,
        List<AttributeContext<TAttributeType>> data);

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(INamedTypeSymbol, TAttributeType)?> types)
    {
        if (types.Length == 0) return;
        var attributesContexts = new List<AttributeContext<TAttributeType>>();

        foreach (var item in types)
        {
            if (item == null)
                continue;

            var (symbol, attributeType) = item.Value;

            var attrContext = new AttributeContext<TAttributeType>
            {
                ClassName = symbol.Name,
                ClassNamespace = symbol.ContainingNamespace.ToString(),
                PreferredNamespace = symbol.ContainingAssembly.Name,
                Symbol = symbol,
                DeclarationSyntax = attributeType
            };

            var attrData = symbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == _targetAttribute.FullName);


            if (attrData != null &&
                (attrData.ConstructorArguments.Length > 0 || attrData.NamedArguments.Length > 0))
            {
                var args = new Dictionary<string, TypedConstant?>();
                // Add constructor args
                for (var index = 0; index < attrData.ConstructorArguments.Length; index++)
                {
                    var ca = attrData.ConstructorArguments[index];
                    args[index.ToString()] = ca;
                }

                // Add named attribute value
                foreach (var namedArg in attrData.NamedArguments)
                    args.Add(namedArg.Key, namedArg.Value);

                attrContext.AttributeArguments = args;
            }

            attributesContexts.Add(attrContext);
        }

        GenerateCode(context, attributesContexts);
    }

    protected T GetNamedArgument<T>(AttributeContext<TAttributeType> ctx, string name, T fallback = default!)
    {
        if (ctx.AttributeArguments == null)
            return fallback;

        if (!ctx.AttributeArguments.TryGetValue(name, out var typed) || typed == null)
            return fallback;

        if (typed.Value.Value is T direct)
            return direct;

        return fallback;
    }

    //Filter for the current analyzed code
    //Keep it simple!
    private bool IsSyntaxTargetForGeneration(
        SyntaxNode syntaxNode,
        CancellationToken cancellationToken)
    {
        if (!(syntaxNode is AttributeSyntax attribute))
            return false;

        var name = ExtractName(attribute.Name);

        return string.Equals(_targetAttribute.Name, name, StringComparison.Ordinal) ||
               string.Equals(_targetAttribute.Name, name + "Attribute", StringComparison.Ordinal);
    }

    private static string ExtractName(NameSyntax name)
    {
        switch (name)
        {
            case SimpleNameSyntax ins:
                return ins.Identifier.Text;
            case QualifiedNameSyntax qns:
                return qns.Right.Identifier.Text;
            default:
                return string.Empty;
        }
    }

    private (INamedTypeSymbol, TAttributeType)? GetSemanticTargetForGeneration(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;

        if (attributeSyntax.Parent?.Parent is not BaseTypeDeclarationSyntax baseTypeDeclaration)
            return null;

        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(baseTypeDeclaration);
        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
            return null;

        // Ensure it has the correct attribute
        if (!HasTargetAttribute(namedTypeSymbol))
            return null;

        if (baseTypeDeclaration is not TAttributeType typedDeclaration)
            return null;

        return (namedTypeSymbol, typedDeclaration);
    }

    private bool HasTargetAttribute(INamedTypeSymbol typeSymbol)
    {
        if (_targetAttributeSymbol is null)
            return false;
        
        return typeSymbol.GetAttributes().Any(attr =>
            SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _targetAttributeSymbol));
    }

    protected void AddAnalyserMessage(SourceProductionContext context, DiagnosticDescriptor descriptor,
        params object?[]? messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, Location.None, messageArgs);
        context.ReportDiagnostic(diagnostic);
    }
}