using Microsoft.Build.Framework;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public class PackageRetrievalTask : ITask
    {
        [Required]
        public string ProjectDirectory { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string ProjectFullPath { get; set; }

        [Required]
        public string SolutionPath { get; set; }

        [Output]
        public ITaskItem[] Result { get; private set; }

        public IBuildEngine BuildEngine { get; set; }

        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            var packageManager = GetPackageManager();

            Result = new ITaskItem[0];
            if (packageManager.TryGetUsedPackagesDependendingOnNugetOutput(out var packages))
            {
                Result = packages;
                return true;
            }

            return false;
        }

        private IPackageManager GetPackageManager()
        {
            var packageManager = new NugetPackageManager(SolutionPath,
                ProjectDirectory,
                ProjectName,
                ProjectFullPath,
                this);

            return packageManager;
        }
    }
}