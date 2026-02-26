using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DeltaStreamNet.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Scriban;

namespace DeltaStreamNet;

[Generator]
public class FrameWrapperGenerator : IIncrementalGenerator
{
    private const string StreamAttributesSource =
        """
        using System;
        using System.Collections.Generic;

        namespace DeltaStreamNet;

        [AttributeUsage(AttributeTargets.Class)]
        public class StreamFrameAttribute : Attribute
        {
            public bool PropagateAttributes { get; set; }
            public bool MinifyJson { get; set; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class StreamFieldAttribute : Attribute {}

        [AttributeUsage(AttributeTargets.Property)]
        public class StreamKeyAttribute : Attribute {}
        """;

    private const string PropertyDeltaWrapperSource =
        """
        using System;
        using System.Text.Json.Serialization;

        namespace DeltaStreamNet;

        public record struct PropertyDeltaWrapper<T>
        {
            [JsonPropertyName("v")]
            public T Value { get; set; }
        }
        """;

    private const string DeltaStreamSerializableAttributeSource =
        """
        using System;

        namespace DeltaStreamNet;

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class DeltaStreamSerializableAttribute : Attribute
        {
            public DeltaStreamSerializableAttribute(Type type) { }
        }
        """;

    #region Implementation of IIncrementalGenerator

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        ctx.RegisterPostInitializationOutput(output => output
            .AddSource("StreamAttributes.g.cs",
                SourceText.From(StreamAttributesSource, Encoding.UTF8)));

        ctx.RegisterPostInitializationOutput(output => output
            .AddSource("PropertyDeltaWrapper.g.cs",
                SourceText.From(PropertyDeltaWrapperSource, Encoding.UTF8)));

        ctx.RegisterPostInitializationOutput(output => output
            .AddSource("DeltaStreamSerializableAttribute.g.cs",
                SourceText.From(DeltaStreamSerializableAttributeSource, Encoding.UTF8)));

