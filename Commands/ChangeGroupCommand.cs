using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 修改分组命令
/// </summary>
public class ChangeGroupCommand : IUndoableCommand
{
    private readonly LabelItem _label;
    private readonly int _oldGroupIndex;
    private readonly int _newGroupIndex;
    
    public string Description => "修改分组";
    
    public ChangeGroupCommand(LabelItem label, int oldGroupIndex, int newGroupIndex)
    {
        _label = label;
        _oldGroupIndex = oldGroupIndex;
        _newGroupIndex = newGroupIndex;
    }
    
    public void Execute()
    {
        _label.GroupIndex = _newGroupIndex;
    }
    
    public void Undo()
    {
        _label.GroupIndex = _oldGroupIndex;
    }
}
