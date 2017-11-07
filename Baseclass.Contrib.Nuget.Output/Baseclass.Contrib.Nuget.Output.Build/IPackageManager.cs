using Microsoft.Build.Framework;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public interface IPackageManager
    {
        bool TryGetUsedPackagesDependendingOnNugetOutput(out ITaskItem[] packages);
    }
}