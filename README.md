# MrMeeseeks.Visitor

If you like the Visitor pattern, but don't like to write the boilerplate code for it, then search no more. This Mr. Meeseeks will help you with that.

This is a source generator which generates the boilerplate code for the [Visitor Pattern](https://en.wikipedia.org/wiki/Visitor_pattern).

## Nuget

The easiest way to use DIE is to get it via nuget. Here is the package page:

[https://www.nuget.org/packages/MrMeeseeks.Visitor/](https://www.nuget.org/packages/MrMeeseeks.Visitor/)

Either search for `MrMeeseeks.Visitor` in the nuget manager of the IDE of your choice.

Or call the following PowerShell command:

```powershell
Install-Package MrMeeseeks.Visitor
```


Alternatively, you can use `dotnet`:

```sh
dotnet add [your project] package MrMeeseeks.Visitor
```

Or manually add the package reference to the target `.csproj`:

```xml
<PackageReference Include="MrMeeseeks.Visitor" Version="[preferrably the current version]" />
```

## Usage

The usage is very simple. You just need to declare a visitor interface, an base interface for the elements, an interface for each element and an implementation for each element.
Then you just need to add a `VisitorInterfacePair` attribute with the visitor interface and the element interface types as input.
As long as the visitor interface, the element interface and the element implementations are partial, this source generator will generate the boilerplate code for you.

Here is an example of the manual effort:

```csharp
namespace MrMeeseeks.Visitor.Sample;

[VisitorInterface(typeof(IElement))]
public partial interface IVisitor { }

public partial interface IElement { }

public interface IElementA : IElement { }

public partial class ElementA : IElementA { }

public interface IElementB : IElement { }

public partial class ElementB : IElementB { }

public interface IElementC : IElement { }

public partial class ElementC : IElementC { }
```

Here is what will be generated. Visitor interface:

```csharp
namespace MrMeeseeks.Visitor.Sample
{
    partial interface IVisitor
    {
        void VisitIElementA(global::MrMeeseeks.Visitor.Sample.IElementA element);
        void VisitIElementC(global::MrMeeseeks.Visitor.Sample.IElementC element);
        void VisitIElementB(global::MrMeeseeks.Visitor.Sample.IElementB element);
    }
}
```

Element interface:

```csharp
namespace MrMeeseeks.Visitor.Sample
{
    partial interface IElement
    {
        void Accept(global::MrMeeseeks.Visitor.Sample.IVisitor visitor);
    }
}
```

One of the element implementations:

```csharp
namespace MrMeeseeks.Visitor.Sample
{
    partial class ElementA
    {
        public void Accept(global::MrMeeseeks.Visitor.Sample.IVisitor visitor)
        {
            visitor.VisitIElementA(this);
        }
    }
}
```