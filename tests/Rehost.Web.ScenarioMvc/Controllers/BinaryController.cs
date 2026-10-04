using System.Data.Linq;
using System.Web.Mvc;
using Rehost.Web.ScenarioMvc.Models;

namespace Rehost.Web.ScenarioMvc.Controllers;

public class BinaryController : Controller
{
    public ActionResult Index() => View(new PayloadModel { Payload = new Binary([0x00, 0x01, 0xFE, 0xFF, 0x2B, 0x2F]) });

    [HttpPost]
    public ActionResult Index(PayloadModel model) =>
        Content(model.Payload == null ? "null" : Convert.ToHexString(model.Payload.ToArray()), "text/plain");

    public ActionResult State() => View(new StateModel { Name = "state-name", State = System.Data.EntityState.Modified });
}
