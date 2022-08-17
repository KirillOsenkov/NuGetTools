using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

class NuGetAPI
{
    public static SourceCacheContext Cache = new SourceCacheContext();

    public static async Task<IEnumerable<NuGetVersion>> GetPackageVersions(string url, string packageId)
    {
        try
        {
            var resource = await GetPackageByIdResource(url);
            if (resource != null)
            {
                return await GetPackageVersions(resource, packageId);
            }
        }
        catch
        {
        }

        return Array.Empty<NuGetVersion>();
    }

    public static async Task<bool> DoesPackageExist(string url, string packageId, string version)
    {
        try
        {
            var resource = await GetPackageByIdResource(url);
            if (resource != null)
            {
                return await resource.DoesPackageExistAsync(
                    packageId,
                    NuGetVersion.Parse(version),
                    Cache,
                    NullLogger.Instance,
                    CancellationToken.None);
            }
        }
        catch
        {
        }

        return false;
    }

    public static async Task<IEnumerable<NuGetVersion>> GetPackageVersions(FindPackageByIdResource resource, string packageId) 
    {
        IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
            packageId,
            Cache,
            NullLogger.Instance,
            CancellationToken.None);

        return versions.ToArray();
    }

    public static SourceRepository GetSourceRepository(string url)
    {
        try
        {
            var packageSource = new PackageSource(url);
            if (url.Contains("devdiv"))
            {
                var pat = GuiLabs.CredentialManager.GetCredentialValue("PAT-Packaging-Read");
                packageSource.Credentials = new PackageSourceCredential(
                    url,
                    username: pat,
                    passwordText: pat,
                    isPasswordClearText: true,
                    validAuthenticationTypesText: null);
            }

            SourceRepository repository = Repository.Factory.GetCoreV3(packageSource);
            return repository;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<FindPackageByIdResource> GetPackageByIdResource(string url)
    {
        var repository = GetSourceRepository(url);
        return await GetPackageByIdResource(repository);
    }

    public static string GetPackageDescription(string url, string packageId, string version)
    {
        var repository = GetSourceRepository(url);
        var resource = GetPackageMetadataResource(repository);
        var metadata = resource.Result.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: true,
            Cache,
            NullLogger.Instance,
            CancellationToken.None).Result;
        var first = metadata.FirstOrDefault();
        var description = first.Description;
        return description;
    }

    public static async Task<PackageMetadataResource> GetPackageMetadataResource(SourceRepository repository)
    {
        try
        {
            var resource = await repository.GetResourceAsync<PackageMetadataResource>();
            return resource;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<FindPackageByIdResource> GetPackageByIdResource(SourceRepository repository)
    {
        try
        {
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            return resource;
        }
        catch
        {
            return null;
        }
    }
}