using System;
using System.IO;
using LabelAva.Services;

namespace LabelAva.Tests;

/// <summary>
/// 把 <see cref="AppDataHelper"/> 的两个根目录重定向到本次测试专用的临时目录。
/// 作用有二：一是让测试输出与开发机上的真实配置无关（否则轨迹基线会随环境漂移），
/// 二是保证测试永远不会读写用户真实的 %APPDATA%\LabelAva。
/// </summary>
public sealed class TempAppData : IDisposable
{
    private readonly string _root;
    private readonly string? _previousAppDataRoot;
    private readonly string? _previousLocalDataRoot;

    public TempAppData()
    {
        _root = Path.Combine(Path.GetTempPath(), "LabelAva.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _previousAppDataRoot = AppDataHelper.AppDataRootOverride;
        _previousLocalDataRoot = AppDataHelper.LocalDataRootOverride;

        AppDataHelper.AppDataRootOverride = Path.Combine(_root, "Roaming");
        AppDataHelper.LocalDataRootOverride = Path.Combine(_root, "Local");
    }

    /// <summary>本次测试独占的临时根目录。</summary>
    public string Root => _root;

    public void Dispose()
    {
        AppDataHelper.AppDataRootOverride = _previousAppDataRoot;
        AppDataHelper.LocalDataRootOverride = _previousLocalDataRoot;

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不能影响测试结论
        }
    }
}
