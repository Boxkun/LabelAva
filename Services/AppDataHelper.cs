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
