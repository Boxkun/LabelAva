using System.Collections.Generic;
using System.Linq;
using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 删除标注命令（处理波纹删除）
/// </summary>
public class DeleteLabelCommand : IUndoableCommand
{
    private readonly List<LabelItem> _labelList;
    private readonly LabelItem _deletedItem;
    private readonly int _originalTextIndex;
    private readonly int _originalPosition;
    
    public string Description => "删除标注";
    
    public DeleteLabelCommand(List<LabelItem> labelList, LabelItem deletedItem)
    {
        _labelList = labelList;
        _deletedItem = deletedItem;
        _originalTextIndex = deletedItem.TextIndex;
        _originalPosition = labelList.IndexOf(deletedItem);
    }
    
    public void Execute()
    {
        // 找到要删除的项的位置
        int index = _labelList.IndexOf(_deletedItem);
        if (index < 0) return;
        
        // 记录删除前的 TextIndex（用于 Undo）
        // 注意：_deletedItem 的 TextIndex 已经在构造函数中保存了
        
        // 移除该项
        _labelList.RemoveAt(index);
        
        // 将后面所有项的 TextIndex 减 1
        foreach (var item in _labelList)
        {
            if (item.TextIndex > _originalTextIndex)
            {
                item.TextIndex--;
            }
        }
    }
    
    public void Undo()
    {
        // 先将后面所有项的 TextIndex 加 1（恢复到删除前的状态）
        foreach (var item in _labelList)
        {
            if (item.TextIndex >= _originalTextIndex)
            {
                item.TextIndex++;
            }
        }
        
        // 按原 TextIndex 重新插入该 item
        // 找到合适的插入位置（按 TextIndex 排序）
        int insertIndex = 0;
        for (int i = 0; i < _labelList.Count; i++)
        {
            if (_labelList[i].TextIndex > _originalTextIndex)
            {
                insertIndex = i;
                break;
            }
            insertIndex = i + 1;
        }
        
        _labelList.Insert(insertIndex, _deletedItem);
    }
}
