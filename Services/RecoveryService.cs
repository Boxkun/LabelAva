using LabelAva.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LabelAva.Services;

/// <summary>
/// 崩溃恢复服务——即时持久化 TranslationData 到本地恢复文件，
/// 正常退出时自动清理，崩溃后重开时检测并提示恢复。
/// </summary>
public static class RecoveryService
{
    // ========================
    // 公开 API
    // ========================

    /// <summary>将 TranslationData 写入恢复文件（先写临时文件再原子替换）</summary>
    public static void Write(string filePath, TranslationData data, TranslationParser parser)
    {
        var recoveryPath = GetRecoveryFilePath(filePath);
        var tempPath = recoveryPath + ".tmp";
        try
        {
            parser.Save(tempPath, data);
            File.Move(tempPath, recoveryPath, overwrite: true);
        }
        catch
        {
            // 清理残留临时文件（忽略清理错误）
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// <summary>检查指定项目是否有恢复文件</summary>
    public static bool Exists(string filePath)
    {
        try
        {
            return File.Exists(GetRecoveryFilePath(filePath));
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    /// <summary>从恢复文件加载 TranslationData</summary>
    public static TranslationData Load(string filePath, TranslationParser parser)
    {
        return parser.Parse(GetRecoveryFilePath(filePath));
    }

    /// <summary>删除恢复文件（主文件已保存到最新时调用）</summary>
    public static void Delete(string filePath)
    {
        try
        {
            var recoveryPath = GetRecoveryFilePath(filePath);
            if (File.Exists(recoveryPath))
                File.Delete(recoveryPath);
        }
        catch (DirectoryNotFoundException) { }
        catch (FileNotFoundException) { }
    }

    /// <summary>将恢复文件移入 rejected/ 子目录（用户选择"不恢复"时调用）</summary>
    public static void Reject(string filePath)
    {
        try
        {
            var recoveryPath = GetRecoveryFilePath(filePath);
            if (!File.Exists(recoveryPath))
                return;

            var rejectedPath = GetRejectedFilePath(filePath);
            var rejectedDir = Path.GetDirectoryName(rejectedPath)!;
            Directory.CreateDirectory(rejectedDir);
            File.Move(recoveryPath, rejectedPath);
        }
        catch (DirectoryNotFoundException) { }
        catch (FileNotFoundException) { }
    }

    /// <summary>清理该项目的活跃恢复文件 + rejected/ 中该项目的所有归档</summary>
    public static void Cleanup(string filePath)
    {
        try
        {
            // 删除活跃恢复文件
            Delete(filePath);

            // 删除 rejected/ 中匹配该项目的所有文件
            var fileName = GetRecoveryFileName(filePath);
            // 文件名格式: {name}_{hash}.txt，rejected 中为 {name}_{hash}_{timestamp}.txt
            // 通过 hash 部分（末尾 8 位十六进制）匹配
            var hashSuffix = fileName[^10..]; // 取 "_a3f2b1c0.txt" 部分
            var rejectedDir = Path.Combine(AppDataHelper.RecoveryFolder, "rejected");
            if (!Directory.Exists(rejectedDir))
                return;

            foreach (var file in Directory.GetFiles(rejectedDir, "*.txt"))
            {
                var rejectedFileName = Path.GetFileName(file);
                if (rejectedFileName.Contains(hashSuffix))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch (DirectoryNotFoundException) { }
    }

    /// <summary>获取恢复文件完整路径</summary>
    public static string GetRecoveryFilePath(string filePath)
    {
        return Path.Combine(AppDataHelper.RecoveryFolder, GetRecoveryFileName(filePath));
    }

    // ========================
    // 内部方法
    // ========================

    /// <summary>生成恢复文件名: {目录名}_{路径哈希前8位}.txt</summary>
    private static string GetRecoveryFileName(string filePath)
    {
        var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
        var hash = ComputePathHash(filePath);
        return $"{dirName}_{hash}.txt";
    }

    /// <summary>生成 rejected/ 中的目标路径（含时间戳防冲突）</summary>
    private static string GetRejectedFilePath(string filePath)
    {
        var baseName = GetRecoveryFileName(filePath);
        // 去掉 .txt，插入时间戳
        var nameWithoutExt = baseName[..^4];
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return Path.Combine(AppDataHelper.RecoveryFolder, "rejected",
            $"{nameWithoutExt}_{timestamp}.txt");
    }

    /// <summary>计算完整路径的 SHA256 前 8 字节十六进制</summary>
    private static string ComputePathHash(string filePath)
    {
        var bytes = Encoding.UTF8.GetBytes(filePath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 8);
    }
}
