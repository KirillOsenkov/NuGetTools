using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace NuGetInfo
{
    class NuGetExeDriver
    {
        static void Main(string[] args)
        {
            var cachePackages = PackageList.ReadFromCacheDirectory(@"\\Mac\Home\.nuget\packages");

            var nugetConfig = @"C:\vsmac\nuget.config";
            var packageSources = ParsePackageSources(nugetConfig)
                .Where(IncludePackageSource)
                .ToArray();

            MapPackages(cachePackages, packageSources);

            //var packageId = "Microsoft.VisualStudio.Utilities";
            //var version = "16.7";
            //FindPackageSourcesWithPackage(packageSources, packageId, version);

            //CallNuGetExe(packageSources, packageId);
        }

        public static void MapPackages(PackageList cachePackages, string[] packageSources)
        {

        }

        private static void FindPackageSourcesWithPackage(string[] packageSources, string packageId, string version)
        {
            foreach (var source in packageSources)
            {
                var versions = NuGetAPI.GetPackageVersions(source, packageId).Result.Where(v => v.ToString().Contains(version)).ToArray();
                if (versions.Any())
                {
                    Output(source);
                    foreach (var v in versions)
                    {
                        Output(v.ToString());
                    }
                }
            }
        }

        public static string[] excludeSources = new[]
        {
            "nuget.org",
            "myget",
            "dotnetfeed.blob.core.windows.net",
            "ci.appveyor.com"
        };

        public static bool IncludePackageSource(string packageSource)
        {
            foreach (var exclude in excludeSources)
            {
                if (packageSource.Contains(exclude, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CallNuGetExe(string[] packageSources, string packageId)
        {
            var nugetExe = @"C:\Dropbox\MS\Tools\NuGet.exe";

            var lineBreaks = new char[] { '\r', '\n' };

            foreach (var source in packageSources)
            {
                string[] lines = GetNuGetOutput(nugetExe, lineBreaks, source, allVersions: false)
                    .Where(l => l.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (!lines.Any())
                {
                    continue;
                }

                lines = GetNuGetOutput(nugetExe, lineBreaks, source, allVersions: true)
                    .Where(l => l.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                bool first = true;
                foreach (var line in lines)
                {
                    if (line.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (first)
                        {
                            first = false;
                            Output("=========");
                            Output(source);
                            Output("=========");
                        }

                        Output("    " + line);
                    }
                }
            }
        }

        private static string[] GetNuGetOutput(string nugetExe, char[] lineBreaks, string source, bool allVersions = false)
        {
            var sourceCache = GetSourceCacheFilePath(source, allVersions);
            if (File.Exists(sourceCache))
            {
                return File.ReadAllLines(sourceCache);
            }

            var allVersionsText = allVersions ? " -AllVersions" : "";
            var arguments = $"list -Source {source}{allVersionsText} -Prerelease";

            var psi = new ProcessStartInfo(nugetExe, arguments);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var sb = new StringBuilder();

            var process = Process.Start(psi);
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    sb.AppendLine(e.Data);
                }
            };
            process.BeginOutputReadLine();
            process.WaitForExit();
            var output = sb.ToString();
            var lines = output.Split(lineBreaks, StringSplitOptions.RemoveEmptyEntries);

            File.WriteAllLines(sourceCache, lines);
            return lines;
        }

        private static string GetSourceCacheFilePath(string source, bool allVersions)
        {
            source = source.Replace("https://", "");
            source = source.Replace("http://", "");
            source = source.Replace("pkgs.dev.azure.com", "");
            source = source.Replace("pkgs.visualstudio.com", "");
            source = source.Replace("DefaultCollection", "");
            source = source.Replace("_packaging", "");
            source = source.Replace("/", "-");
            source = source.Replace("index.json", "");
            source = source.Replace(".", "");
            source = source.Replace("api", "");
            source = source.Replace("nuget", "");
            source = source.Replace("v3", "");
            source = source.Replace("v2", "");
            source = source.Replace("--", "-");
            source = source.Trim('-');
            if (source.Length > 30)
            {
                source = source.Substring(0, 30);
            }

            if (allVersions)
            {
                source += "-allVersions";
            }

            source += ".txt";
            source = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), source);
            return source;
        }

        private static void Output(string line)
        {
            Console.WriteLine(line);
        }

        private static IEnumerable<string> ParsePackageSources(string nugetConfig)
        {
            var xml = XDocument.Load(nugetConfig);
            var sources = xml.Root.Element("packageSources").Elements("add");
            foreach (var source in sources)
            {
                var valueAttribute = source.Attribute("value").Value;
                yield return valueAttribute;
            }
        }
    }
}
