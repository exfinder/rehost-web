// Compile-time marker for legacy EditorAttribute metadata.
// Rehost.WebForms does not support Windows design-time functionality.
namespace System.Drawing.Design
{
    internal sealed class UITypeEditor
    {
        private UITypeEditor()
        {
        }
    }
}
