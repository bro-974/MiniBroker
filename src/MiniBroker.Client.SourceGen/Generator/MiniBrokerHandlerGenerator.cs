using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniBroker.Client.SourceGen.Infrastructure;

namespace MiniBroker.Client.SourceGen.Generator;

[Generator]
public class MiniBrokerHandlerGenerator : ATemplateGenerator<MiniBrokerHandlerAttribute>
{
    private const string ModuleTemplateName = "MiniBrokerClientModule";
    private const string InterfaceFullName = "MiniBroker.Abstraction.IHandleMessage<T>";
    
    private const string HandlerPartialTemplateName = "MiniBrokerClientHandlerPartial";
    

    protected override void GenerateCode(SourceProductionContext context,
        List<AttributeContext<ClassDeclarationSyntax>> attributesData)
    {
        var moduleSourceCode = GetSourceCodeForAllAttributes(context, attributesData, ModuleTemplateName);
        context.AddSource("GeneratedMiniBrokerClientModule.g.cs", moduleSourceCode);
        
        // New partial generation for each handler
        foreach (var attr in attributesData)
        {
            var partialCode = GetSourceCodeForEachAttribute(context, attr, HandlerPartialTemplateName);
            context.AddSource($"{attr.ClassName}.HandlerPartial.g.cs", partialCode);
        }
    }
    
    protected override void AddParameters(SourceProductionContext context,
        AttributeContext<ClassDeclarationSyntax> attributeInfo,
        Dictionary<string, string> transforms)
    {
        base.AddParameters(context, attributeInfo, transforms);

        // Find IHandleMessage<T> interface
        var iface = attributeInfo.Symbol.AllInterfaces
            .FirstOrDefault(i => i.ConstructedFrom.ToDisplayString() == InterfaceFullName);

        if (iface != null)
        {
            var messageType = iface.TypeArguments[0];
            transforms["MessageFullName"] = messageType.ToDisplayString();

            // Build usings
            var usings = new HashSet<string>
            {
                messageType.ContainingNamespace.ToDisplayString(),
                attributeInfo.ClassNamespace,
                "MiniBroker.Abstraction"
            };
            transforms["Usings"] = string.Join("\n", usings.Select(ns => $"using {ns};"));
        }
    }

    protected override void AddParameters(SourceProductionContext context, List<AttributeContext<ClassDeclarationSyntax>> attributesData,
        Dictionary<string, string> parameters)
    {
        base.AddParameters(context, attributesData, parameters);
        var registrations = new List<string>();
        var usings = new HashSet<string>();
        
        foreach (var data in attributesData)
        {
            // 🧠 Extract the attribute instance
            var enumValue = GetNamedArgument(data, "Lifetime", 0);
            var lifetimeMethod = enumValue switch
            {
                1 => "AddAsScoped",
                2 => "AddAsSingleton",
                _ => "AddAsTransient"
            };

            foreach (var named in data.Symbol.AllInterfaces)
                if (named is not null &&
                    named.ConstructedFrom.ToDisplayString() == InterfaceFullName)
                {
                    var messageType = named.TypeArguments[0];
                    var messageTypeFullName = messageType.ToDisplayString();
                    var handlerFullName = $"{data.ClassNamespace}.{data.ClassName}";
                    //services.AddAsTransient<IHandleMessage<MiniBroker.Demo.App1.Messages.ChatMessage>, MiniBroker.Demo.App1.Handlers.ChatMessageHandler>();
                    var registration = $"        services.{lifetimeMethod}<IHandleMessage<{messageTypeFullName}>, {handlerFullName}>(\"{messageTypeFullName}\");";
                    
                    registrations.Add(registration);
                    usings.Add(messageType.ContainingNamespace.ToDisplayString());
                }

            usings.Add(data.ClassNamespace);
        }

        usings.Add("MiniBroker.Abstraction");
        parameters["HandlerRegistrations"]= string.Join("\n", registrations.Distinct());
        parameters["Usings"]= string.Join("\n", usings.Distinct().Select(ns => $"using {ns};"));
    }

    protected override string GetResourceByName(SourceProductionContext context, string templateName)
    {
        var libName = GetType().Assembly.GetName().Name;
        return templateName switch
        {
            ModuleTemplateName => GetEmbeddedResource(context, $"{libName}.Templates.{ModuleTemplateName}.t.cs"),
            HandlerPartialTemplateName => GetEmbeddedResource(context, $"{libName}.Templates.{HandlerPartialTemplateName}.t.cs"),
            _ => string.Empty
        };
    }
}