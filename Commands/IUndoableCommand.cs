namespace LabelAva.Commands;

/// <summary>
/// 可撤销命令接口
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// 执行命令
    /// </summary>
    void Execute();
    
    /// <summary>
    /// 撤销命令
    /// </summary>
    void Undo();
    
    /// <summary>
    /// 操作描述
    /// </summary>
    string Description { get; }
}
