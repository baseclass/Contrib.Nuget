using System;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public class PackageFilterTask : ITask
    {
        [Required]
        public ITaskItem[] NugetPackages { get; set; }

        [Required]
        public string ProjectDirectory { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Output]
        public ITaskItem[] Result { get; private set; }

        public IBuildEngine BuildEngine { get; set; }

        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            var usedPackages = new HashSet<string>(); // packaged used by the project
            try
            {
                var packageConfigPath = Path.Combine(this.ProjectDirectory, "packages.config");
                if(!File.Exists(packageConfigPath))
                {
                    this.LogMessage("Config doesn't exist: {0}", packageConfigPath);
                    packageConfigPath = Path.Combine(this.ProjectDirectory, string.Format("packages.{0}.config", this.ProjectName));
                }

                if (!File.Exists(packageConfigPath))
                {
                    this.LogError("Config doesn't exist: {0}", packageConfigPath);
                    return false;
                }

                this.LogMessage("Reading config: {0}", packageConfigPath);

                var xml = new XmlDocument();
                xml.LoadXml(File.ReadAllText(packageConfigPath));
                var deps = xml.GetElementsByTagName("package");
                foreach (XmlNode dep in deps)
                {
                    if (dep.Attributes == null)
                    {
                        continue;
                    }
                    var id = dep.Attributes.GetNamedItem("id").Value;
                    if ("Baseclass.Contrib.Nuget.Output".Equals(id))
                    {
                        continue;
                    }
                    var version = dep.Attributes.GetNamedItem("version").Value;
                    var s = string.Format("{0}.{1}", id, version);
                    usedPackages.Add(s);
                }
            }
            catch (Exception e)
            {
                this.LogError("Failed to load package files: {0}", e.Message);
                return false;
            }

            var usedNugetPackages = new List<ITaskItem>(); // list of nuget packages used by the project
            try
            {
                foreach (var nugetPackage in this.NugetPackages)
                {
                    var path = nugetPackage.GetMetadata("Fullpath");
                    var parts = path.Split(Path.DirectorySeparatorChar);
                    usedNugetPackages.AddRange(from part in parts where usedPackages.Contains(part) select nugetPackage);
                }
            }
            catch (Exception e)
            {
                this.LogError("Failed to filter nuget specs: {0}", e.Message);
                return false;
            }

            var result = new List<ITaskItem>(); // list of nuget packages used by the project that depends on Baseclass.Contrib.Nuget.Output
            foreach (var nugetPackage in usedNugetPackages)
            {
                var path = nugetPackage.GetMetadata("Fullpath");
                var nupkgpath = Path.GetDirectoryName(path);

                using (var archive = Package.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var nuspec = archive.GetParts().Single(part => part.Uri.ToString().EndsWith(".nuspec"));
                    var nugetSpec = Path.Combine(nupkgpath, Path.GetFileName(nuspec.Uri.ToString()));

                    // use a mutex to ensure that only one process unzip the nuspec
                    // and that one process do not start reading it due to its existence while another one is still writing it.
                    if (!File.Exists(nugetSpec))
                    {
                        var mut = new Mutex(false, "UnzipNuSpec");
                        try
                        {
                            mut.WaitOne();

                            if (!File.Exists(nugetSpec))
                            {
                                // unpack the nuget spec file from the package
                                try
                                {
                                    using (var outputstream = new FileStream(nugetSpec, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
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
                                    {
                                        throw;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            mut.ReleaseMutex();
                        }
                    }

                    var xml = new XmlDocument();
                    xml.LoadXml(File.ReadAllText(nugetSpec));
                    var deps = xml.GetElementsByTagName("dependency");
                    foreach (XmlNode dep in deps)
                    {
                        if (dep.Attributes == null)
                        {
                            continue;
                        }
                        var id = dep.Attributes.GetNamedItem("id").Value;
                        if ("Baseclass.Contrib.Nuget.Output".Equals(id))
                        {
                            result.Add(nugetPackage);
                            break;
                        }
                    }
                }
            }

            this.Result = result.ToArray();
            return true;
        }
    }
}
