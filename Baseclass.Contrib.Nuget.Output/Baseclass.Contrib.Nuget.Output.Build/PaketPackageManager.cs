using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public class PaketPackageManager : IPackageManager
    {
        private readonly string projectDirectory;
        private readonly string paketPath;
        
        public PaketPackageManager(string projectDirectory)
        {
            var paketPath = projectDirectory;

            while (!File.Exists(Path.Combine(paketPath, "paket.dependencies")))
                paketPath = Path.Combine(paketPath, "../");
                        
            this.projectDirectory = projectDirectory;
            this.paketPath = paketPath;
        }

        public bool TryGetUsedPackagesDependendingOnNugetOutput(out ITaskItem[] packages)
        {
            var packagesDependendingOnNugetOutput = GetPackagesDependendingOnNugetOutput().ToArray();
            var packagesUsedByProject = GetPackagesUsedByProject().ToArray();

            packages = packagesDependendingOnNugetOutput.Where(package => packagesUsedByProject.Contains(package))
                .Select(id => Path.Combine(paketPath, "packages", id, id)) // CollectNugetOutputFiles target expects directory with file name
                .Select(p => (ITaskItem) new TaskItem(p))
                .ToArray();
            return true;
        }

        private IEnumerable<string> GetPackagesUsedByProject()
        {
            return File.ReadAllLines(Path.Combine(projectDirectory, "paket.references"));
        }

        private IEnumerable<string> GetPackagesDependendingOnNugetOutput()
        {
            var dependencies = File.ReadAllLines(Path.Combine(paketPath, "paket.lock"))
                .Where(line => line.StartsWith("    "));

            string currentDirectDependency = null;
            foreach (var dependency in dependencies)
                if (!IsTransativeDependency(dependency))
                    currentDirectDependency = dependency;
                else if (dependency.Contains("Baseclass.Contrib.Nuget.Output"))
                {
                    var trimmedDependendency = currentDirectDependency.Trim();
                    yield return trimmedDependendency.Substring(0, trimmedDependendency.IndexOf(" "));
                }
        }

        private bool IsTransativeDependency(string dependency)
        {
            return dependency.StartsWith("      ");
        }
    }
}