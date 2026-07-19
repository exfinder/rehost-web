// Compile-time shapes for legacy Web Forms designer-only branches.
// Rehost.WebForms does not support Windows design-time functionality.
namespace System.Web.UI.Design
{
    using System;
    using System.Configuration;

    internal interface IWebApplication
    {
        Configuration OpenWebConfiguration(bool isReadOnly);
    }

    internal abstract class WebFormsReferenceManager
    {
        internal abstract Type GetType(string tagPrefix, string typeName);
    }

    internal abstract class WebFormsRootDesigner
    {
        internal abstract WebFormsReferenceManager ReferenceManager { get; }
    }
}
