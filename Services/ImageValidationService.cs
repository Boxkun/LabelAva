using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LabelAva.Models;

namespace LabelAva.Services;

public class ImageValidationService
{
    private static readonly string[] SupportedExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

    public List<ImageAssociationItem> Validate(string imageFolderPath, IEnumerable<string> imageNames)
    {
        return imageNames.Select(name =>
        {
            var fullPath = Path.Combine(imageFolderPath, name);
            var exists = File.Exists(fullPath);
            return new ImageAssociationItem
            {
                ImageName = name,
                Status = exists ? ImageValidationStatus.OK : ImageValidationStatus.Missing,
                StatusText = exists ? "\u2713 \u6b63\u5e38" : "\u2717 \u7f3a\u5931",
                NewPath = null
            };
        }).ToList();
    }

    public ImageValidationStatus ValidateSingle(string imageFolderPath, string imageName)
    {
        var fullPath = Path.Combine(imageFolderPath, imageName);
        return File.Exists(fullPath) ? ImageValidationStatus.OK : ImageValidationStatus.Missing;
    }

    public (ImageValidationStatus status, string statusText) ValidateSingleWithText(string imageFolderPath, string imageName)
    {
        var status = ValidateSingle(imageFolderPath, imageName);
        return (status, status == ImageValidationStatus.OK ? "\u2713 \u6b63\u5e38" : "\u2717 \u7f3a\u5931");
    }

    public static (ImageValidationStatus status, string statusText) ValidateFullPath(string? fullPath)
    {
        var exists = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
        return (exists ? ImageValidationStatus.OK : ImageValidationStatus.Missing,
                exists ? "\u2713 \u6b63\u5e38" : "\u2717 \u7f3a\u5931");
    }

    /// <summary>
    /// 对缺失的图片名称，尝试在文件夹中查找相同基础名、但扩展名不同的文件。
    /// 跳过扩展名完全相同（仅大小写不同，Linux 场景）的条目。
    /// 跳过存在歧义（同一基础名匹配到多个文件）的条目。
    /// </summary>
    /// <param name="imageFolderPath">图片文件夹路径</param>
    /// <param name="missingImageNames">缺失的图片文件名列表</param>
    /// <returns>Key=原始 ImageName, Value=匹配到的完整文件路径</returns>
    public Dictionary<string, string> FindAlternateExtensionMatches(
        string imageFolderPath,
        IEnumerable<string> missingImageNames)
    {
        var result = new Dictionary<string, string>();

        if (!Directory.Exists(imageFolderPath))
            return result;

        var fileLookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(imageFolderPath))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
                continue;

            var baseName = Path.GetFileNameWithoutExtension(file);
            if (!fileLookup.TryGetValue(baseName, out var list))
            {
                list = new List<string>();
                fileLookup[baseName] = list;
            }
            list.Add(file);
        }

        foreach (var imageName in missingImageNames)
        {
            var originalBaseName = Path.GetFileNameWithoutExtension(imageName);
            var originalExt = Path.GetExtension(imageName).ToLowerInvariant();

            if (!fileLookup.TryGetValue(originalBaseName, out var matches) || matches.Count == 0)
                continue;

            var alternateMatches = matches
                .Where(m => Path.GetExtension(m).ToLowerInvariant() != originalExt)
                .ToList();

            if (alternateMatches.Count == 1)
            {
                result[imageName] = alternateMatches[0];
            }
        }

        return result;
    }

    public static (bool isConsistent, string? actualExtension) CheckFormatConsistency(string filePath)
    {
        var actualExt = DetectFormatFromHeader(filePath);
        var fileExt = Path.GetExtension(filePath).ToLowerInvariant();
        Debug.WriteLine($"[CheckFormatConsistency] file={Path.GetFileName(filePath)} fileExt={fileExt} magicResult={actualExt ?? "(null)"}");

        if (actualExt == null)
            return (true, null);

        if (IsJpegExt(fileExt) && IsJpegExt(actualExt))
            return (true, actualExt);

        var isConsistent = fileExt == actualExt;
        Debug.WriteLine($"[CheckFormatConsistency] => isConsistent={isConsistent} fileExt={fileExt} actualExt={actualExt}");
        return (isConsistent, actualExt);
    }

    private static string? DetectFormatFromHeader(string filePath)
    {
        try
        {
            var buffer = new byte[12];
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytesRead = fs.Read(buffer, 0, buffer.Length);

            Debug.WriteLine($"[DetectFormat] file={Path.GetFileName(filePath)} bytesRead={bytesRead} hex[0..{Math.Min(bytesRead,12)}]={BitConverter.ToString(buffer, 0, Math.Min(bytesRead, 12))}");

            if (bytesRead < 3)
            {
                Debug.WriteLine("[DetectFormat] => bytesRead<3, return null");
                return null;
            }

            if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                return ".jpg";

            if (bytesRead >= 4 && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                return ".png";

            if (bytesRead >= 4 && buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
                return ".gif";

            if (buffer[0] == 0x42 && buffer[1] == 0x4D)
                return ".bmp";

            if (bytesRead >= 12 && buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46
                && buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
                return ".webp";

            if (bytesRead >= 4 && buffer[0] == 0x49 && buffer[1] == 0x49 && buffer[2] == 0x2A && buffer[3] == 0x00)
                return ".tiff";

            if (bytesRead >= 4 && buffer[0] == 0x4D && buffer[1] == 0x4D && buffer[2] == 0x00 && buffer[3] == 0x2A)
                return ".tiff";

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsJpegExt(string ext)
    {
        return ext == ".jpg" || ext == ".jpeg";
    }

    public static bool HasAnyFormatIssue(string imageFolderPath, IEnumerable<ImageAssociationItem> items)
    {
        foreach (var item in items)
        {
            if (item.Status != ImageValidationStatus.OK || !string.IsNullOrEmpty(item.NewPath))
                continue;

            var filePath = Path.Combine(imageFolderPath, item.ImageName);
            if (!File.Exists(filePath))
                continue;

            var (isConsistent, _) = CheckFormatConsistency(filePath);
            if (!isConsistent)
                return true;
        }

        return false;
    }
}
