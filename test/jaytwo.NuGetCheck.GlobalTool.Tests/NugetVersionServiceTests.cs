using System;
using System.Threading.Tasks;
using NuGet.Versioning;
using Xunit;

namespace jaytwo.NuGetCheck.GlobalTool.Tests;

public class NugetVersionServiceTests
{
    [Fact]
    public async Task GetPackageVersionsAsync_returns_versions()
    {
        // arrange
        var versionService = new NugetVersionSource();
        var package = "xunit";

        // act
        var versions = await versionService.GetPackageVersionsAsync(package);

        // assert
        Assert.NotEmpty(versions);
    }
}
