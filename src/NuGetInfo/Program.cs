using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetInfo
{
    class NuGetExeDriver
    {
        static void Main(string[] args)
        {
            //var cachePackagesRaw = PackageList.ReadFromCacheDirectory(@"\\Mac\Home\.nuget\packages");
            //var cacheText = cachePackagesRaw.SaveToText();
            //File.WriteAllText("CachePackages.txt", cacheText);
            var cacheText = File.ReadAllText("CachePackages.txt");
            var cachePackages = PackageList.FromText(cacheText);

            var nugetConfig = @"C:\vsmac\nuget.config";
            var packageSources = ParsePackageSources(nugetConfig)
                //.Where(IncludePackageSource)
                .ToArray();

            //MapPackages(cachePackages, packageSources).Wait();

            var packageId = "Microsoft.VisualStudio.CodingConventions";
            var version = "1.1.20180528.2";
            FindPackageSourcesWithPackage(packageSources, packageId, version);

            //CallNuGetExe(packageSources, packageId);

            FlushOutput();
        }

        public static async Task MapPackages(PackageList cachePackages, string[] packageSources)
        {
            List<(string packageId, string version, string source)> mapping = new List<(string packageId, string version, string source)>();
            Dictionary<(string packageId, string version), List<string>> sourcesForPackage = new Dictionary<(string packageId, string version), List<string>>();
            PackageList packagesNotFound = new PackageList();
            Dictionary<string, PackageList> uniquePackagesInSource = new Dictionary<string, PackageList>();

            void ClaimPackageVersion(string packageId, string version, string packageSource)
            {
                mapping.Add((packageId, version, packageId));
                var key = (packageId, version);
                if (!sourcesForPackage.TryGetValue(key, out var bucket))
                {
                    bucket = new List<string>();
                    sourcesForPackage[key] = bucket;
                }

                bucket.Add(packageSource);
            }

            foreach (var source in packageSources)
            {
                var repository = NuGetAPI.GetSourceRepository(source);
                var listApi = await repository.GetResourceAsync<ListResource>();
                var packageExistApi = await repository.GetResourceAsync<FindPackageByIdResource>();

                HashSet<string> idsInSource = null;

                if (listApi != null && IncludePackageSource(source))
                {
                    var idsInSourceResult = await listApi.ListAsync(
                        null,
                        prerelease: true,
                        allVersions: false,
                        includeDelisted: true,
                        NullLogger.Instance,
                        CancellationToken.None);

                    var enumerator = idsInSourceResult.GetEnumeratorAsync();

                    idsInSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while (await enumerator.MoveNextAsync())
                    {
                        var current = enumerator.Current;
                        idsInSource.Add(current.Identity.Id);
                    }
                }

                foreach (var packageIds in cachePackages.Packages)
                {
                    if (idsInSource != null && !idsInSource.Contains(packageIds.Key))
                    {
                        continue;
                    }

                    var packageId = packageIds.Key;
                    foreach (var version in packageIds.Value.OrderBy(s => s))
                    {
                        var exists = await packageExistApi.DoesPackageExistAsync(packageId, NuGetVersion.Parse(version), NuGetAPI.Cache, NullLogger.Instance, CancellationToken.None);
                        if (exists)
                        {
                            ClaimPackageVersion(packageId, version, source);
                        }
                    }
                }
            }

            foreach (var kvp in sourcesForPackage)
            {
                if (kvp.Value.Count == 0)
                {
                    packagesNotFound.Add(kvp.Key.packageId, kvp.Key.version);
                    Output($"Package not found in any of the sources: {kvp.Key.packageId} {kvp.Key.version}");
                }
                else if (kvp.Value.Count == 1)
                {
                    var singleSource = kvp.Value[0];
                    if (!uniquePackagesInSource.TryGetValue(singleSource, out var bucket))
                    {
                        bucket = new PackageList();
                        uniquePackagesInSource[singleSource] = bucket;
                    }

                    bucket.Add(kvp.Key.packageId, kvp.Key.version);
                }
            }

            foreach (var source in packageSources)
            {
                if (!uniquePackagesInSource.ContainsKey(source))
                {
                    Output($"Redundant package source: {source}");
                }
            }
        }

        private static void FindPackageSourcesWithPackage(string[] packageSources, string packageId, string version)
        {
            foreach (var source in packageSources)
            {
                bool exists = NuGetAPI.DoesPackageExist(source, packageId, version).Result;
                if (!exists)
                {
                    continue;
                }

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

        private static readonly StringBuilder sb = new StringBuilder();

        private static void FlushOutput()
        {
            var text = sb.ToString();
            File.WriteAllText("log.txt", text);
        }

        private static void Output(string line)
        {
            Console.WriteLine(line);
            sb.AppendLine(line);
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
