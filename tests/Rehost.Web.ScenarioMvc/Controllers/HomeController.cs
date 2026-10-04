using System.Web.Mvc;

namespace Rehost.Web.ScenarioMvc.Controllers;

public class HomeController : Controller
{
    public ActionResult Index()
    {
        ViewBag.Title = "Home";
        return View();
    }

    public ActionResult Cached() => View();

    [ChildActionOnly]
    [OutputCache(Duration = 3600)]
    public ActionResult Stamp() => Content(Guid.NewGuid().ToString("N"));
}
