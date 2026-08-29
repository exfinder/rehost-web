// Compile-time shapes for legacy Web Forms designer-only branches.
// Rehost.WebForms does not support Windows design-time functionality.
namespace System.Web.UI.Design
{
    using System;
    using System.Configuration;
    using System.Drawing.Design;

    public interface IProjectItem
    {
        string AppRelativeUrl { get; }

        string PhysicalPath { get; }
    }

    public interface IWebApplication : IServiceProvider
    {
        IProjectItem RootProjectItem { get; }

        Configuration OpenWebConfiguration(bool isReadOnly);
    }

    public abstract class WebFormsReferenceManager
    {
        internal abstract Type GetType(string tagPrefix, string typeName);
    }

    public abstract class WebFormsRootDesigner
    {
        public virtual string DocumentUrl
        {
            get { return String.Empty; }
        }

        internal abstract WebFormsReferenceManager ReferenceManager { get; }

        public virtual string ResolveUrl(string relativeUrl)
        {
            return relativeUrl;
        }
    }

    public class UrlEditor : UITypeEditor
    {
        protected virtual string Caption
        {
            get { return String.Empty; }
        }

        protected virtual string Filter
        {
            get { return String.Empty; }
        }

        protected virtual UrlBuilderOptions Options
        {
            get { return UrlBuilderOptions.None; }
        }
    }

    public class ImageUrlEditor : UrlEditor
    {
    }

    [Flags]
    public enum UrlBuilderOptions
    {
        None = 0,
        NoAbsolute = 1,
    }
}
