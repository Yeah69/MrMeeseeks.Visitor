using System;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local

namespace MrMeeseeks.Visitor;
/// <summary>
/// 
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