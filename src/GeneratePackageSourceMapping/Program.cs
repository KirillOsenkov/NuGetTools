using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class GeneratePackageSourceMapping
{
    static void Main(string[] args)
    {
        new GeneratePackageSourceMapping().DoWork();
    }

    private void DoWork()
    {
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var packages = Path.Combine(userDirectory, ".nuget", "packages");
        var packageDirectories = Directory.GetDirectories(packages);
        foreach (var packageDirectory in packageDirectories)
        {
            var packageId = Path.GetFileName(packageDirectory);

            var versions = Directory.GetDirectories(packageDirectory);
            if (versions.Length == 0)
            {
                continue;
            }

            var versionDirectory = versions.Last();
            var metadataFile = Path.Combine(versionDirectory, ".nupkg.metadata");
            if (!File.Exists(metadataFile))
            {
                continue;
            }

            var source = ExtractSource(metadataFile);
            if (source == null)
            {
                continue;
            }

            AddPackageSource(packageId, source);
        }

        var mapping = GetPackageSourceMapping(packageSources);
        Console.WriteLine(mapping);
    }

    private string GetPackageSourceMapping(Dictionary<string, string> packageSources)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"\t<packageSourceMapping>");

        var sources = packageSources.GroupBy(kvp => kvp.Value, kvp => kvp.Key).OrderBy(g => g.Key).ToArray();
        foreach (var source in sources)
        {
            sb.AppendLine($"\t\t<packageSource key=\"{source.Key}\">");
            foreach (var package in source.OrderBy(s => s))
            {
                sb.AppendLine($"\t\t\t<package pattern=\"{package}\" />");
            }

            sb.AppendLine($"\t\t</packageSource>");
        }

        sb.AppendLine($"\t</packageSourceMapping>");

        return sb.ToString();
    }

    private readonly Dictionary<string, string> packageSources = new Dictionary<string, string>();

    private void AddPackageSource(string packageId, string source)
    {
        packageSources[packageId] = source;
    }

    private string ExtractSource(string metadataFile)
    {
        var lines = File.ReadAllLines(metadataFile);
        foreach (var line in lines)
        {
            var prefix = "\"source\": \"";
            var index = line.IndexOf(prefix);
            if (index == -1)
            {
                continue;
            }

            index += prefix.Length;

            var source = line.Substring(index, line.Length - index - 1);
            return source;
        }

        return null;
    }
}