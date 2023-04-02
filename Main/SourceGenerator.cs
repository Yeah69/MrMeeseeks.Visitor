using System;
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
        var visitorInterfacePairs = context
            .Compilation
            .Assembly
            .GetAttributes()
            .Where(ad => CustomSymbolEqualityComparer.Default.Equals(ad.AttributeClass, visitorInterfacePairAttribute))
            .Where(ad => ad.ConstructorArguments.Length == 2
                         && ad.ConstructorArguments[0].Value is INamedTypeSymbol
                         && ad.ConstructorArguments[1].Value is INamedTypeSymbol)
            .Select(ad => (ad.ConstructorArguments[0].Value as INamedTypeSymbol, ad.ConstructorArguments[1].Value as INamedTypeSymbol))
            .OfType<(INamedTypeSymbol, INamedTypeSymbol)>();

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
partial interface {{elementInterfaceType.FullName()}}
{
void Accept({{visitorInterfaceType.FullName()}} visitor);
}
}
""");
                context.NormalizeWhitespaceAndAddSource(
                    $"{elementInterfaceType.ContainingNamespace.FullName()}.{elementInterfaceType.Name}.ElementPart.g.cs", 
                    partialCode);
            }
        }
    }
}