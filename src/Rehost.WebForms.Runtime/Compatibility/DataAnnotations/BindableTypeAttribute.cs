using System;

namespace System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class BindableTypeAttribute : Attribute
{
    public BindableTypeAttribute()
    {
        IsBindable = true;
    }

    public bool IsBindable { get; set; }
}
