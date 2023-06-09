﻿using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using MrMeeseeks.SourceGeneratorUtility;
using MrMeeseeks.SourceGeneratorUtility.Extensions;

namespace MrMeeseeks.Visitor;

[Generator]
public class SourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Nothing to do here
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var visitorInterfacePairAttribute =
            context.Compilation.GetTypeByMetadataName(typeof(VisitorInterfacePairAttribute).FullName ?? "")
            ?? throw new ArgumentException("Type not found by metadata name.", nameof(VisitorInterfacePairAttribute));
        var visitorInterfaceAttribute =
            context.Compilation.GetTypeByMetadataName(typeof(VisitorInterfaceAttribute).FullName ?? "")
            ?? throw new ArgumentException("Type not found by metadata name.", nameof(VisitorInterfaceAttribute));
        var visitorInterfacePairs = context
            .Compilation
            .Assembly
            .GetAttributes()
            .Where(ad => CustomSymbolEqualityComparer.Default.Equals(ad.AttributeClass, visitorInterfacePairAttribute))
            .Where(ad => ad.ConstructorArguments.Length == 2
                         && ad.ConstructorArguments[0].Value is INamedTypeSymbol
                         && ad.ConstructorArguments[1].Value is INamedTypeSymbol)
            .Select(ad => (ad.ConstructorArguments[0].Value as INamedTypeSymbol, ad.ConstructorArguments[1].Value as INamedTypeSymbol))
            .OfType<(INamedTypeSymbol, INamedTypeSymbol)>()
            
            .Concat(new NamedTypeCache(context, new CheckInternalsVisible(context))
                .ForAssembly(context.Compilation.Assembly)
                .Where(nts => nts.GetAttributes()
                    .Any(ad => CustomSymbolEqualityComparer.Default.Equals(ad.AttributeClass, visitorInterfaceAttribute)))
                .Select(nts => nts.GetAttributes()
                    .First(ad => CustomSymbolEqualityComparer.Default.Equals(ad.AttributeClass, visitorInterfaceAttribute))
                    .ConstructorArguments[0].Value is INamedTypeSymbol elementInterfaceType
                    ? (nts, elementInterfaceType)
                    : throw new ArgumentException("Attribute not found on visitor interface.", nameof(VisitorInterfaceAttribute))));

        var implementationTypeSetCache = new NamedTypeCache(context, new CheckInternalsVisible(context));
        var interfaceTypesOfCurrentAssembly = implementationTypeSetCache.ForAssembly(context.Compilation.Assembly)
            .Where(nts => nts.TypeKind == TypeKind.Interface)
            .ToList();

        foreach (var (visitorInterfaceType, elementInterfaceType) in visitorInterfacePairs)
        {
            var code = new StringBuilder();
            var elementSubInterfaceTypes = interfaceTypesOfCurrentAssembly
                .Where(it =>
                    it.AllDerivedTypes().Any(d => CustomSymbolEqualityComparer.Default.Equals(d, elementInterfaceType)))
                .ToList();
            
            var allBaseInterfaces = elementSubInterfaceTypes
                .SelectMany(it => it.AllDerivedTypes())
                .ToImmutableHashSet(CustomSymbolEqualityComparer.Default);
            
            var leafInterfaces = elementSubInterfaceTypes
                // Filter for leaf interfaces
                .Where(i => !allBaseInterfaces.Contains(i))
                .ToList();

            if (visitorInterfaceType.IsPartial())
            {
                var visitFunctionsCode = string.Join(Environment.NewLine, leafInterfaces
                    .Select(i => $"void Visit{i.Name}({i.FullName()} element);")
                    .ToList());

                code.AppendLine($$"""
namespace {{visitorInterfaceType.ContainingNamespace.FullName()}}
{
partial interface {{visitorInterfaceType.Name}}
{
{{visitFunctionsCode}}
}
}
""");
                context.NormalizeWhitespaceAndAddSource(
                    $"{visitorInterfaceType.ContainingNamespace.FullName()}.{visitorInterfaceType.Name}.VisitorPart.g.cs", 
                    code);
            }
            
            if (elementInterfaceType.IsPartial()
                && !elementInterfaceType
                    .GetMembers("Accept")
                    .OfType<IMethodSymbol>()
                    .Any(m => m.Parameters.Length == 1
                              && CustomSymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, visitorInterfaceType)))
            {
                var partialCode = new StringBuilder();
                partialCode.AppendLine($$"""
namespace {{elementInterfaceType.ContainingNamespace.FullName()}}
{
partial interface {{elementInterfaceType.Name}}
{
void Accept({{visitorInterfaceType.FullName()}} visitor);
}
}
""");
                context.NormalizeWhitespaceAndAddSource(
                    $"{elementInterfaceType.ContainingNamespace.FullName()}.{elementInterfaceType.Name}.ElementPart.g.cs", 
                    partialCode);
            }
            
            var elementImplementations = implementationTypeSetCache.ForAssembly(context.Compilation.Assembly)
                .Where(nts => nts.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Structure)
                .Where(nts => nts.AllInterfaces.Any(i => leafInterfaces.Contains(i, CustomSymbolEqualityComparer.Default)));
            
            foreach (var implementation in elementImplementations)
            {
                if (implementation.IsPartial()
                    && implementation
                    .AllInterfaces
                    .SingleOrDefault(i => leafInterfaces.Contains(i, CustomSymbolEqualityComparer.Default))
                    is {} leafInterface
                    && (!implementation
                        .GetMembers("Accept")
                        .OfType<IMethodSymbol>()
                        .Any(m => m.Parameters.Length == 1
                                  && CustomSymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, visitorInterfaceType))
                    || implementation
                            .GetMembers("Accept")
                            .OfType<IMethodSymbol>()
                            .SingleOrDefault(m => m.Parameters.Length == 1
                                                  && CustomSymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, visitorInterfaceType))
                        is { IsPartialDefinition: true, PartialDefinitionPart: null }))
                {
                    var firstBaseAcceptDeclaration = implementation.AllBaseTypes()
                        .Select(b => b.GetMembers("Accept").FirstOrDefault())
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m => m.Parameters.Length == 1
                                             && CustomSymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, visitorInterfaceType));

                    var isVirtual = firstBaseAcceptDeclaration is { IsVirtual: true };
                    var overridePrefix = isVirtual || firstBaseAcceptDeclaration is { IsAbstract: true }
                        ? "override "
                        : "";
                    var baseCall = isVirtual
                        ? "base.Accept(visitor);"
                        : "";

                    var implementationCode = new StringBuilder();
                    implementationCode.AppendLine($$"""
namespace {{implementation.ContainingNamespace.FullName()}}
{
partial class {{implementation.Name}}
{
public {{overridePrefix}}void Accept({{visitorInterfaceType.FullName()}} visitor)
{
{{baseCall}}
visitor.Visit{{leafInterface.Name}}(this);
}
}
}
""");
                    context.NormalizeWhitespaceAndAddSource(
                        $"{implementation.ContainingNamespace.FullName()}.{implementation.Name}.ImplementationPart.g.cs", 
                        implementationCode);
                }
            }
        }
    }
}