using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LabelAva.Models;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>
/// 行为轨迹快照：把「当前应用状态」规范化成一个可逐字节比对的 JSON。
///
/// 设计要点：
/// - 所有绝对路径都替换成 &lt;root&gt;，否则轨迹会随临时目录名变化。
/// - 所有 map 用 SortedDictionary 保证键序确定；列表保持语义顺序。
/// - 浮点统一四舍五入到 4 位，避免渲染/布局的微小抖动造成假差异。
/// - 只纳入「确定性状态」：不纳入耗时、时间戳、文件内容等易变项。
/// </summary>
public static class StateTrace
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Capture(MainWindowHarness harness, ProjectFixture project)
        => JsonSerializer.Serialize(Build(harness, project), Options);

    private static SortedDictionary<string, object?> Build(MainWindowHarness harness, ProjectFixture project)
    {
        var vm = harness.Vm;
        var window = harness.Window;
        var textBox = window.FindControl<TextBox>("TranslationTextBox");

        return new SortedDictionary<string, object?>
        {
            // 几何只记录**与环境无关的事实**：绝对像素（fitScale / 变换矩阵 / 缩放百分比）
            // 取决于窗口布局与 DPI，录进基线会逼着每个平台各录一份。这里改为记录它们之间
            // 应当成立的关系 —— 判据更强（它编码的是意图而不是某台机器的输出），且跨平台共用一份基线。
            ["canvas"] = new SortedDictionary<string, object?>
            {
                ["currentImagePath"] = Relative(window.CanvasControl.CurrentImagePath, project.Root),
                ["geometry"] = GeometryFacts(harness),
                ["hasImage"] = vm.CanvasWorkspace.HasImage,
                ["highlightedLabelIndex"] = vm.CanvasWorkspace.HighlightedLabelIndex,
                ["isFirstImageLoaded"] = window.CanvasControl.IsFirstImageLoaded,
                ["isPanning"] = vm.CanvasWorkspace.IsPanning,
                ["pendingNewLabelIndex"] = vm.CanvasWorkspace.PendingNewLabelIndex,
            },
            ["document"] = new SortedDictionary<string, object?>
            {
                ["filePath"] = Relative(vm.Document.FilePath, project.Root),
                ["hasDocument"] = vm.Document.HasDocument,
                ["imageFolderPath"] = Relative(vm.Document.ImageFolderPath, project.Root),
                ["imagePathMapping"] = SortedMap(
                    vm.Document.ImagePathMapping,
                    v => (object?)Relative(v, project.Root)),
                ["isDirty"] = vm.Document.IsDirty,
                ["translationData"] = TranslationDataOf(vm.Document.TranslationData),
            },
            ["edit"] = new SortedDictionary<string, object?>
            {
                ["activeDligFontFamily"] = vm.Edit.ActiveDligFontFamily?.Name,
                ["activeDligFontFeatures"] = vm.Edit.ActiveDligFontFeatures?
                    .Select(f => $"{f.Tag}={f.Value}").ToList(),
                ["canToggleEditMode"] = vm.Edit.CanToggleEditMode,
                ["currentGroupIndex"] = vm.Edit.CurrentGroupIndex,
                ["isEditMode"] = vm.Edit.IsEditMode,
                ["quickInputSlots"] = vm.Edit.QuickInputSlots.Select(s => (object?)
                    new SortedDictionary<string, object?>
                    {
                        ["character"] = s.Character,
                        ["fromDlig"] = s.IsFromDligConfig,
                        ["label"] = s.Label,
                    }).ToList(),
            },
            ["history"] = new SortedDictionary<string, object?>
            {
                ["canRedo"] = vm.History.CanRedo,
                ["canUndo"] = vm.History.CanUndo,
                ["redoHeader"] = vm.History.RedoHeader,
                ["undoHeader"] = vm.History.UndoHeader,
            },
            ["navigation"] = new SortedDictionary<string, object?>
            {
                ["currentImageIndex"] = vm.Navigation.CurrentImageIndex,
                ["currentTreeItem"] = vm.Navigation.CurrentTreeItem?.ImageName,
                ["hasDocument"] = vm.Navigation.HasDocument,
                ["imageFolderPath"] = Relative(vm.Navigation.ImageFolderPath, project.Root),
                ["imageNames"] = vm.Navigation.ImageNames.ToList(),
                ["lastFocusedRootItem"] = vm.Navigation.LastFocusedRootItem?.ImageName,
                ["selectedItemKind"] = vm.Navigation.SelectedItem?.GetType().Name,
                ["selectedTranslationIndex"] = vm.Navigation.SelectedTranslationItem?.Index,
                ["tree"] = vm.Navigation.TreeItems.Select(t => (object?)
                    new SortedDictionary<string, object?>
                    {
                        ["imageName"] = t.ImageName,
                        ["translationCount"] = t.Translations?.Count ?? 0,
                        ["translationIndexes"] = t.Translations?.Select(x => x.Index).ToList(),
                    }).ToList(),
            },
            ["resources"] = new SortedDictionary<string, object?>
            {
                ["dligFontFamily"] = (window.Resources["DligFontFamily"] as FontFamily)?.Name,
                ["hasDligFontFeatures"] = window.Resources["DligFontFeatures"] is not null,
            },
            ["statusBar"] = new SortedDictionary<string, object?>
            {
                // ZoomText 是绝对缩放百分比的字符串形式，随平台变化，故不入轨迹；
                // 它与 fitScale 的一致性由 GeometryFacts.zoomPercentMatchesFitScale 守着。
                ["statusText"] = vm.StatusBar.StatusText,
            },
            ["textBox"] = textBox is null ? null : new SortedDictionary<string, object?>
            {
                ["caretIndex"] = textBox.CaretIndex,
                ["isEnabled"] = textBox.IsEnabled,
                ["isFocused"] = textBox.IsFocused,
                ["selectionEnd"] = textBox.SelectionEnd,
                ["selectionStart"] = textBox.SelectionStart,
                ["text"] = NormalizeNewlines(textBox.Text),
            },
        };
    }

    private static object? TranslationDataOf(TranslationData? data)
    {
        if (data is null) return null;

        var imageLabels = new SortedDictionary<string, object?>();
        foreach (var entry in data.ImageLabels.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            imageLabels[entry.Key] = entry.Value.Select(l => (object?)
                new SortedDictionary<string, object?>
                {
                    ["groupIndex"] = l.GroupIndex,
                    ["text"] = NormalizeNewlines(l.Text),
                    ["textIndex"] = l.TextIndex,
                    ["x"] = Round(l.X),
                    ["y"] = Round(l.Y),
                }).ToList();
        }

        return new SortedDictionary<string, object?>
        {
            ["comments"] = data.Comments.ToList(),
            ["groups"] = data.Groups.Select(g => (object?)
                new SortedDictionary<string, object?>
                {
                    ["index"] = g.Index,
                    ["name"] = g.Name,
                }).ToList(),
            ["imageLabels"] = imageLabels,
            ["unknownParam"] = data.UnknownParam,
        };
    }

    /// <summary>
    /// 与环境无关的画布几何事实。图片未加载或容器未布局时返回 null。
    ///
    /// 刻意**不记录绝对数值**：fitScale、变换矩阵、缩放百分比都由窗口布局与 DPI 决定，
    /// 录成基线会让每个平台都需要自己的基线文件。这里记录的是它们之间应当成立的关系。
    /// </summary>
    private static object? GeometryFacts(MainWindowHarness harness)
    {
        var vm = harness.Vm;
        var workspace = vm.CanvasWorkspace;
        var decodedImage = harness.Window.CanvasControl.CurrentImage;

        // 用 fit 逻辑实际收到的尺寸（VM 里的那份），而不是快照时刻某个控件的尺寸
        var containerSize = workspace.ContainerSize;
        var imageSize = workspace.ImageSize;

        double containerWidth = containerSize.Width;
        double containerHeight = containerSize.Height;
        double imageWidth = imageSize.Width;
        double imageHeight = imageSize.Height;
        if (containerWidth <= 0 || containerHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
            return null;

        var matrix = workspace.TransformMatrix;
        double zoomPercent = workspace.ZoomPercent;
        double fitScale = workspace.CurrentFitScale;

        return new SortedDictionary<string, object?>
        {
            // fit 逻辑用的图片尺寸，必须与真实解码出来的位图尺寸一致
            // （桩渲染后端会把位图报成 1×1，这条就是用来抓这种事的）
            ["imageSizeMatchesDecodedBitmap"] = decodedImage is not null
                                               && imageWidth == decodedImage.Size.Width
                                               && imageHeight == decodedImage.Size.Height,
            // 变换必须是等比缩放 + 纯平移（应用只会 scale + translate）
            ["isUniformScale"] = Math.Abs(matrix.M11 - matrix.M22) < 1e-6
                                 && Math.Abs(matrix.M12) < 1e-6
                                 && Math.Abs(matrix.M21) < 1e-6,
            // 状态栏的缩放百分比必须是 fitScale 的百分比
            ["zoomPercentMatchesFitScale"] = Math.Abs(zoomPercent - fitScale * 100) < 1e-6,
            //
            // 刻意不在这里断言「缩放等于适应容器比例」「平移正好居中」：
            // 容器尺寸变化时应用只重新居中/钳制，并不重算 fit 比例（OnContainerSizeChanged），
            // 所以快照时刻这两条是否成立取决于布局时序 —— 本机恰好成立、CI 上不成立。
            // 这两条属于 CalculateFitTransform 的契约，改由 CanvasFitTests 显式触发后断言。
        };
    }

    private static SortedDictionary<string, object?> SortedMap<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> source,
        Func<TValue, object?> project)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in source)
            result[entry.Key] = project(entry.Value);
        return result;
    }

    private static double Round(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 把 CRLF 归一成 LF。
    ///
    /// TranslationParser 用 <c>StringBuilder.AppendLine</c> 拼多行文本，换行符来自
    /// <c>Environment.NewLine</c>，所以同一个翻译文件在 Windows 上解析出 "\r\n"、在 Linux/macOS 上
    /// 解析出 "\n"。这是应用层面的跨平台不一致（保存时同理），本测试网不去改应用行为，
    /// 但轨迹必须在各平台可比，所以这里归一化。
    /// </summary>
    private static string? NormalizeNewlines(string? value)
        => value?.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string? Relative(string? path, string root)
        => string.IsNullOrEmpty(path)
            ? path
            : path.Replace(root, "<root>", StringComparison.OrdinalIgnoreCase)
                  .Replace('\\', '/');
}
