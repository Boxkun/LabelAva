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
            ["canvas"] = new SortedDictionary<string, object?>
            {
                ["currentFitScale"] = Round(vm.CanvasWorkspace.CurrentFitScale),
                ["currentImagePath"] = Relative(window.CanvasControl.CurrentImagePath, project.Root),
                ["hasImage"] = vm.CanvasWorkspace.HasImage,
                ["highlightedLabelIndex"] = vm.CanvasWorkspace.HighlightedLabelIndex,
                ["isFirstImageLoaded"] = window.CanvasControl.IsFirstImageLoaded,
                ["isPanning"] = vm.CanvasWorkspace.IsPanning,
                ["pendingNewLabelIndex"] = vm.CanvasWorkspace.PendingNewLabelIndex,
                ["transform"] = MatrixOf(vm.CanvasWorkspace.TransformMatrix),
                ["zoomPercent"] = Round(vm.CanvasWorkspace.ZoomPercent),
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
                ["statusText"] = vm.StatusBar.StatusText,
                ["zoomText"] = vm.StatusBar.ZoomText,
            },
            ["textBox"] = textBox is null ? null : new SortedDictionary<string, object?>
            {
                ["caretIndex"] = textBox.CaretIndex,
                ["isEnabled"] = textBox.IsEnabled,
                ["isFocused"] = textBox.IsFocused,
                ["selectionEnd"] = textBox.SelectionEnd,
                ["selectionStart"] = textBox.SelectionStart,
                ["text"] = textBox.Text,
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
                    ["text"] = l.Text,
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

    private static SortedDictionary<string, object?> MatrixOf(Matrix m)
        => new()
        {
            ["m11"] = Round(m.M11),
            ["m12"] = Round(m.M12),
            ["m21"] = Round(m.M21),
            ["m22"] = Round(m.M22),
            ["m31"] = Round(m.M31),
            ["m32"] = Round(m.M32),
        };

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

    private static string? Relative(string? path, string root)
        => string.IsNullOrEmpty(path)
            ? path
            : path.Replace(root, "<root>", StringComparison.OrdinalIgnoreCase)
                  .Replace('\\', '/');
}
