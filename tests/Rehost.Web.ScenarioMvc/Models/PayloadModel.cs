using System.Data.Linq;
using System.Web.Mvc;

namespace Rehost.Web.ScenarioMvc.Models;

public class PayloadModel
{
    [HiddenInput(DisplayValue = false)]
    public Binary? Payload { get; set; }
}
