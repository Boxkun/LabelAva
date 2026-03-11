using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 修改文本命令
/// </summary>
public class ChangeTextCommand : IUndoableCommand
{
    private readonly LabelItem _label;
    private readonly string _oldText;
    private readonly string _newText;
    
    public string Description => "修改文本";
    
    public ChangeTextCommand(LabelItem label, string oldText, string newText)
    {
        _label = label;
        _oldText = oldText;
        _newText = newText;
    }
    
    public void Execute()
    {
        _label.Text = _newText;
    }
    
    public void Undo()
    {
        _label.Text = _oldText;
    }
}
