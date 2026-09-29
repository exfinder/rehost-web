namespace System.Web.Compilation {
    using System;
    using System.Web.Hosting;

    internal sealed class WebReferencesBuildProvider : BuildProvider {
        internal WebReferencesBuildProvider(VirtualDirectory virtualDirectory) {
        }

        public override void GenerateCode(AssemblyBuilder assemblyBuilder) {
            throw Unsupported();
        }

        internal static PlatformNotSupportedException Unsupported() {
            return new PlatformNotSupportedException(
                "Legacy .wsdl and Application_WebReferences proxy generation is not supported in this Rehost.Web iteration. Generate and compile the service client separately.");
        }
    }

    [BuildProviderAppliesTo(BuildProviderAppliesTo.Code)]
    internal sealed class WsdlBuildProvider : BuildProvider {
        public override void GenerateCode(AssemblyBuilder assemblyBuilder) {
            throw WebReferencesBuildProvider.Unsupported();
        }
    }
}
