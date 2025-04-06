# jaytwo.NuGetCheck

A [.NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) (and docker image) that can check to see if a NuGet package/version has already been published.

I use this in my CI pipelines to fail a build in which I forget to bump the version.


## Installation

```
dotnet tool install -g jaytwo.NuGetCheck
```

## Normal Usage

To view help:

```bash
nugetcheck --help
```

To see if the package `xunit` has any published versions greater than or equal to `2.0.0` and less than `3.0.0`, run the following command:

```bash
nugetcheck xunit -gte 2.0.0 --same-major
```

To see if the package `xunit` has any published versions greater than or equal to `2.0.0` and less than `2.1.0`, run the following command:

```bash
nugetcheck xunit -gte 2.0.0 --same-minor
```

> For any .NET Core Global Tools to work, your path needs to include `~/.dotnet/tools`

### Normal Exit Codes

If there are no results, the process exits with code `-1` (fail).

If there are results, the process exits with code `0` (success).

If there is an exception, the process exits with code `1` (fail).

### Opposite Day Exit Codes

Since the main use case for this tool is to fail CI if I forgot to bump the version, I want to fail when there _are_ versions found.  Just use the option `--fail-on-match`.

If there are no results, the process exits with code `0` (success).

If there are results, the process exits with code `-1` (fail).

If there is an exception, the process exits with code `1` (fail).

```bash
# This will fail if `xunit` has any published versions greater than or equal to `2.0.0` and less than `2.1.0`:
nugetcheck xunit -gte 2.0.0 --same-minor --fail-on-match
```

## Docker Usage

Sometimes the easiest way to use someting is in a docker image.  To see if the package has any published versions greater than or equal to `2.0.0` and less than `3.0.0`, run the following command:

```bash
docker run --rm jakegough/jaytwo.nugetcheck xunit -gte 2.0.0 --same-major
```

If you want to start the container with an interactive terminal, you'll need to override the entrypoint.  
For example, when I run this in Jenkins.  It's easier to use this with a `docker.image().inside()` 
and use the automagically mounted nupkg files.  The command inside the container is `nugetcheck`.

```bash
docker run -it --rm --entrypoint sh jakegough/jaytwo.nugetcheck

# then inside the container
nugetcheck xunit -gte 2.0.0 --same-major
```
