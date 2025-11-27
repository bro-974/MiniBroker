using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MiniBroker.Client.SourceGen.Infrastructure;

/// <summary>
///     Template Manager for Attribute on a Class
/// </summary>
/// <typeparam name="TAttribute"></typeparam>
public abstract class
    ATemplateGenerator<TAttribute> : AAttributeIncrementalGenerator<TAttribute, ClassDeclarationSyntax>
    where TAttribute : Attribute
{
    private const string TemplateVarFormat = "{{{{{0}}}}}";
    private readonly Dictionary<string, string> _templateCache = new();
    
    protected abstract string GetResourceByName(SourceProductionContext context, string templateName);

    /// <summary>
    ///     Get the embedded template resource by its name.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual string GetEmbeddedResource(SourceProductionContext context, string path)
    {
        if (_templateCache.TryGetValue(path, out var cached))
            return cached;
        
        var assembly = GetType().Assembly;

        using var stream = assembly.GetManifestResourceStream(path);
        if (stream == null)
        {
            AddAnalyserMessage(context, GeneratorDiagnostics.MissingResource, path,
                assembly.FullName);
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _templateCache[path] = content;
        return content;
    }

    /// <summary>
    ///     replace parameters in the template with the provided values.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="template"></param>
    /// <param name="templateParameters"></param>
    /// <returns></returns>
    protected virtual string ReplaceParameters(SourceProductionContext context, string template,
        Dictionary<string, string>? templateParameters = null)
    {
        if (templateParameters == null || templateParameters.Count == 0)
            return template;

        var sb = new StringBuilder(template);
        // foreach (var transform in templateParameters) 
        //     sb.Replace($"{{{{{transform.Key}}}}}", transform.Value);
        foreach (var kvp in templateParameters)
        {
            var placeholder = string.Format(TemplateVarFormat, kvp.Key);
            sb.Replace(placeholder, kvp.Value ?? string.Empty);
        }
        return sb.ToString();
    }

    #region GetSourceCodeForEachAttribute

    /// <summary>
    ///     Generate source code for each attribute using a template.
    ///     This method is used to generate a class per attribute declaration.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="attributeInfo"></param>
    /// <param name="templateName"></param>
    /// <param name="templateParameters"></param>
    /// <returns></returns>
    protected virtual string GetSourceCodeForEachAttribute(SourceProductionContext context,
        AttributeContext<ClassDeclarationSyntax> attributeInfo,
        string templateName, Dictionary<string, string>? templateParameters = null)
    {
        var template = GetResourceByName(context, templateName);
        templateParameters ??= new Dictionary<string, string>();
        AddParameters(context, attributeInfo, templateParameters);
        return ReplaceParameters(context, template, templateParameters);
    }

    /// <summary>
    ///     Add parameters to the template for a single attribute.
    ///     This method is used to inject class-specific information into the template.
    ///     default parameters are:
    ///     - ClassName: The name of the class where the attribute is applied.
    ///     - ClassNamespace: The namespace of the class where the attribute is applied.
    ///     - PreferredNamespace: The preferred namespace for the generated code, typically the assembly name.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="attributeInfo"></param>
    /// <param name="transforms"></param>
    protected virtual void AddParameters(SourceProductionContext context,
        AttributeContext<ClassDeclarationSyntax> attributeInfo, Dictionary<string, string> transforms)
    {
        transforms[nameof(attributeInfo.ClassName)] = attributeInfo.ClassName;
        transforms[nameof(attributeInfo.ClassNamespace)] = attributeInfo.ClassNamespace;
        transforms[nameof(attributeInfo.PreferredNamespace)] = attributeInfo.PreferredNamespace;
        transforms["SafeNamespace"] = $"{attributeInfo.PreferredNamespace}.Client.SourceGenerated";
    }

    #endregion

    #region GetSourceCodeForAllAttributes

    /// <summary>
    ///     Generate source code for all attributes using a template.
    ///     This method is used to generate a single class code that contains all attributes.
    ///     It is typically used to generate a class who aggregate all Attributes into one class file.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="attributeInfo"></param>
    /// <param name="templateName"></param>
    /// <param name="templateParameters"></param>
    /// <returns></returns>
    protected virtual string GetSourceCodeForAllAttributes(SourceProductionContext context,
        List<AttributeContext<ClassDeclarationSyntax>> attributeInfo,
        string templateName, Dictionary<string, string>? templateParameters = null)
    {
        var template = GetResourceByName(context, templateName);

        if (templateParameters == null)
        {
            templateParameters = new Dictionary<string, string>();
            AddParameters(context, attributeInfo, templateParameters);
        }

        return ReplaceParameters(context, template, templateParameters);
    }

    /// <summary>
    ///     add parameters to the template for all attributes.
    ///     This method is used to inject assembly-specific information into the template.
    ///     default parameters are:
    ///     - PreferredNamespace: The preferred namespace for the generated code, typically the assembly name.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="attributesData"></param>
    /// <param name="transforms"></param>
    protected virtual void AddParameters(SourceProductionContext context,
        List<AttributeContext<ClassDeclarationSyntax>> attributesData,
        Dictionary<string, string> transforms)
    {
        if (attributesData.Count == 0)
        {
            transforms["SafeNamespace"] = "MiniBroker.Client.SourceGenerated";
            return;
        }

        var attributeInfo = attributesData[0];
        transforms[nameof(attributeInfo.PreferredNamespace)] = attributeInfo.PreferredNamespace;
        transforms["SafeNamespace"] = $"{attributeInfo.PreferredNamespace}.Client.SourceGenerated";
    }

    #endregion
}