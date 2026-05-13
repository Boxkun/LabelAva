using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabelAva.Models;

namespace LabelAva.Services;

[JsonSerializable(typeof(DligFontConfig))]
[JsonSerializable(typeof(QuickInputSlot))]
[JsonSerializable(typeof(List<QuickInputSlot>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class DligConfigContext : JsonSerializerContext { }

public static class DligConfigService
{
    private static readonly string ConfigDir = AppDataHelper.DligConfigFolder;

    public static void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDir);
    }

    public static string GetConfigDir() => ConfigDir;

    public static List<string> ListConfigNames()
    {
        if (!Directory.Exists(ConfigDir))
            return new List<string>();

        return Directory.GetFiles(ConfigDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public static DligFontConfig? LoadConfig(string configName)
    {
        if (string.IsNullOrWhiteSpace(configName))
            return null;

        var path = Path.Combine(ConfigDir, $"{configName}.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize(json, DligConfigContext.Default.DligFontConfig);
            return config;
        }
        catch
        {
            return null;
        }
    }

    public static bool SaveConfig(string configName, DligFontConfig config)
    {
        if (string.IsNullOrWhiteSpace(configName))
            return false;

        EnsureDirectory();
        var path = Path.Combine(ConfigDir, $"{configName}.json");

        try
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            JsonSerializer.Serialize(writer, config, DligConfigContext.Default.DligFontConfig);
            writer.Flush();
            var json = Encoding.UTF8.GetString(stream.ToArray());
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool DeleteConfig(string configName)
    {
        if (string.IsNullOrWhiteSpace(configName))
            return false;

        var path = Path.Combine(ConfigDir, $"{configName}.json");
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
