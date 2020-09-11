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
            var nugetConfig = @"C:\vsmac\nuget.config";
            var packageSources = ParsePackageSources(nugetConfig).ToArray();
            var packageId = "Microsoft.VisualStudio.Utilities";

            var nugetExe = @"C:\Dropbox\MS\Tools\NuGet.exe";

            var lineBreaks = new char[] { '\r', '\n' };

            var excludeSources = new[]
            {
                "nuget.org",
                "myget",
                "dotnetfeed.blob.core.windows.net",
                "ci.appveyor.com"
            };

            foreach (var source in packageSources)
            {
                bool shouldExclude = false;
                foreach (var exclude in excludeSources)
                {
                    if (source.Contains(exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExclude = true;
                        break;
                    }
                }

                if (shouldExclude)
                {
                    continue;
                }

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
