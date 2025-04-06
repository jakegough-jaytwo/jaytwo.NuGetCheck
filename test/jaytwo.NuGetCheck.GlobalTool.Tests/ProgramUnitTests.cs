using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace jaytwo.NuGetCheck.GlobalTool.Tests;

public class ProgramUnitTests
{
    [Fact]
    public void Run_returns_negative_1_when_no_package_results()
    {
        // arrange
        var packageId = "anything";

        var mockNugetVersionService = new Mock<INugetVersionSource>();
        mockNugetVersionService
            .Setup(x => x.GetPackageVersionsAsync(packageId))
            .ReturnsAsync(new NuGetVersion[] { });

        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);
            var args = new[] { packageId };

            // act
            var result = program.Run(args);

            // assert
            Assert.Equal(-1, result);
            mockNugetVersionService.VerifyAll();
        }
    }

    [Theory]
    [InlineData(new[] { "abc", "-eq", "1.2.3", "--fail-on-match" }, -1, new[] { "1.2.3" })]
    [InlineData(new[] { "abc", "-eq", "1.2.3" }, 0, new[] { "1.2.3" })]
    [InlineData(new[] { "abc", "-eq", "5.5.5", "--fail-on-match" }, 0, new[] { "No results." })]
    [InlineData(new[] { "abc", "-eq", "5.5.5" }, -1, new[] { "No results." })]
    public void Run_returns_expected_exit_code_on_opposite_day(string[] args, int expectedExitCode, string[] expectedVersions)
    {
        // arrange
        var mockNugetVersionService = new Mock<INugetVersionSource>();
        mockNugetVersionService
            .Setup(x => x.GetPackageVersionsAsync("abc"))
            .ReturnsAsync(new NuGetVersion[] { new NuGetVersion("1.2.3") });

        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);

            // act
            var outputCode = program.Run(args);

            // assert
            Assert.Equal(expectedExitCode, outputCode);
            var outputLines = mockStandardOut.ToString().Split("\n").Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim()).ToArray();
            Assert.Equal(expectedVersions, outputLines);
        }
    }

    [Fact]
    public void Run_returns_1_when_invalid_args()
    {
        // arrange
        var mockNugetVersionService = new Mock<INugetVersionSource>();

        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);
            var args = new[] { $"--foo=bar" };

            // act
            var result = program.Run(args);

            // assert
            Assert.Equal(1, result);
        }
    }

    [Fact]
    public void Run_writes_to_console_when_invalid_args()
    {
        // arrange
        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var mockNugetVersionService = new Mock<INugetVersionSource>();
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);
            var args = new[] { $"--foo=bar" };

            // act
            var result = program.Run(args);

            // assert
            Assert.Equal(1, result);

            var output = mockStandardError.ToString();
            Assert.Contains("--foo=bar", output);
        }
    }

    [Fact]
    public void TryLoadNugetVersion_works_on_nupkg()
    {
        // arrange
        var testNupkg = "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme";

        // act
        var result = Program.TryLoadNugetVersion(testNupkg, out var version);

        // assert
        Assert.True(result);
        Assert.Equal(new NuGetVersion("2.1.0-rc1-build3168"), version);
    }

    [Fact]
    public void TryLoadPackageIdentityFromFile_works_on_nupkg()
    {
        // arrange
        var testNupkg = "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme";

        // act
        var result = Program.TryLoadPackageIdentityFromFile(testNupkg, out var packageIdentity);

        // assert
        Assert.True(result);
        Assert.Equal("xunit", packageIdentity!.Id);
        Assert.True(packageIdentity.HasVersion);
        Assert.Equal(new NuGetVersion("2.1.0-rc1-build3168"), packageIdentity.Version);
    }

    [Theory]
    [InlineData(new[] { "abc", "-eq", "1.1.0" }, 0, new[] { "1.1.0" })]
    [InlineData(new[] { "abc", "-lt", "1.1.0" }, 0, new[] { "0.1.0-beta1", "0.1.0-beta2", "0.1.0", "1.0.0-beta", "1.0.0", "1.1.0-beta" })]
    [InlineData(new[] { "abc", "-lt", "1.1.0", "--same-major" }, 0, new[] { "1.0.0-beta", "1.0.0", "1.1.0-beta" })]
    [InlineData(new[] { "abc", "-lt", "1.1.0", "--same-minor" }, 0, new[] { "1.1.0-beta" })]
    [InlineData(new[] { "abc", "-lte", "1.1.0" }, 0, new[] { "0.1.0-beta1", "0.1.0-beta2", "0.1.0", "1.0.0-beta", "1.0.0", "1.1.0-beta", "1.1.0" })]
    [InlineData(new[] { "abc", "-lte", "1.1.0", "--same-major" }, 0, new[] { "1.0.0-beta", "1.0.0", "1.1.0-beta", "1.1.0" })]
    [InlineData(new[] { "abc", "-lte", "1.1.0", "--same-minor" }, 0, new[] { "1.1.0-beta", "1.1.0" })]
    [InlineData(new[] { "abc", "-gt", "2.0.0-beta" }, 0, new[] { "2.0.0", "2.1.0-beta", "2.1.0", "3.0.0", "3.1.0-beta", "3.1.0" })]
    [InlineData(new[] { "abc", "-gt", "2.0.0-beta", "--same-major" }, 0, new[] { "2.0.0", "2.1.0-beta", "2.1.0" })]
    [InlineData(new[] { "abc", "-gt", "2.0.0-beta", "--same-minor" }, 0, new[] { "2.0.0" })]
    [InlineData(new[] { "abc", "-gte", "2.0.0-beta" }, 0, new[] { "2.0.0-beta", "2.0.0", "2.1.0-beta", "2.1.0", "3.0.0", "3.1.0-beta", "3.1.0" })]
    [InlineData(new[] { "abc", "-gte", "2.0.0-beta", "--same-major" }, 0, new[] { "2.0.0-beta", "2.0.0", "2.1.0-beta", "2.1.0" })]
    [InlineData(new[] { "abc", "-gte", "2.0.0-beta", "--same-minor" }, 0, new[] { "2.0.0-beta", "2.0.0" })]
    public void Run_filters_properly(string[] args, int expectedExitCode, string[] expectedVersions)
    {
        // arrange
        var mockNugetVersionService = new Mock<INugetVersionSource>();
        mockNugetVersionService
            .Setup(x => x.GetPackageVersionsAsync("abc"))
            .ReturnsAsync(new NuGetVersion[]
            {
                new NuGetVersion("0.1.0-beta1"),
                new NuGetVersion("0.1.0-beta2"),
                new NuGetVersion("0.1.0"),
                new NuGetVersion("1.0.0-beta"),
                new NuGetVersion("1.0.0"),
                new NuGetVersion("1.1.0-beta"),
                new NuGetVersion("1.1.0"),
                new NuGetVersion("2.0.0-beta"),
                new NuGetVersion("2.0.0"),
                new NuGetVersion("2.1.0-beta"),
                new NuGetVersion("2.1.0"),
                new NuGetVersion("3.0.0"),
                new NuGetVersion("3.1.0-beta"),
                new NuGetVersion("3.1.0"),
            });

        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);

            // act
            var outputCode = program.Run(args);

            // assert
            Assert.Equal(expectedExitCode, outputCode);
            var outputLines = mockStandardOut.ToString().Split("\n").Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim()).ToArray();
            Assert.Equal(expectedVersions, outputLines);
        }
    }

    [Theory]
    [InlineData(new[] { "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "-eq", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "2.1.0-rc1-build3168" })]
    [InlineData(new[] { "xunit", "-eq", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "2.1.0-rc1-build3168" })]
    [InlineData(new[] { "xunit", "-lt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "0.1.0", "1.0.0", "2.0.0", "2.1.0-beta" })]
    [InlineData(new[] { "xunit", "-lt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-major" }, 0, new[] { "2.0.0", "2.1.0-beta" })]
    [InlineData(new[] { "xunit", "-lt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-minor" }, 0, new[] { "2.1.0-beta" })]
    [InlineData(new[] { "xunit", "-lte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "0.1.0", "1.0.0", "2.0.0", "2.1.0-beta", "2.1.0-rc1-build3168" })]
    [InlineData(new[] { "xunit", "-lte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-major" }, 0, new[] { "2.0.0", "2.1.0-beta", "2.1.0-rc1-build3168" })]
    [InlineData(new[] { "xunit", "-lte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-minor" }, 0, new[] { "2.1.0-beta", "2.1.0-rc1-build3168" })]
    [InlineData(new[] { "xunit", "-gt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "2.1.0", "2.2.0", "3.0.0", "3.1.0-beta", "3.1.0" })]
    [InlineData(new[] { "xunit", "-gt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-major" }, 0, new[] { "2.1.0", "2.2.0" })]
    [InlineData(new[] { "xunit", "-gt", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-minor" }, 0, new[] { "2.1.0" })]
    [InlineData(new[] { "xunit", "-gte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme" }, 0, new[] { "2.1.0-rc1-build3168", "2.1.0", "2.2.0", "3.0.0", "3.1.0-beta", "3.1.0" })]
    [InlineData(new[] { "xunit", "-gte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-major" }, 0, new[] { "2.1.0-rc1-build3168", "2.1.0", "2.2.0" })]
    [InlineData(new[] { "xunit", "-gte", "TestData/xunit.2.1.0-rc1-build3168.nupkg.keepme", "--same-minor" }, 0, new[] { "2.1.0-rc1-build3168", "2.1.0" })]
    public void Run_filters_properly_file(string[] args, int expectedExitCode, string[] expectedVersions)
    {
        // arrange
        var mockNugetVersionService = new Mock<INugetVersionSource>();
        mockNugetVersionService
            .Setup(x => x.GetPackageVersionsAsync("xunit"))
            .ReturnsAsync(new NuGetVersion[]
            {
                new NuGetVersion("0.1.0"),
                new NuGetVersion("1.0.0"),
                new NuGetVersion("2.0.0"),
                new NuGetVersion("2.1.0-beta"),
                new NuGetVersion("2.1.0-rc1-build3168"),
                new NuGetVersion("2.1.0"),
                new NuGetVersion("2.2.0"),
                new NuGetVersion("3.0.0"),
                new NuGetVersion("3.1.0-beta"),
                new NuGetVersion("3.1.0"),
            });

        using (var mockStandardOut = new StringWriter())
        using (var mockStandardError = new StringWriter())
        {
            var program = new Program(mockNugetVersionService.Object, mockStandardOut, mockStandardError);

            // act
            var outputCode = program.Run(args);

            // assert
            Assert.Equal(expectedExitCode, outputCode);
            var outputLines = mockStandardOut.ToString().Split("\n").Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim()).ToArray();
            Assert.Equal(expectedVersions, outputLines);
        }
    }
}
