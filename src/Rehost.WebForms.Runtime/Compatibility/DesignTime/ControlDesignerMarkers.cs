// Compile-time shapes for the System.Design control-designer closure that
// third-party control libraries compile into their runtime assembly.
// Rehost.WebForms does not support Windows design-time functionality; every
// member here stays inert.
namespace System.Web.UI.Design
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Web.UI;

    public class ControlDesigner : IDisposable
    {
        public virtual bool AllowResize
        {
            get { return true; }
        }

        public virtual DesignerActionListCollection ActionLists
        {
            get { return _actionLists ?? (_actionLists = new DesignerActionListCollection()); }
        }

        public virtual IComponent Component { get; private set; }

        public virtual bool DesignTimeHtmlRequiresLoadComplete
        {
            get { return false; }
        }

        public IDictionary DesignerState
        {
            get { return _designerState ?? (_designerState = new Hashtable()); }
        }

        public virtual TemplateGroupCollection TemplateGroups
        {
            get { return new TemplateGroupCollection(); }
        }

        public string ID { get; set; }

        protected WebFormsRootDesigner RootDesigner { get; set; }

        public Control ViewControl
        {
            get { return Component as Control; }
            set { }
        }

        protected virtual bool UsePreviewControl
        {
            get { return false; }
        }

        protected virtual bool Visible
        {
            get { return true; }
        }

        protected bool IsDirty { get; set; }

        protected bool InTemplateMode
        {
            get { return false; }
        }

        public virtual void Initialize(IComponent component)
        {
            Component = component;
        }

        public virtual string GetDesignTimeHtml()
        {
            return String.Empty;
        }

        public virtual string GetDesignTimeHtml(DesignerRegionCollection regions)
        {
            return GetDesignTimeHtml();
        }

        public virtual string GetEditableDesignerRegionContent(EditableDesignerRegion region)
        {
            return String.Empty;
        }

        public virtual void SetEditableDesignerRegionContent(EditableDesignerRegion region, string content)
        {
        }

        public virtual string GetPersistenceContent()
        {
            return null;
        }

        public virtual string GetPersistInnerHtml()
        {
            return null;
        }

        public void UpdateDesignTimeHtml()
        {
        }

        public virtual void OnComponentChanged(object sender, ComponentChangedEventArgs ce)
        {
        }

        public virtual void OnComponentChanging(object sender, ComponentChangingEventArgs ce)
        {
        }

        protected virtual void OnClick(DesignerRegionMouseEventArgs e)
        {
        }

        protected virtual string GetEmptyDesignTimeHtml()
        {
            return CreatePlaceHolderDesignTimeHtml(null);
        }

        protected virtual string GetErrorDesignTimeHtml(Exception e)
        {
            return CreatePlaceHolderDesignTimeHtml(null);
        }

        protected string CreatePlaceHolderDesignTimeHtml()
        {
            return CreatePlaceHolderDesignTimeHtml(null);
        }

        protected string CreatePlaceHolderDesignTimeHtml(string instruction)
        {
            return String.Empty;
        }

        protected virtual void PreFilterProperties(IDictionary properties)
        {
        }

        protected virtual void PostFilterProperties(IDictionary properties)
        {
        }

        protected virtual void PreFilterAttributes(IDictionary attributes)
        {
        }

        protected virtual void PostFilterAttributes(IDictionary attributes)
        {
        }

        protected virtual void PreFilterEvents(IDictionary events)
        {
        }

        protected virtual void PostFilterEvents(IDictionary events)
        {
        }

        protected void SetViewFlags(ViewFlags viewFlags, bool setFlag)
        {
        }

        protected virtual object GetService(Type serviceType)
        {
            return Component != null && Component.Site != null
                ? Component.Site.GetService(serviceType)
                : null;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private DesignerActionListCollection _actionLists;
        private IDictionary _designerState;
    }

    [Flags]
    public enum ViewFlags
    {
        CustomPaint = 1,
        DesignTimeHtmlRequiresLoadComplete = 2,
        TemplateEditing = 4,
    }

    public class CompositeControlDesigner : ControlDesigner
    {
        protected virtual void CreateChildControls()
        {
        }

        protected void RecreateChildControls()
        {
        }
    }

    public class ExtenderControlDesigner : ControlDesigner
    {
    }

    public class TemplatedControlDesigner : ControlDesigner
    {
    }

    public class DesignerRegion
    {
        public static readonly string DesignerRegionAttributeName = "_designerRegion";

        public DesignerRegion()
        {
        }

        public DesignerRegion(ControlDesigner designer, string name)
            : this(designer, name, false)
        {
        }

        public DesignerRegion(ControlDesigner designer, string name, bool selectable)
        {
            Designer = designer;
            Name = name;
            Selectable = selectable;
        }

        public string Description { get; set; }

        public ControlDesigner Designer { get; private set; }

        public bool DisplayName { get; set; }

        public bool EnsureSize { get; set; }

        public bool Highlight { get; set; }

        public string Name { get; private set; }

        public IDictionary Properties
        {
            get { return _properties ?? (_properties = new Hashtable()); }
        }

        public bool Selectable { get; set; }

        public bool Selected { get; set; }

        public string UserData { get; set; }

        private IDictionary _properties;
    }

    public class EditableDesignerRegion : DesignerRegion
    {
        public EditableDesignerRegion()
        {
        }

        public EditableDesignerRegion(ControlDesigner designer, string name)
            : base(designer, name)
        {
        }

        public EditableDesignerRegion(ControlDesigner designer, string name, bool serverControlsOnly)
            : base(designer, name)
        {
            ServerControlsOnly = serverControlsOnly;
        }

        public bool ServerControlsOnly { get; set; }

        public bool SupportsDataBinding { get; set; }
    }

    public class TemplatedEditableDesignerRegion : EditableDesignerRegion
    {
        public TemplatedEditableDesignerRegion(TemplateDefinition templateDefinition)
        {
            TemplateDefinition = templateDefinition;
        }

        public bool IsSingleInstanceTemplate { get; set; }

        public TemplateDefinition TemplateDefinition { get; private set; }
    }

    public class DesignerRegionCollection : IList
    {
        public DesignerRegionCollection()
        {
        }

        public DesignerRegionCollection(ControlDesigner owner)
        {
            Owner = owner;
        }

        public ControlDesigner Owner { get; private set; }

        public int Count
        {
            get { return _regions.Count; }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public DesignerRegion this[int index]
        {
            get { return (DesignerRegion)_regions[index]; }
            set { _regions[index] = value; }
        }

        object IList.this[int index]
        {
            get { return _regions[index]; }
            set { _regions[index] = value; }
        }

        public int Add(DesignerRegion region)
        {
            return _regions.Add(region);
        }

        int IList.Add(object value)
        {
            return _regions.Add(value);
        }

        public void Clear()
        {
            _regions.Clear();
        }

        public bool Contains(DesignerRegion region)
        {
            return _regions.Contains(region);
        }

        bool IList.Contains(object value)
        {
            return _regions.Contains(value);
        }

        public void CopyTo(Array array, int index)
        {
            _regions.CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return _regions.GetEnumerator();
        }

        public int IndexOf(DesignerRegion region)
        {
            return _regions.IndexOf(region);
        }

        int IList.IndexOf(object value)
        {
            return _regions.IndexOf(value);
        }

        public void Insert(int index, DesignerRegion region)
        {
            _regions.Insert(index, region);
        }

        void IList.Insert(int index, object value)
        {
            _regions.Insert(index, value);
        }

        public void Remove(DesignerRegion region)
        {
            _regions.Remove(region);
        }

        void IList.Remove(object value)
        {
            _regions.Remove(value);
        }

        public void RemoveAt(int index)
        {
            _regions.RemoveAt(index);
        }

        private readonly ArrayList _regions = new ArrayList();
    }

    public class DesignerRegionMouseEventArgs : EventArgs
    {
        public DesignerRegionMouseEventArgs(DesignerRegion region, System.Drawing.Point location)
        {
            Region = region;
            Location = location;
        }

        public System.Drawing.Point Location { get; private set; }

        public DesignerRegion Region { get; private set; }
    }

    public class TemplateDefinition : IDisposable
    {
        public TemplateDefinition(ControlDesigner designer, string name, object templatedObject, string templatePropertyName)
            : this(designer, name, templatedObject, templatePropertyName, false)
        {
        }

        public TemplateDefinition(ControlDesigner designer, string name, object templatedObject, string templatePropertyName, bool serverControlsOnly)
        {
            Designer = designer;
            Name = name;
            TemplatedObject = templatedObject;
            TemplatePropertyName = templatePropertyName;
            ServerControlsOnly = serverControlsOnly;
        }

        public bool AllowEditing { get; set; }

        public string Content { get; set; }

        public ControlDesigner Designer { get; private set; }

        public string Name { get; private set; }

        public bool ServerControlsOnly { get; set; }

        public bool SupportsDataBinding { get; set; }

        public object TemplatedObject { get; private set; }

        public string TemplatePropertyName { get; private set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class TemplateGroup
    {
        public TemplateGroup(string groupName)
        {
            GroupName = groupName;
        }

        public TemplateGroup(string groupName, System.Web.UI.WebControls.Style groupStyle)
        {
            GroupName = groupName;
            GroupStyle = groupStyle;
        }

        public string GroupName { get; private set; }

        public System.Web.UI.WebControls.Style GroupStyle { get; private set; }

        public bool IsEmpty
        {
            get { return _definitions.Count == 0; }
        }

        public TemplateDefinition[] Templates
        {
            get { return (TemplateDefinition[])_definitions.ToArray(typeof(TemplateDefinition)); }
        }

        public void AddTemplateDefinition(TemplateDefinition templateDefinition)
        {
            _definitions.Add(templateDefinition);
        }

        private readonly ArrayList _definitions = new ArrayList();
    }

    public class TemplateGroupCollection : IList
    {
        public int Count
        {
            get { return _groups.Count; }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public TemplateGroup this[int index]
        {
            get { return (TemplateGroup)_groups[index]; }
            set { _groups[index] = value; }
        }

        object IList.this[int index]
        {
            get { return _groups[index]; }
            set { _groups[index] = value; }
        }

        public int Add(TemplateGroup group)
        {
            return _groups.Add(group);
        }

        int IList.Add(object value)
        {
            return _groups.Add(value);
        }

        public void AddRange(TemplateGroupCollection groups)
        {
            foreach (TemplateGroup group in groups)
            {
                Add(group);
            }
        }

        public void Clear()
        {
            _groups.Clear();
        }

        public bool Contains(TemplateGroup group)
        {
            return _groups.Contains(group);
        }

        bool IList.Contains(object value)
        {
            return _groups.Contains(value);
        }

        public void CopyTo(Array array, int index)
        {
            _groups.CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return _groups.GetEnumerator();
        }

        public int IndexOf(TemplateGroup group)
        {
            return _groups.IndexOf(group);
        }

        int IList.IndexOf(object value)
        {
            return _groups.IndexOf(value);
        }

        public void Insert(int index, TemplateGroup group)
        {
            _groups.Insert(index, group);
        }

        void IList.Insert(int index, object value)
        {
            _groups.Insert(index, value);
        }

        public void Remove(TemplateGroup group)
        {
            _groups.Remove(group);
        }

        void IList.Remove(object value)
        {
            _groups.Remove(value);
        }

        public void RemoveAt(int index)
        {
            _groups.RemoveAt(index);
        }

        private readonly ArrayList _groups = new ArrayList();
    }

    public static class ControlPersister
    {
        public static string PersistControl(Control control)
        {
            return String.Empty;
        }

        public static string PersistControl(Control control, IDesignerHost host)
        {
            return String.Empty;
        }

        public static string PersistInnerProperties(object component, IDesignerHost host)
        {
            return String.Empty;
        }

        public static string PersistTemplate(ITemplate template, IDesignerHost host)
        {
            return String.Empty;
        }
    }

    public static class ControlParser
    {
        public static Control ParseControl(IDesignerHost designerHost, string controlText)
        {
            return null;
        }

        public static ITemplate ParseTemplate(IDesignerHost designerHost, string templateText)
        {
            return null;
        }
    }

    public class DataBindingHandler
    {
        public virtual void DataBindControl(IDesignerHost designerHost, Control control)
        {
        }
    }
}
