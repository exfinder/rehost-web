using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Rehost.Fixtures.AjaxQuery
{
    public class AjaxQueryPerson
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class AjaxQueryPeopleDataSource : QueryableDataSource
    {
        protected override QueryableDataSourceView CreateQueryableView()
        {
            return new AjaxQueryPeopleView(this, "DefaultView", HttpContext.Current);
        }
    }

    public class AjaxQueryPeopleView : QueryableDataSourceView
    {
        public AjaxQueryPeopleView(AjaxQueryPeopleDataSource owner, string viewName, HttpContext context)
            : base(owner, viewName, context)
        {
        }

        protected override Type EntityType
        {
            get { return typeof(AjaxQueryPerson); }
        }

        protected override object GetSource(QueryContext context)
        {
            List<AjaxQueryPerson> people = new List<AjaxQueryPerson>();
            people.Add(new AjaxQueryPerson { Name = "Ada", Age = 36 });
            people.Add(new AjaxQueryPerson { Name = "Brendan", Age = 29 });
            people.Add(new AjaxQueryPerson { Name = "Grace", Age = 85 });
            people.Add(new AjaxQueryPerson { Name = "Linus", Age = 55 });
            return people.AsQueryable();
        }

        protected override void HandleValidationErrors(
            IDictionary<string, Exception> errors, DataSourceOperation operation)
        {
            foreach (KeyValuePair<string, Exception> error in errors)
            {
                throw error.Value;
            }
        }
    }
}
