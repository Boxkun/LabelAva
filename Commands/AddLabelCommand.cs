using System.Collections.Generic;
using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 添加标注命令
/// </summary>
public class AddLabelCommand : IUndoableCommand
{
    private readonly List<LabelItem> _labelList;
    private readonly LabelItem _newItem;
    
    public string Description => "添加标注";
    
    public AddLabelCommand(List<LabelItem> labelList, LabelItem newItem)
    {
        _labelList = labelList;
        _newItem = newItem;
    }
    
    public void Execute()
    {
        _labelList.Add(_newItem);
    }
    
    public void Undo()
    {
        _labelList.Remove(_newItem);
    }
}
