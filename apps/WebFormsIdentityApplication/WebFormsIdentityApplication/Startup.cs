using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(WebFormsIdentityApplication.Startup))]
namespace WebFormsIdentityApplication
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {
            ConfigureAuth(app);
        }
    }
}
