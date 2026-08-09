using Microsoft.AspNet.FriendlyUrls;
using System.Runtime.CompilerServices;
using System.Web;

[assembly: PreApplicationStartMethod(typeof(PreApplicationStartCode), nameof(PreApplicationStartCode.Start))]
[assembly: InternalsVisibleTo("Rehost.WebForms.FriendlyUrls.Tests")]
