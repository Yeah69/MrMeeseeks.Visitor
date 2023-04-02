using MrMeeseeks.Visitor;
using MrMeeseeks.Visitor.Sample;

[assembly:VisitorInterfacePair(typeof(IVisitor), typeof(IElement))]

namespace MrMeeseeks.Visitor.Sample;

public partial interface IVisitor { }

public partial interface IElement { }

public interface IElementA : IElement { }

public partial class ElementA : IElementA { }

public interface IElementB : IElement { }

public partial class ElementB : IElementB { }

public interface IElementC : IElement { }

public partial class ElementC : IElementC { }