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