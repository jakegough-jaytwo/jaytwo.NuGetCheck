namespace jaytwo.NuGetCheck.GlobalTool;

public class RunOptions
{
    public string? PackageId { get; set; }

    public string? EqualTo { get; set; }

    public string? GreaterThan { get; set; }

    public string? GreaterThanOrEqualTo { get; set; }

    public string? LessThan { get; set; }

    public string? LessThanOrEqualTo { get; set; }

    public bool? SameMajorVersion { get; set; }

    public bool? SameMinorVersion { get; set; }

    public bool FailOnMatchFound { get; set; }
}
