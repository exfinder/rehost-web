using System;
using System.Web.Http;

public class PingModel
{
    public string Name { get; set; }
}

public class PingController : ApiController
{
    public object Get(string fail = null)
    {
        if (fail == "1")
        {
            throw new InvalidOperationException("probe-failure");
        }

        return new { pong = true };
    }

    public object Post(PingModel model)
    {
        return new { echo = model == null ? null : model.Name };
    }
}
