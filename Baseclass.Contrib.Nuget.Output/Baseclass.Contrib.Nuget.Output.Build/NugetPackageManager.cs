using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public enum NugetPackageSource
    {
        PackagesConfig,
        ProjectFile
    }

    public class NugetPackageManager : IPackageManager
    {
        private readonly string projectDirectory;
        private readonly string projectFullPath;
        private readonly string projectName;
        private readonly string solutionPath;
        private readonly ITask task;

        public NugetPackageManager(string solutionPath, string projectDirectory, string projectName,
            string projectFullPath, ITask task)
        {
            this.task = task;
            this.solutionPath = solutionPath;
            this.projectDirectory = projectDirectory;
            this.projectName = projectName;
            this.projectFullPath = projectFullPath;
        }

        public bool TryGetUsedPackagesDependendingOnNugetOutput(out ITaskItem[] packages)
        {
            packages = new ITaskItem[0];

            var currentNugetPackageSource = GetNugetPackageSource();

            HashSet<string> usedPackages;
            try
            {
                usedPackages = GetUsedPackages(currentNugetPackageSource);
            }
            catch (Exception e)
            {
                task.LogError("Failed to load package files: {0}", e.Message);

                return false;
            }

            var nugetPackages = GetFilteredProjectNugetPackages(currentNugetPackageSource, usedPackages);

            var usedNugetPackages =
                nugetPackages.Select(path => new TaskItem(path))
                    .ToArray(); // list of nuget packages used by the project

            var result = new List<ITaskItem>();
            // list of nuget packages used by the project that depends on Baseclass.Contrib.Nuget.Output
            foreach (var nugetPackage in usedNugetPackages)
            {
                var path = nugetPackage.GetMetadata("Fullpath");
                var nupkgpath = Path.GetDirectoryName(path);

                using (var archive = Package.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var nuspec = archive.GetParts().Single(part => part.Uri.ToString().EndsWith(".nuspec"));
                    var nuspecFilename = Path.GetFileName(nuspec.Uri.ToString());
                    var nugetSpec = Path.Combine(nupkgpath, nuspecFilename);

                    // use a mutex to ensure that only one process unzip the nuspec
                    // and that one process do not start reading it due to its existence while another one is still writing it.
                    var mut = new Mutex(false, nuspecFilename);
                    var xml = new XmlDocument();
                    try
                    {
                        mut.WaitOne();

                        if (!File.Exists(nugetSpec))
                        {
                            try
                            {
                                using (
                                    var outputstream = new FileStream(nugetSpec, FileMode.Create,
                                        FileAccess.ReadWrite, FileShare.None))
                                {
                                    using (var nspecstream = nuspec.GetStream())
                                    {
                                        nspecstream.CopyTo(outputstream);
                                    }
                                }
                            }
                            catch (IOException)
                            {
                                if (!File.Exists(nugetSpec))
                                    throw;
                            }
                        }

                        xml.Load(nugetSpec);
                    }
                    finally
                    {
                        mut.ReleaseMutex();
                    }
                    
                    var deps = xml.GetElementsByTagName("dependency");
                    foreach (XmlNode dep in deps)
                    {
                        if (dep.Attributes == null)
                            continue;
                        var id = dep.Attributes.GetNamedItem("id").Value;
                        if ("Baseclass.Contrib.Nuget.Output".Equals(id))
                        {
                            result.Add(nugetPackage);
                            break;
                        }
                    }
                }
            }

            packages = result.ToArray();
            return true;
        }


        private IEnumerable<string> GetFilteredProjectNugetPackages(NugetPackageSource currentNugetPackageSource,
            HashSet<string> usedNugetPackages)
        {
            string packagesPath;
            switch (currentNugetPackageSource)
            {
                case NugetPackageSource.PackagesConfig:
                    packagesPath = GetSolutionPackagePath();
                    break;
                case NugetPackageSource.ProjectFile:
                    packagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
                                   Path.Combine(
                                       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                       @".nuget\packages");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentNugetPackageSource), currentNugetPackageSource,
                        null);
            }

            return Directory.EnumerateFiles(packagesPath, "*.nupkg", SearchOption.AllDirectories)
                .Where(p => usedNugetPackages.Contains(Path.GetFileNameWithoutExtension(p).ToLowerInvariant()))
                .ToArray();
        }

        private string GetSolutionPackagePath()
        {
            var solutionNugetConfig = Path.Combine(solutionPath, "nuget.config");
            if (File.Exists(solutionNugetConfig))
            {
                var config = new XmlDocument();
                config.Load(solutionNugetConfig);

                string repoPath = null;
                var repoPathSetting = config.SelectSingleNode("/configuration/config/add[@key='repositoryPath']");
                if (repoPathSetting != null && repoPathSetting.Attributes != null)
                    repoPath = repoPathSetting.Attributes["value"].Value;

                if (string.IsNullOrEmpty(repoPath))
                {
                    repoPathSetting = config.SelectSingleNode("/configuration/settings/repositoryPath");
                    if (repoPathSetting != null)
                        repoPath = repoPathSetting.InnerText;
                }

                if (!string.IsNullOrEmpty(repoPath))
                {
                    if (Path.IsPathRooted(repoPath))
                        return repoPath;

                    return Path.GetFullPath(Path.Combine(solutionPath, repoPath));
                }
            }

            return Path.Combine(solutionPath, "packages");
        }

        private NugetPackageSource GetNugetPackageSource()
        {
            var packageConfigPaths = new[]
            {
                Path.Combine(projectDirectory, "packages.config"),
                Path.Combine(projectDirectory, $"packages.{projectName}.config")
            };

            return packageConfigPaths.Any(File.Exists)
                ? NugetPackageSource.PackagesConfig
                : NugetPackageSource.ProjectFile;
        }

        private HashSet<string> GetUsedPackages(NugetPackageSource currentNugetPackageSource)
        {
            var usedPackages = new HashSet<string>(); // packaged used by the project

            switch (currentNugetPackageSource)
            {
                case NugetPackageSource.PackagesConfig:
                    var packageConfigPaths = new[]
                    {
                        Path.Combine(projectDirectory, "packages.config"),
                        Path.Combine(projectDirectory, $"packages.{projectName}.config")
                    };

                    foreach (var packageConfigPath in packageConfigPaths)
                        if (TryReadPackagesConfig(packageConfigPath, usedPackages))
                            return usedPackages;
                    break;
                case NugetPackageSource.ProjectFile:
                    ReadPackagesFromProjectFile(projectFullPath, usedPackages);
                    return usedPackages;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            throw new InvalidOperationException("This code isn't possible to be reached");
        }

        private void ReadPackagesFromProjectFile(string projectFilePath, HashSet<string> usedPackages)
        {
            ReadUserPackagesFromXml(projectFilePath, usedPackages, "PackageReference", "Include", "Version");
        }

        private bool TryReadPackagesConfig(string packageConfigPath, HashSet<string> usedPackages)
        {
            if (!File.Exists(packageConfigPath))
                return false;

            ReadUserPackagesFromXml(packageConfigPath, usedPackages, "package", "id", "version");
            return true;
        }

        private void ReadUserPackagesFromXml(string packageConfigPath,
            HashSet<string> usedPackages,
            string elementName,
            string idAttributeName,
            string versionAttributeName)
        {
            task.LogMessage("Reading config: {0}", packageConfigPath);

            var xml = new XmlDocument();
            xml.LoadXml(File.ReadAllText(packageConfigPath));
            var deps = xml.GetElementsByTagName(elementName);
            foreach (XmlNode dep in deps)
            {
                if (dep.Attributes == null)
                    continue;
                var id = dep.Attributes.GetNamedItem(idAttributeName).Value;
                if ("Baseclass.Contrib.Nuget.Output".Equals(id))
                    continue;
                var version = dep.Attributes.GetNamedItem(versionAttributeName)?.Value
                              ?? dep[versionAttributeName]?.InnerText;
                usedPackages.Add($"{id}.{version}".ToLowerInvariant());
            }
        }
    }
}