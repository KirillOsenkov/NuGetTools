using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System;
using Bucket = System.Collections.Generic.HashSet<string>;
using System.Text;
using System.Linq;

namespace NuGetInfo
{
    public class PackageList
    {
        private Dictionary<string, Bucket> packageVersions = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);

        public static PackageList ReadFromCacheDirectory(string rootDirectory)
        {
            var packageList = new PackageList();

            foreach (var packageDirectory in Directory.GetDirectories(rootDirectory))
            {
                foreach (var versionDirectory in Directory.GetDirectories(packageDirectory))
                {
                    if (Directory.GetFiles(versionDirectory, "*.nupkg").Length == 1)
                    {
                        var packageId = Path.GetFileName(packageDirectory);
                        var version = Path.GetFileName(versionDirectory);
                        packageList.Add(packageId, version);
                    }
                }
            }

            return packageList;
        }

        public IEnumerable<KeyValuePair<string, Bucket>> Packages => this.packageVersions;

        public void Add(string packageId, string version)
        {
            if (!packageVersions.TryGetValue(packageId, out var bucket))
            {
                bucket = new Bucket();
                packageVersions[packageId] = bucket;
            }

            bucket.Add(version);
        }

        public string SaveToText()
        {
            var sb = new StringBuilder();

            foreach (var kvp in this.packageVersions.OrderBy(k => k.Key))
            {
                if (kvp.Value.Count == 0)
                {
                    continue;
                }

                foreach (var version in kvp.Value.OrderBy(v => v))
                {
                    sb.AppendLine($"{kvp.Key} {version}");
                }
            }

            return sb.ToString();
        }

        public static PackageList FromText(string text)
        {
            var result = new PackageList();

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                int space = line.IndexOf(' ');
                if (space > 0)
                {
                    string packageId = line.Substring(0, space);
                    string version = line.Substring(space + 1, line.Length - space - 1);
                    result.Add(packageId, version);
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"{packageVersions.Count} package ids";
        }
    }
}