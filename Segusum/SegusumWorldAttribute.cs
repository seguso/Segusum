using System;

namespace Seg;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SegusumWorldAttribute : Attribute
{
    public SegusumWorldAttribute(string id) => Id = id;
    public string Id { get; }
}
