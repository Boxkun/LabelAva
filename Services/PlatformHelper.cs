namespace LabelAva.Services;

public static class PlatformHelper
{
    public static bool IsMacOS => OperatingSystem.IsMacOS();
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsLinux => OperatingSystem.IsLinux();
}
