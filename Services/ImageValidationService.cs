using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabelAva.Models;

namespace LabelAva.Services;

public class ImageValidationService
{
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
}
