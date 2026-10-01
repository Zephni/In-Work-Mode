using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace WorkMode
{
    internal static class EmbeddedRuntime
    {
        private const string RuntimeResource = "WorkMode.Resources.Runtime.zip";
        private const string CompleteMarker = ".complete";
        private static string _runtimeDirectory;

        public static string EditorDirectory
        {
            get { return Path.Combine(_runtimeDirectory, "editor"); }
        }

        public static void Initialize()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string buildId = assembly.ManifestModule.ModuleVersionId.ToString("N");
            string runtimeRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkMode", "Runtime");
            _runtimeDirectory = Path.Combine(runtimeRoot, buildId);

            Directory.CreateDirectory(runtimeRoot);
            using (var mutex = new Mutex(false, "Local\\WorkModeRuntime-" + buildId))
            {
                bool ownsMutex = false;
                try
                {
                    try { ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(30)); }
                    catch (AbandonedMutexException) { ownsMutex = true; }

                    if (!ownsMutex)
                        throw new TimeoutException("Timed out preparing the application runtime.");

                    ExtractRuntime(assembly);
                }
                finally
                {
                    if (ownsMutex) mutex.ReleaseMutex();
                }
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            SetDllDirectory(_runtimeDirectory);
        }

        private static void ExtractRuntime(Assembly assembly)
        {
            string markerPath = Path.Combine(_runtimeDirectory, CompleteMarker);
            if (File.Exists(markerPath)) return;

            if (Directory.Exists(_runtimeDirectory))
                Directory.Delete(_runtimeDirectory, true);
            Directory.CreateDirectory(_runtimeDirectory);

            string archivePath = Path.Combine(_runtimeDirectory, "runtime.zip");
            using (Stream source = assembly.GetManifestResourceStream(RuntimeResource))
            {
                if (source == null)
                    throw new InvalidOperationException("The embedded application runtime is missing.");

                using (var destination = File.Create(archivePath))
                    source.CopyTo(destination);
            }

            ZipFile.ExtractToDirectory(archivePath, _runtimeDirectory);
            File.Delete(archivePath);
            File.WriteAllText(markerPath, string.Empty);
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            if (name != "Microsoft.Web.WebView2.Core" &&
                name != "Microsoft.Web.WebView2.WinForms")
                return null;

            string path = Path.Combine(_runtimeDirectory, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string pathName);
    }
}