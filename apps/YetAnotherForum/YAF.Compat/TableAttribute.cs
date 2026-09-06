namespace System.Data.Linq.Mapping;

[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute : Attribute
{
    public string? Name { get; set; }
}
