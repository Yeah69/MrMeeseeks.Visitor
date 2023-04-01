using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
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
        var testCode = new StringBuilder();
        testCode.AppendLine($$"""
namespace Asdf
{
public class Test {}
}
""");
            
        context.AddSource("Asdf.g.cs", testCode.ToString());
        return;
        
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

        var implementationTypeSetCache = new ImplementationTypeSetCache(context, new CheckInternalsVisible(context));
        var interfaceTypesOfCurrentAssembly = implementationTypeSetCache.ForAssembly(context.Compilation.Assembly)
            .Where(nts => nts.TypeKind == TypeKind.Interface)
            .ToList();

        foreach (var (visitorInterfaceType, elementInterfaceType) in visitorInterfacePairs)
        {
            StringBuilder code = new();
            var elementSubInterfaceTypes = interfaceTypesOfCurrentAssembly
                .Where(it =>
                    it.AllDerivedTypes().Any(d => CustomSymbolEqualityComparer.Default.Equals(d, elementInterfaceType)))
                .ToList();

            var visitFunctionsCode = string.Join(Environment.NewLine, elementSubInterfaceTypes
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
            var codeSource = CSharpSyntaxTree
                .ParseText(SourceText.From(code.ToString(), Encoding.UTF8))
                .GetRoot()
                .NormalizeWhitespace()
                .SyntaxTree
                .GetText();
            
            context.AddSource($"{visitorInterfaceType.ContainingNamespace.FullName()}.{visitorInterfaceType.Name}.VisitorPart.g.cs", codeSource);
        }
    }
}