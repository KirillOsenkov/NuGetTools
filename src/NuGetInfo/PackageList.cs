using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System;
using Bucket = System.Collections.Generic.HashSet<string>;

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

        public void Add(string packageId, string version)
        {
            if (!packageVersions.TryGetValue(packageId, out var bucket))
            {
                bucket = new Bucket();
                packageVersions[packageId] = bucket;
            }

            bucket.Add(version);
        }
    }
}