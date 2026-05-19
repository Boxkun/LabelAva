using System.Collections.Generic;
using System.Linq;
using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 重新排序标注命令，用于处理底层数据的排序和编号重计算，并支持撤销
/// </summary>
public class ReorderLabelsCommand : IUndoableCommand
{
    private readonly List<LabelItem> _labels;
    private readonly LabelItem _draggedItem;
    private readonly int _oldIndex;
    private readonly int _newIndex;

    // 缓存操作前的完整状态（顺序和原始编号），用于完美撤销
    private readonly List<LabelItem> _originalState;

    public ReorderLabelsCommand(List<LabelItem> labels, LabelItem draggedItem, int newIndex)
    {
        _labels = labels;
        _draggedItem = draggedItem;
        _oldIndex = labels.IndexOf(draggedItem);
        _newIndex = newIndex;

        // 深拷贝原始状态列表
        _originalState = labels.Select(l => new LabelItem
        {
            ImageName = l.ImageName,
            TextIndex = l.TextIndex,
            X = l.X,
            Y = l.Y,
            GroupIndex = l.GroupIndex,
            Text = l.Text
        }).ToList();
    }

    public void Execute()
    {
        // 1. 移动元素
        _labels.RemoveAt(_oldIndex);

        // 若 oldIndex 在 newIndex 之前，移除操作使后续索引 -1
        int insertIndex = _newIndex;
        if (_oldIndex < insertIndex) insertIndex--;

        _labels.Insert(insertIndex, _draggedItem);
        // 恢复引用（Insert 后 _draggedItem 可能指向已被替换的对象，但 C# 按引用存储，不影响）

        // 2. 重新顺序分配 TextIndex (从 1 开始)
        for (int i = 0; i < _labels.Count; i++)
        {
            _labels[i].TextIndex = i + 1;
        }
    }

    public void Undo()
    {
        // 恢复原始顺序和编号
        _labels.Clear();
        foreach (var originalItem in _originalState)
        {
            // 创建新的对象或恢复属性以触发可能的绑定更新
            var item = new LabelItem
            {
                ImageName = originalItem.ImageName,
                TextIndex = originalItem.TextIndex,
                X = originalItem.X,
                Y = originalItem.Y,
                GroupIndex = originalItem.GroupIndex,
                Text = originalItem.Text
            };
            _labels.Add(item);
        }
    }

    public string Description => "重新排序标注";
}