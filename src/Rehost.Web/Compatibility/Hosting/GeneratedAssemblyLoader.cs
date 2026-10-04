namespace System.Web.Util {
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;

    // Every assembly an application supplies or generates enters the process here, into the one
    // load context. Framework had a single AppDomain, so a type compiled against App_Code had the
    // same identity everywhere; separate contexts would give the same type two identities and fail
    // where a generated assembly references another one.
    //
    // Two entry points share one policy. Load is the explicit door taken right after a compile.
    // Resolve serves the runtime's own binding, which no call site can intercept: it fires when
    // generated code touches a type from another generated assembly, and when a preserved build
    // result is loaded by simple name on a later run.
    internal static class GeneratedAssemblyLoader {
        private static int _installed;
        private static string _binDirectory;
        private static string _codegenDirectory;
        private static IReadOnlyList<string> _probingDirectories = [];

        internal static void PublishProbingDirectories(IReadOnlyList<string> probingDirectories) {
            _probingDirectories = probingDirectories;
        }

        internal static void Install(string binDirectory, string codegenDirectory) {
            if (Interlocked.CompareExchange(ref _installed, 1, 0) != 0) {
                return;
            }

            _binDirectory = binDirectory;
            _codegenDirectory = codegenDirectory;
            AssemblyLoadContext.Default.Resolving += Resolve;
        }

        internal static Assembly Load(string path) {
            if (String.IsNullOrEmpty(path)) {
                throw new ArgumentException("A generated assembly path is required.", nameof(path));
            }

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            RehostWebEventSource.Log.AssemblyResolution(AssemblyResolutionOutcome.Compiled, path);
            return assembly;
        }

        // LoadFromAssemblyPath returns the assembly already loaded from a path rather than reading
        // the file again, so compiling over a loaded assembly would silently keep serving the old
        // one. Framework detected the same condition through GetModuleHandle.
        internal static bool IsLoadedFrom(string path) {
            if (String.IsNullOrEmpty(path)) {
                return false;
            }

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return AssemblyLoadContext.Default.Assemblies.Any(
                assembly => !assembly.IsDynamic
                    && assembly.Location.Length > 0
                    && String.Equals(assembly.Location, path, comparison));
        }

        // Generated assemblies are probed before 'bin' because a stale copy under 'bin' must never
        // shadow output this application generation just produced. Runtime-owned assemblies still
        // resolve first and win, reproducing the Framework precedence where the GAC beat both.
        internal static IEnumerable<string> CandidatePaths(
            string binDirectory,
            string codegenDirectory,
            IReadOnlyList<string> probingDirectories,
            AssemblyName name) {
            if (name.Name == null) {
                yield break;
            }

            var fileName = name.Name + ".dll";
            var culture = name.CultureName;

            if (!String.IsNullOrEmpty(codegenDirectory)) {
                // A satellite assembly is emitted into a culture subdirectory of the codegen
                // directory, which is where the resource fallback expects to find it.
                if (!String.IsNullOrEmpty(culture)) {
                    yield return Path.Combine(codegenDirectory, culture, fileName);
                }
                else {
                    yield return Path.Combine(codegenDirectory, fileName);
                }
            }

            if (!String.IsNullOrEmpty(culture)) {
                yield break;
            }

            if (!String.IsNullOrEmpty(binDirectory)) {
                yield return Path.Combine(binDirectory, fileName);
            }

            foreach (var directory in probingDirectories) {
                yield return Path.Combine(directory, fileName);
            }
        }

        // A .delete marker means the assembly is dead and only survives because it could not be
        // deleted while loaded. Loading it would resurrect a build result the cache invalidated.
        internal static bool IsUsable(string path) {
            return File.Exists(path) && !File.Exists(path + ".delete");
        }

        private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name) {
            foreach (var path in CandidatePaths(_binDirectory, _codegenDirectory, _probingDirectories, name)) {
                if (!IsUsable(path)) {
                    continue;
                }

                var assembly = context.LoadFromAssemblyPath(path);
                RehostWebEventSource.Log.AssemblyResolution(AssemblyResolutionOutcome.Loaded, name.FullName);
                return assembly;
            }

            RehostWebEventSource.Log.AssemblyResolution(AssemblyResolutionOutcome.Absent, name.FullName);
            return null;
        }
    }
}
