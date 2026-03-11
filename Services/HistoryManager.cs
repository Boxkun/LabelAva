using System;
using System.Collections.Generic;
using System.Linq;
using LabelAva.Commands;

namespace LabelAva.Services;

/// <summary>
/// 历史记录管理器，用于撤销/重做功能
/// 使用命令模式 (Command Pattern) 存储可撤销操作
/// </summary>
public class HistoryManager
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly int _maxStackSize;
    
    /// <summary>
    /// 历史记录变化事件
    /// </summary>
    public event EventHandler? HistoryChanged;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="maxStackSize">最大历史记录数量，默认50</param>
    public HistoryManager(int maxStackSize = 50)
    {
        _maxStackSize = maxStackSize;
    }
    
    /// <summary>
    /// 是否可以撤销
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;
    
    /// <summary>
    /// 是否可以重做
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;
    
    /// <summary>
    /// 获取最近的操作描述列表（最多返回指定数量）
    /// </summary>
    public List<string> GetRecentUndoDescriptions(int count = 2)
    {
        return _undoStack.Take(count).Select(e => e.Description).ToList();
    }
    
    /// <summary>
    /// 获取最近的重做操作描述列表（最多返回指定数量）
    /// </summary>
    public List<string> GetRecentRedoDescriptions(int count = 2)
    {
        return _redoStack.Take(count).Select(e => e.Description).ToList();
    }
    
    /// <summary>
    /// 执行命令并记录到历史
    /// </summary>
    /// <param name="command">要执行的命令</param>
    public void ExecuteCommand(IUndoableCommand command)
    {
        // 执行命令
        command.Execute();
        
        // 压入撤销栈
        _undoStack.Push(command);
        
        // 限制撤销栈大小，防止内存溢出
        if (_undoStack.Count > _maxStackSize)
        {
            // 取出最新的 N 个，反转为[老->新]顺序，再重新入栈恢复正常栈顺序
            var items = _undoStack.Take(_maxStackSize).Reverse().ToList();
            _undoStack.Clear();
            foreach (var item in items)
            {
                _undoStack.Push(item);
            }
        }
        
        // 每次执行新命令时，清空重做栈
        _redoStack.Clear();
        
        // 触发历史变化事件
        OnHistoryChanged();
    }
    
    /// <summary>
    /// 撤销操作
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            return;
        
        // 从撤销栈弹出命令
        var command = _undoStack.Pop();
        
        // 执行撤销
        command.Undo();
        
        // 压入重做栈
        _redoStack.Push(command);
        
        // 触发历史变化事件
        OnHistoryChanged();
    }
    
    /// <summary>
    /// 重做操作
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
            return;
        
        // 从重做栈弹出命令
        var command = _redoStack.Pop();
        
        // 重新执行命令
        command.Execute();
        
        // 压入撤销栈
        _undoStack.Push(command);
        
        // 触发历史变化事件
        OnHistoryChanged();
    }
    
    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnHistoryChanged();
    }
    
    /// <summary>
    /// 触发历史变化事件
    /// </summary>
    private void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