        // Pipeline 1: [StreamFrameAttribute] records → KeyFrame, DeltaFrame, DeltaFrameGenerator
        var frameProvider = ctx.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is RecordDeclarationSyntax,
                Transform)
            .Where(x => x is not null);

        ctx.RegisterSourceOutput(frameProvider, GenerateFrame);

        // Pipeline 2: [DeltaStreamSerializable] classes → DeltaStreamContext partial
        var contextProvider = ctx.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                ContextTransform)
            .Where(x => x is not null);

        ctx.RegisterSourceOutput(contextProvider, GenerateContext);
    }

    #endregion

    #region Frame pipeline

    private void GenerateFrame(SourceProductionContext sourceProductionContext, ClassInfo? classInfo)
    {
        var model = new
        {
            class_namespace = classInfo?.Namespace,
            class_name = classInfo?.Name,
            keyframe_class_name = classInfo?.Name + "KeyFrame",
            delta_class_name = classInfo?.Name + "DeltaFrame",
            has_stream_key = classInfo?.HasStreamKey ?? false,
            stream_key_property_name = classInfo?.StreamKeyPropertyName,
            stream_key_type_name = classInfo?.StreamKeyTypeName,
            collection_keyed_delta_class_name = classInfo?.CollectionKeyedDeltaClassName,
            class_attribute_block = classInfo?.ClassAttributeBlock ?? "",
            propagate_attributes = classInfo?.PropagateAttributes ?? false,
            minify_json = classInfo?.MinifyJson ?? false,
            properties = classInfo?.Properties.Select(p => new
            {
                type_name = p.TypeName,
                name = p.Name,
                is_stream_frame = p.IsStreamFrame,
                delta_type_name = p.DeltaTypeName,
                key_frame_type_name = p.KeyFrameTypeName,
                attribute_block = p.AttributeBlock,
                is_keyed_collection = p.IsKeyedCollection,
                collection_delta_type_name = p.CollectionDeltaTypeName,
                json_minified_name = p.JsonMinifiedName ?? ""
            })
        };

        var keyFrameTemplateScriban = Template.Parse(TemplateLoader.Get("KeyFrameClass.scriban"));
        var keyFrameSource = keyFrameTemplateScriban.Render(model);
        sourceProductionContext.AddSource($"{classInfo?.Name}.KeyFrame.g.cs", keyFrameSource);

        var deltaFrameTemplateScriban = Template.Parse(TemplateLoader.Get("DeltaFrameClass.scriban"));
        var deltaFrameSource = deltaFrameTemplateScriban.Render(model);
        sourceProductionContext.AddSource($"{classInfo?.Name}.DeltaFrame.g.cs", deltaFrameSource);

        var generatorTemplateScriban = Template.Parse(TemplateLoader.Get("DeltaFrameGeneratorClass.scriban"));
        var generatorSource = generatorTemplateScriban.Render(model);
        sourceProductionContext.AddSource($"{classInfo?.Name}.DeltaFrameGenerator.g.cs", generatorSource);

        // Emit collection delta class for element types that carry [StreamKey]
        if (classInfo?.HasStreamKey == true)
        {
            var collectionTemplateScriban = Template.Parse(TemplateLoader.Get("CollectionKeyedDeltaClass.scriban"));
            var collectionSource = collectionTemplateScriban.Render(model);
            sourceProductionContext.AddSource($"{classInfo?.Name}.CollectionKeyedDelta.g.cs", collectionSource);
        }
    }

    private ClassInfo? Transform(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var classDeclarationSyntax = (RecordDeclarationSyntax)syntaxContext.Node;

        if (ModelExtensions.GetDeclaredSymbol(syntaxContext.SemanticModel, classDeclarationSyntax, cancellationToken) is not INamedTypeSymbol classSymbol)
            return null;

        var compilation = syntaxContext.SemanticModel.Compilation;

        var frameAttribute = compilation.GetTypeByMetadataName(
            typeof(StreamFrameAttribute).FullName!);

        if (frameAttribute is null)
            return null;

        var streamFrameAttrData = classSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, frameAttribute));

        if (streamFrameAttrData == null)
            return null;

        var propagateAttributes = streamFrameAttrData.NamedArguments
            .FirstOrDefault(na => na.Key == "PropagateAttributes")
            .Value.Value is true;

        var minifyJson = streamFrameAttrData.NamedArguments
            .FirstOrDefault(na => na.Key == "MinifyJson")
            .Value.Value is true;

        var streamKeyAttr = compilation.GetTypeByMetadataName("DeltaStreamNet.StreamKeyAttribute");
        var keyProp = streamKeyAttr != null
            ? classSymbol.GetMembers().OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.GetAttributes()
                    .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, streamKeyAttr)))
            : null;

        var hasStreamKey = keyProp != null;
        var ns = classSymbol.ContainingNamespace?.ToDisplayString();

        var propertyInfos = GetPropertyInfos(classSymbol, compilation, propagateAttributes,
            filterJsonPropertyName: minifyJson && propagateAttributes).ToList();

        if (minifyJson)
        {
            var minifiedNames = ComputeMinifiedNames(propertyInfos.Select(p => p.Name));
            for (var i = 0; i < propertyInfos.Count; i++)
            {
                var pi = propertyInfos[i];
                if (minifiedNames.TryGetValue(pi.Name, out var shortName))
                    pi.JsonMinifiedName = shortName;
                propertyInfos[i] = pi;
            }
        }

        return new ClassInfo
        {
            Namespace = ns,
            Name = classSymbol.Name,
            Accessibility = classSymbol.DeclaredAccessibility,
            Properties = propertyInfos,
            HasStreamKey = hasStreamKey,
            StreamKeyPropertyName = keyProp?.Name,
            StreamKeyTypeName = keyProp?.Type.ToDisplayString(),
            CollectionKeyedDeltaClassName = hasStreamKey ? $"{classSymbol.Name}CollectionKeyedDelta" : null,
            ClassAttributeBlock = propagateAttributes
                ? GetClassAttributeBlock(classSymbol, compilation)
                : string.Empty,
            PropagateAttributes = propagateAttributes,
            MinifyJson = minifyJson
        };
    }

    private IEnumerable<PropertyInfo> GetPropertyInfos(
        INamedTypeSymbol classSymbol, Compilation compilation, bool propagateAttributes,
        bool filterJsonPropertyName = false)
    {
        var streamFrameAttr = compilation.GetTypeByMetadataName(typeof(StreamFrameAttribute).FullName!);
        var streamKeyAttr   = compilation.GetTypeByMetadataName("DeltaStreamNet.StreamKeyAttribute");
        var listSymbol      = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

        return classSymbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .OrderBy(x => x.Name)
            .Select(p =>
            {
                var propTypeSymbol = p.Type as INamedTypeSymbol;
                var isStreamFrame = streamFrameAttr != null && propTypeSymbol != null &&
                    propTypeSymbol.GetAttributes()
                        .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, streamFrameAttr));

                var typeNs = propTypeSymbol?.ContainingNamespace?.ToDisplayString();

                // Detect List<T> where T is [StreamFrame] and has [StreamKey]
                var isKeyedCollection = false;
                string? collectionDeltaTypeName = null;

                if (!isStreamFrame && listSymbol != null && propTypeSymbol != null && streamFrameAttr != null
                    && SymbolEqualityComparer.Default.Equals(propTypeSymbol.OriginalDefinition, listSymbol))
                {
                    var elementType = propTypeSymbol.TypeArguments.Length == 1
                        ? propTypeSymbol.TypeArguments[0] as INamedTypeSymbol
                        : null;

                    if (elementType != null)
                    {
                        var elementIsStreamFrame = elementType.GetAttributes()
                            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, streamFrameAttr));

                        var elementHasStreamKey = streamKeyAttr != null && elementType.GetMembers()
                            .OfType<IPropertySymbol>()
                            .Any(ep => ep.GetAttributes()
                                .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, streamKeyAttr)));

                        if (elementIsStreamFrame && elementHasStreamKey)
                        {
                            isKeyedCollection = true;
                            var elementNs = elementType.ContainingNamespace?.ToDisplayString();
                            collectionDeltaTypeName = string.IsNullOrEmpty(elementNs)
                                ? $"{elementType.Name}CollectionKeyedDelta"
                                : $"{elementNs}.{elementType.Name}CollectionKeyedDelta";
                        }
                    }
                }

                return new PropertyInfo
                {
                    Accessibility = p.DeclaredAccessibility,
                    Name = p.Name,
                    TypeName = p.Type.ToDisplayString(),
                    IsStreamFrame = isStreamFrame,
                    DeltaTypeName = isStreamFrame ? $"{typeNs}.{propTypeSymbol!.Name}DeltaFrame" : null,
                    KeyFrameTypeName = isStreamFrame ? $"{typeNs}.{propTypeSymbol!.Name}KeyFrame" : null,
                    AttributeBlock = propagateAttributes
                        ? GetPropertyAttributeBlock(p, compilation, filterJsonPropertyName)
                        : string.Empty,
                    IsKeyedCollection = isKeyedCollection,
                    CollectionDeltaTypeName = collectionDeltaTypeName
                };
            });
    }

    private static string GetPropertyAttributeBlock(
        IPropertySymbol property, Compilation compilation, bool filterJsonPropertyName = false)
    {
        var syntaxRef = property.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return string.Empty;

        var propertySyntax = (PropertyDeclarationSyntax)syntaxRef.GetSyntax();
        var semanticModel = compilation.GetSemanticModel(propertySyntax.SyntaxTree);

        var attrs = propertySyntax.AttributeLists
            .SelectMany(al => al.Attributes)
            .Select(attr => (
                Syntax: attr,
                AttrClass: (semanticModel.GetSymbolInfo(attr).Symbol as IMethodSymbol)?.ContainingType
            ))
            .Where(x => x.AttrClass != null &&
                        x.AttrClass.ContainingNamespace?.ToDisplayString() != "DeltaStreamNet" &&
                        (!filterJsonPropertyName ||
                         x.AttrClass.ToDisplayString() != "System.Text.Json.Serialization.JsonPropertyNameAttribute"))
            .Select(x =>
            {
                var className = x.AttrClass!.ToDisplayString();
                var args = x.Syntax.ArgumentList?.ToString() ?? string.Empty;
                return $"[{className}{args}]";
            })
            .ToList();

        return string.Join(" ", attrs);
    }

    private static Dictionary<string, string> ComputeMinifiedNames(IEnumerable<string> propertyNames)
    {
        var names = propertyNames.ToList();
        var result = new Dictionary<string, string>();
        var remaining = new List<string>(names);

        for (var prefixLen = 1; remaining.Count > 0; prefixLen++)
        {
            var groups = remaining.GroupBy(n => n.Substring(0, Math.Min(prefixLen, n.Length)).ToLowerInvariant()).ToList();

            var nextRemaining = new List<string>();
            foreach (var group in groups)
            {
                var items = group.ToList();
                if (items.Count == 1)
                {
                    result[items[0]] = group.Key;
                }
                else
                {
                    // Check if all items produce the same prefix (name exhausted)
                    var allExhausted = items.All(n => prefixLen >= n.Length);
                    if (allExhausted)
                    {
                        // Fallback: use full lowercase name
                        foreach (var item in items)
                            result[item] = item.ToLowerInvariant();
                    }
                    else
                    {
                        nextRemaining.AddRange(items);
                    }
                }
            }

            remaining = nextRemaining;
        }

        return result;
    }

    private static string GetClassAttributeBlock(INamedTypeSymbol classSymbol, Compilation compilation)
    {
        var syntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return string.Empty;

        var classSyntax = (RecordDeclarationSyntax)syntaxRef.GetSyntax();
        var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

        var attrs = classSyntax.AttributeLists
            .SelectMany(al => al.Attributes)
            .Select(attr => (
                Syntax: attr,
                AttrClass: (semanticModel.GetSymbolInfo(attr).Symbol as IMethodSymbol)?.ContainingType
            ))
            .Where(x => x.AttrClass != null &&
                        x.AttrClass.ContainingNamespace?.ToDisplayString() != "DeltaStreamNet")
            .Select(x =>
            {
                var className = x.AttrClass!.ToDisplayString();
                var args = x.Syntax.ArgumentList?.ToString() ?? string.Empty;
                return $"[{className}{args}]";
            })
            .ToList();

        return string.Join("\n", attrs);
    }

    #endregion

    #region Context pipeline

    private ContextInfo? ContextTransform(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;

        if (syntaxContext.SemanticModel.GetDeclaredSymbol(classDecl, cancellationToken) is not INamedTypeSymbol classSymbol)
            return null;

        var compilation = syntaxContext.SemanticModel.Compilation;

        var serializableAttr = compilation.GetTypeByMetadataName(
            typeof(DeltaStreamSerializableAttribute).FullName!);

        if (serializableAttr is null)
            return null;

        var matchingAttrs = classSymbol.GetAttributes()
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializableAttr))
            .ToList();

        if (!matchingAttrs.Any())
            return null;

        var dtoTypes = matchingAttrs
            .Select(a => a.ConstructorArguments.Length > 0
                ? a.ConstructorArguments[0].Value as INamedTypeSymbol
                : null)
            .Where(t => t is not null)
            .Select(t => new DtoTypeInfo
            {
                Name = t!.Name,
                FullName = t.ToDisplayString(),
                GeneratorFullName = $"{t.ContainingNamespace?.ToDisplayString()}.{t.Name}DeltaFrameGenerator"
            })
            .ToList();

        return new ContextInfo
        {
            Namespace = classSymbol.ContainingNamespace?.ToDisplayString(),
            Name = classSymbol.Name,
            DtoTypes = dtoTypes
        };
    }

    private void GenerateContext(SourceProductionContext ctx, ContextInfo? contextInfo)
    {
        if (contextInfo is null) return;

        var model = new
        {
            context_namespace = contextInfo.Value.Namespace,
            context_class_name = contextInfo.Value.Name,
            dto_types = contextInfo.Value.DtoTypes.Select(d => new
            {
                name = d.Name,
                full_name = d.FullName,
                generator_full_name = d.GeneratorFullName
            })
        };

        var template = Template.Parse(TemplateLoader.Get("DeltaStreamContextClass.scriban"));
        var source = template.Render(model);
        ctx.AddSource($"{contextInfo.Value.Name}.g.cs", source);
    }

    #endregion
}
