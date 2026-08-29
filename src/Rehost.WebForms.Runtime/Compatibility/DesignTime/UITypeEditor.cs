// Compile-time marker for legacy EditorAttribute metadata.
// Rehost.WebForms does not support Windows design-time functionality.
namespace System.Drawing.Design
{
    using System;
    using System.ComponentModel;

    public class UITypeEditor
    {
        public virtual object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            return value;
        }

        public virtual UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.None;
        }

        public virtual bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            return false;
        }
    }

    public enum UITypeEditorEditStyle
    {
        None = 1,
        Modal = 2,
        DropDown = 3,
    }
}
