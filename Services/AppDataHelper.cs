using System;
using System.IO;

namespace LabelAva.Services;

public static class AppDataHelper
{
    private static readonly string AppName = "LabelAva";

    /// <summary>
    /// 测试注入点：非 null 时把漫游 AppData 根目录重定向到该路径。
    /// 生产代码保持 null，此时行为与引入该接缝之前完全一致。
    /// </summary>
    public static string? AppDataRootOverride { get; set; }

    /// <summary>
    /// 测试注入点：非 null 时把本地 AppData 根目录重定向到该路径。
    /// 生产代码保持 null。
    /// </summary>
    public static string? LocalDataRootOverride { get; set; }

    public static string AppDataFolder
    {
        get
        {
            var folder = AppDataRootOverride is { Length: > 0 } root
                ? root
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppName);
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string SettingsFilePath =>
        Path.Combine(AppDataFolder, "config.json");

    public static string LocalDataFolder
    {
        get
        {
            if (LocalDataRootOverride is { Length: > 0 } localRoot)
            {
                Directory.CreateDirectory(localRoot);
                return localRoot;
            }

            string basePath;
            if (PlatformHelper.IsMacOS)
            {
                // .NET 在 macOS 上错误映射 LocalApplicationData → ~/.local/share
                basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support");
            }
            else
            {
                basePath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
            }
            var folder = Path.Combine(basePath, AppName);
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string RecoveryFolder
    {
        get
        {
            var folder = Path.Combine(LocalDataFolder, "recovery");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string DligConfigFolder
    {
        get
        {
            var folder = Path.Combine(AppDataFolder, "dlig_conf");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
