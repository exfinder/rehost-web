// Compile-time shapes for the System.Design designer-action and collection-editor
// closure. Rehost.Web does not support Windows design-time functionality.
namespace System.ComponentModel.Design
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Drawing.Design;

    public abstract class DesignerActionItem
    {
        protected DesignerActionItem(string displayName, string category, string description)
        {
            DisplayName = displayName;
            Category = category;
            Description = description;
        }

        public string Category { get; private set; }

        public string Description { get; private set; }

        public string DisplayName { get; private set; }

        public IDictionary Properties
        {
            get { return _properties ?? (_properties = new Hashtable()); }
        }

        public bool ShowInSourceView { get; set; }

        private IDictionary _properties;
    }

    public sealed class DesignerActionPropertyItem : DesignerActionItem
    {
        public DesignerActionPropertyItem(string memberName, string displayName)
            : this(memberName, displayName, null, null)
        {
        }

        public DesignerActionPropertyItem(string memberName, string displayName, string category)
            : this(memberName, displayName, category, null)
        {
        }

        public DesignerActionPropertyItem(string memberName, string displayName, string category, string description)
            : base(displayName, category, description)
        {
            MemberName = memberName;
        }

        public string MemberName { get; private set; }

        public IComponent RelatedComponent { get; set; }
    }

    public sealed class DesignerActionMethodItem : DesignerActionItem
    {
        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName)
            : this(actionList, memberName, displayName, null, null)
        {
        }

        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName, string category)
            : this(actionList, memberName, displayName, category, null)
        {
        }

        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName, bool includeAsDesignerVerb)
            : this(actionList, memberName, displayName, null, null)
        {
            IncludeAsDesignerVerb = includeAsDesignerVerb;
        }

        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName, string category, bool includeAsDesignerVerb)
            : this(actionList, memberName, displayName, category, null)
        {
            IncludeAsDesignerVerb = includeAsDesignerVerb;
        }

        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName, string category, string description, bool includeAsDesignerVerb)
            : this(actionList, memberName, displayName, category, description)
        {
            IncludeAsDesignerVerb = includeAsDesignerVerb;
        }

        public DesignerActionMethodItem(DesignerActionList actionList, string memberName, string displayName, string category, string description)
            : base(displayName, category, description)
        {
            ActionList = actionList;
            MemberName = memberName;
        }

        public DesignerActionList ActionList { get; private set; }

        public bool IncludeAsDesignerVerb { get; private set; }

        public string MemberName { get; private set; }

        public IComponent RelatedComponent { get; set; }

        public void Invoke()
        {
        }
    }

    public sealed class DesignerActionHeaderItem : DesignerActionTextItem
    {
        public DesignerActionHeaderItem(string displayName)
            : base(displayName, displayName)
        {
        }

        public DesignerActionHeaderItem(string displayName, string category)
            : base(displayName, category)
        {
        }
    }

    public class DesignerActionTextItem : DesignerActionItem
    {
        public DesignerActionTextItem(string displayName, string category)
            : base(displayName, category, null)
        {
        }
    }

    public class DesignerActionItemCollection : CollectionBase
    {
        public DesignerActionItem this[int index]
        {
            get { return (DesignerActionItem)List[index]; }
            set { List[index] = value; }
        }

        public int Add(DesignerActionItem value)
        {
            return List.Add(value);
        }

        public bool Contains(DesignerActionItem value)
        {
            return List.Contains(value);
        }

        public void CopyTo(DesignerActionItem[] array, int index)
        {
            List.CopyTo(array, index);
        }

        public int IndexOf(DesignerActionItem value)
        {
            return List.IndexOf(value);
        }

        public void Insert(int index, DesignerActionItem value)
        {
            List.Insert(index, value);
        }

        public void Remove(DesignerActionItem value)
        {
            List.Remove(value);
        }
    }

    public class DesignerActionList
    {
        public DesignerActionList(IComponent component)
        {
            Component = component;
        }

        public virtual bool AutoShow { get; set; }

        public IComponent Component { get; private set; }

        public object GetService(Type serviceType)
        {
            return Component != null && Component.Site != null
                ? Component.Site.GetService(serviceType)
                : null;
        }

        public virtual DesignerActionItemCollection GetSortedActionItems()
        {
            return new DesignerActionItemCollection();
        }
    }

    public class DesignerActionListCollection : CollectionBase
    {
        public DesignerActionList this[int index]
        {
            get { return (DesignerActionList)List[index]; }
            set { List[index] = value; }
        }

        public int Add(DesignerActionList value)
        {
            return List.Add(value);
        }

        public void AddRange(DesignerActionList[] value)
        {
            foreach (var item in value)
            {
                Add(item);
            }
        }

        public void AddRange(DesignerActionListCollection value)
        {
            foreach (DesignerActionList item in value)
            {
                Add(item);
            }
        }

        public bool Contains(DesignerActionList value)
        {
            return List.Contains(value);
        }

        public void CopyTo(DesignerActionList[] array, int index)
        {
            List.CopyTo(array, index);
        }

        public int IndexOf(DesignerActionList value)
        {
            return List.IndexOf(value);
        }

        public void Insert(int index, DesignerActionList value)
        {
            List.Insert(index, value);
        }

        public void Remove(DesignerActionList value)
        {
            List.Remove(value);
        }
    }

    public class CollectionEditor : UITypeEditor
    {
        public CollectionEditor(Type type)
        {
            CollectionType = type;
        }

        protected Type CollectionType { get; private set; }

        protected virtual Type CollectionItemType
        {
            get { return typeof(object); }
        }

        protected virtual bool CanSelectMultipleInstances()
        {
            return true;
        }

        protected virtual object CreateInstance(Type itemType)
        {
            return Activator.CreateInstance(itemType);
        }

        protected virtual Type[] CreateNewItemTypes()
        {
            return new[] { CollectionItemType };
        }

        protected virtual string GetDisplayText(object value)
        {
            return value == null ? String.Empty : value.ToString();
        }
    }

    public sealed class MultilineStringEditor : UITypeEditor
    {
    }
}
