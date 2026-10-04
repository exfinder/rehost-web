using System.Web.Mvc;

namespace Rehost.Web.ScenarioMvc.Areas.Admin.Controllers;

public class DashboardController : Controller
{
    public ActionResult Index() => View();
}
