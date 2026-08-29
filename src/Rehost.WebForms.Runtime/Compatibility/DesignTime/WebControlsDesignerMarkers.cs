// Compile-time shapes for the System.Design data-bound control designers.
// Rehost.WebForms does not support Windows design-time functionality.
namespace System.Web.UI.Design.WebControls
{
    using System;
    using System.ComponentModel.Design;
    using System.Web.UI.Design;

    public class DataBoundControlDesigner : ControlDesigner
    {
        public string DataSourceID { get; set; }

        public string DataMember { get; set; }

        protected virtual bool CanRefreshSchema
        {
            get { return false; }
        }

        protected virtual void OnSchemaRefreshed()
        {
        }
    }

    public class ListControlDesigner : DataBoundControlDesigner
    {
        public string DataTextField { get; set; }

        public string DataValueField { get; set; }
    }

    public class ListControlDataBindingHandler : DataBindingHandler
    {
    }

    public class BaseDataListDesigner : DataBoundControlDesigner
    {
    }
}
