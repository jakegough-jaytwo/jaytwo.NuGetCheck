using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace jaytwo.NuGetCheck.GlobalTool;

public interface INugetVersionSource
{
    Task<IList<NuGetVersion>> GetPackageVersionsAsync(string packageId);
}
