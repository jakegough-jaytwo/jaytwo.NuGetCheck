using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace jaytwo.NuGetCheck.GlobalTool;

public class Program
{
    private readonly INugetVersionSource _nugetVersionSource;
    private readonly TextWriter _standardOut;
    private readonly TextWriter _standardError;

    public Program()
        : this(null, null, null)
    {
    }

    internal Program(INugetVersionSource? nugetVersionSource, TextWriter? standardOut, TextWriter? standardError)
    {
        _nugetVersionSource = nugetVersionSource ?? new NugetVersionSource();
        _standardOut = standardOut ?? Console.Out;
        _standardError = standardError ?? Console.Error;
    }

    public static int Main(string[] args) => new Program().Run(args);

    public static bool IsMajorVersionEqualTo(NuGetVersion a, NuGetVersion b)
    {
        return a.Major == b.Major;
    }

    public static bool IsMajorMinorVersionEqualTo(NuGetVersion a, NuGetVersion b)
    {
        return a.Major == b.Major && a.Minor == b.Minor;
    }

    public int Run(string[] args)
    {
        var app = new CommandLineApplication();
        app.Name = "nugetcheck";
        app.HelpOption("--help");
        app.VersionOption("--version", GetType().Assembly.GetName().Version!.ToString());
        app.Out = _standardOut;
        app.Error = _standardError;

        var packageIdArgument = app.Argument("[package-id-or-file]", "NuGet package id or file to query against");

        var equalToOption = app.Option("-eq <version-or-file>", "Show only versions equal to this value", CommandOptionType.SingleValue);
        var lessThanOption = app.Option("-lt <version-or-file>", "Show only versions less than this value", CommandOptionType.SingleValue);
        var lessThanOrEqualToOption = app.Option("-lte <version-or-file>", "Show only versions less or equal to than this value", CommandOptionType.SingleValue);
        var greaterThanOption = app.Option("-gt <version-or-file>", "Show only versions greater than this value", CommandOptionType.SingleValue);
        var greaterThanOrEqualToOption = app.Option("-gte <version-or-file>", "Show only versions greater or equal to than this value", CommandOptionType.SingleValue);

        var sameMajorVersionOption = app.Option("--same-major", "Show only versions with the same major version as specified in options", CommandOptionType.NoValue);
        var sameMinorVersionOption = app.Option("--same-minor", "Show only versions with the same major and minor versions as specified in options", CommandOptionType.NoValue);

        var failOnMatchOption = app.Option("--fail-on-match", "Return a non-zero exit code if any versions are returned", CommandOptionType.NoValue);

        app.OnExecute(() =>
        {
            return RunWithOptionsAsync(new RunOptions()
            {
                PackageId = packageIdArgument.Value,
                EqualTo = equalToOption.HasValue() ? equalToOption.Value() : null,
                GreaterThan = greaterThanOption.HasValue() ? greaterThanOption.Value() : null,
                GreaterThanOrEqualTo = greaterThanOrEqualToOption.HasValue() ? greaterThanOrEqualToOption.Value() : null,
                LessThan = lessThanOption.HasValue() ? lessThanOption.Value() : null,
                LessThanOrEqualTo = lessThanOrEqualToOption.HasValue() ? lessThanOrEqualToOption.Value() : null,
                SameMajorVersion = sameMajorVersionOption.HasValue(),
                SameMinorVersion = sameMinorVersionOption.HasValue(),
                FailOnMatchFound = failOnMatchOption.HasValue(),
            });
        });

        try
        {
            return app.Execute(args);
        }
        catch (Exception ex)
        {
            _standardError.WriteLine(ex.Message);
            return 1;
        }
    }

    internal static IEnumerable<NuGetVersion> FilterByPredicate(IEnumerable<NuGetVersion> versions, string versionOrFile, Func<NuGetVersion, NuGetVersion, bool> predicate)
    {
        NuGetVersion? version;
        var result = versions;

        if (!string.IsNullOrWhiteSpace(versionOrFile))
        {
            if (NuGetVersion.TryParse(versionOrFile, out version))
            {
                result = result.Where(x => predicate(x, version!));
            }
            else if (TryLoadNugetVersion(versionOrFile, out version))
            {
                result = result.Where(x => predicate(x, version!));
            }
        }

        return result;
    }

    internal static PackageIdentity? GetPackageIdentityFromFile(string path)
    {
        if (TryLoadPackageIdentityFromFile(path, out var result))
        {
            return result!;
        }
        else
        {
            return null;
        }
    }

    internal static bool TryLoadPackageIdentityFromFile(string path, out PackageIdentity? packageIdentity)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var file = new FileInfo(path);
                if (file.Exists)
                {
                    using (var archive = new PackageArchiveReader(file.FullName))
                    {
                        packageIdentity = archive.GetIdentity();
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        packageIdentity = null;
        return false;
    }

    internal static bool TryLoadNugetVersion(string path, out NuGetVersion? version)
    {
        if (TryLoadPackageIdentityFromFile(path, out PackageIdentity? packageIdentity))
        {
            version = packageIdentity!.Version;
            return true;
        }
        else
        {
            version = null;
            return false;
        }
    }

    internal static IEnumerable<NuGetVersion> ApplyFiltersFromOptions(IEnumerable<NuGetVersion> versions, RunOptions options)
    {
        var result = versions;

        result = FilterByPredicate(result, options.EqualTo!, (a, b) => a == b);

        result = FilterByPredicate(result, options.GreaterThan!, (a, b) => a > b);
        result = FilterByPredicate(result, options.GreaterThanOrEqualTo!, (a, b) => a >= b);
        result = FilterByPredicate(result, options.LessThan!, (a, b) => a < b);
        result = FilterByPredicate(result, options.LessThanOrEqualTo!, (a, b) => a <= b);

        if (options.SameMajorVersion ?? false)
        {
            result = FilterByPredicate(result, options.GreaterThan!, IsMajorVersionEqualTo);
            result = FilterByPredicate(result, options.GreaterThanOrEqualTo!, IsMajorVersionEqualTo);
            result = FilterByPredicate(result, options.LessThan!, IsMajorVersionEqualTo);
            result = FilterByPredicate(result, options.LessThanOrEqualTo!, IsMajorVersionEqualTo);
        }

        if (options.SameMinorVersion ?? false)
        {
            result = FilterByPredicate(result, options.GreaterThan!, IsMajorMinorVersionEqualTo);
            result = FilterByPredicate(result, options.GreaterThanOrEqualTo!, IsMajorMinorVersionEqualTo);
            result = FilterByPredicate(result, options.LessThan!, IsMajorMinorVersionEqualTo);
            result = FilterByPredicate(result, options.LessThanOrEqualTo!, IsMajorMinorVersionEqualTo);
        }

        return result;
    }

    private async Task<int> RunWithOptionsAsync(RunOptions options)
    {
        var packageId = GetPackageIdentityFromFile(options.PackageId!)?.Id ?? options.PackageId!;
        var allVersions = await _nugetVersionSource.GetPackageVersionsAsync(packageId);

        var filteredVersions = ApplyFiltersFromOptions(allVersions, options).ToList();

        var versionsExistExitCode = options.FailOnMatchFound ? -1 : 0;
        var noVersionsExitCode = options.FailOnMatchFound ? 0 : -1;

        if (filteredVersions.Any())
        {
            foreach (var version in filteredVersions)
            {
                _standardOut.WriteLine(version.ToString());
            }

            return versionsExistExitCode;
        }
        else
        {
            _standardOut.WriteLine("No results.");
            return noVersionsExitCode;
        }
    }
}
