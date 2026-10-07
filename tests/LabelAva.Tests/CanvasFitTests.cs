using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace LabelAva.Tests;

/// <summary>
/// 「适应容器」变换的契约。
///
/// 这条契约原本散在轨迹的一个布尔字段里，但那个字段依赖布局时序：容器尺寸变化时
/// 应用只重新居中/钳制、并不重算 fit 比例（<c>OnContainerSizeChanged</c>），
/// 所以快照时刻「缩放是否仍等于当前容器算出的比例」取决于此前是否发生过尺寸变化 ——
/// 本机恰好成立，CI 上不成立。
///
/// 正确做法是**显式触发一次 CalculateFitTransform** 再核对它承诺的公式：
/// 这样断言的是契约本身，不依赖布局时序，也不依赖平台。
/// </summary>
public class CanvasFitTests
{
    [AvaloniaFact]
    public void 适应容器变换_缩放取min比例且图片居中()
    {
        using var session = new EditModeSession();
        var workspace = session.Harness.Vm.CanvasWorkspace;

        HeadlessPump.Until(
            () => workspace.ContainerSize.Width > 0 && workspace.ContainerSize.Height > 0,
            "画布容器完成布局");
        HeadlessPump.Until(
            () => workspace.ImageSize.Width > 0 && workspace.ImageSize.Height > 0,
            "图片尺寸已就绪");

        double containerWidth = workspace.ContainerSize.Width;
        double containerHeight = workspace.ContainerSize.Height;
        double imageWidth = workspace.ImageSize.Width;
        double imageHeight = workspace.ImageSize.Height;

        workspace.CalculateFitTransform();

        double expectedScale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);
        var matrix = workspace.TransformMatrix;

        // 等比缩放
        Assert.Equal(expectedScale, matrix.M11, 6);
        Assert.Equal(expectedScale, matrix.M22, 6);
        Assert.Equal(0d, matrix.M12, 6);
        Assert.Equal(0d, matrix.M21, 6);

        // 居中
        Assert.Equal((containerWidth - imageWidth * expectedScale) / 2, matrix.M31, 6);
        Assert.Equal((containerHeight - imageHeight * expectedScale) / 2, matrix.M32, 6);

        // 同时写回 fitScale 与缩放百分比
        Assert.Equal(expectedScale, workspace.CurrentFitScale, 6);
        Assert.Equal(expectedScale * 100, workspace.ZoomPercent, 6);
    }

    [AvaloniaFact]
    public void 适应容器变换_容器为零尺寸时不做任何改动()
    {
        using var session = new EditModeSession();
        var workspace = session.Harness.Vm.CanvasWorkspace;

        // 显式把容器尺寸置零：CalculateFitTransform 应当直接返回，不产生 NaN 变换
        workspace.UpdateContainerSize(new Avalonia.Size(0, 0));
        var before = workspace.TransformMatrix;

        workspace.CalculateFitTransform();

        Assert.Equal(before, workspace.TransformMatrix);
    }
}
