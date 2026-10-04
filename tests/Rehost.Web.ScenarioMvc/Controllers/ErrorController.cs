using System.Web.Mvc;

namespace Rehost.Web.ScenarioMvc.Controllers;

public class ErrorController : Controller
{
    public ActionResult Index() => throw new InvalidOperationException("mvc-fixture-failure");
}
