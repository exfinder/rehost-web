namespace System.Web.Util {
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;

    // Portable replacement for AppDomain private-path probing of the application 'bin'
    // directory. Runtime-owned assemblies resolve first and win, reproducing the .NET
    // Framework precedence where the GAC beat 'bin'.
    internal static class BinAssemblyResolver {
        private static int _installed;
        private static string _binDirectory;

        internal static void Install(string binDirectory) {
            if (Interlocked.CompareExchange(ref _installed, 1, 0) != 0) {
                return;
            }

            _binDirectory = binDirectory;
            AssemblyLoadContext.Default.Resolving += Resolve;
        }

        private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name) {
            if (name.Name == null) {
                return null;
            }

            var path = Path.Combine(_binDirectory, name.Name + ".dll");

            if (!File.Exists(path)) {
                WebFormsRuntimeEventSource.Log.BinAssemblyResolution("absent", name.FullName);
                return null;
            }

            var assembly = context.LoadFromAssemblyPath(path);
            WebFormsRuntimeEventSource.Log.BinAssemblyResolution("loaded", name.FullName);
            return assembly;
        }
    }
}
