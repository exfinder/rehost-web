using System;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Mvc;

[assembly: NeutralResourcesLanguage("en-US")]
[assembly: ComVisible(false)]
[assembly: CLSCompliant(true)]
[assembly: PreApplicationStartMethod(typeof(PreApplicationStartCode), "Start")]
[assembly: TypeForwardedTo(typeof(TagBuilder))]
[assembly: TypeForwardedTo(typeof(TagRenderMode))]
[assembly: TypeForwardedTo(typeof(HttpAntiForgeryException))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationEqualToRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationRangeRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationRegexRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationRemoteRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationRequiredRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationRule))]
[assembly: TypeForwardedTo(typeof(ModelClientValidationStringLengthRule))]
