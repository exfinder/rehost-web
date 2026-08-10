// System.Drawing.Common is Windows-gated, and the real ToolboxBitmapAttribute
// reaches GDI+ from its type initializer, which the page parser triggers.
namespace System.Drawing
{
    using System;
    using System.ComponentModel;

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ToolboxBitmapAttribute : Attribute
    {
        public ToolboxBitmapAttribute(Type t)
        {
        }

        public ToolboxBitmapAttribute(Type t, string name)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class BitmapSuffixInSatelliteAssemblyAttribute : Attribute
    {
    }

    internal class FontConverter
    {
        internal sealed class FontNameConverter : TypeConverter
        {
        }
    }
}
