using System;
using System.IO;

namespace LabelAva.Services;

public static class AppDataHelper
{
    private static readonly string AppName = "LabelAva";

    public static string AppDataFolder
    {
        get
        {
            var folder = Path.Combine(
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
