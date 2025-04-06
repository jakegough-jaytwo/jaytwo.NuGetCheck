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

namespace jaytwo.NuGetCheck.GlobalTool;

public class NugetVersionSource : INugetVersionSource
{
    private readonly string _directory;

    public NugetVersionSource()
        : this(Environment.CurrentDirectory)
    {
    }

    internal NugetVersionSource(string directory)
    {
        _directory = directory;
    }

    public async Task<IList<NuGetVersion>> GetPackageVersionsAsync(string packageId)
    {
        var repo = GetRepository();
        var findResource = await repo.GetResourceAsync<FindPackageByIdResource>();
        var packageVersions = await findResource.GetAllVersionsAsync(
            packageId,
            new SourceCacheContext(),
            NullLogger.Instance,
            CancellationToken.None);

        var result = packageVersions
            .OrderByDescending(x => x)
            .ToList();

        return result;
    }

    private SourceRepository GetRepository()
    {
        var packageSourceProvider = new PackageSourceProvider(Settings.LoadDefaultSettings(_directory));
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());
        var repos = sourceRepositoryProvider.GetRepositories();
        var result = repos.Single(x => x.PackageSource.Name == "nuget.org");
        return result;
    }
}
