using System;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local

namespace MrMeeseeks.Visitor;

/// <summary>
/// Marks a visitor interface and an element interface as a pair.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class VisitorInterfacePairAttribute : Attribute
{
    public VisitorInterfacePairAttribute(
        Type visitorInterface, 
        Type elementInterface)
    {
    }
}

/// <summary>
/// Marks a visitor interface.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public class VisitorInterfaceAttribute : Attribute
{
    public VisitorInterfaceAttribute(
        Type elementInterface)
    {
    }
}