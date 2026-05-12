using System.Collections.Generic;
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
                StatusText = exists ? "✓ 正常" : "✗ 缺失",
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
}
