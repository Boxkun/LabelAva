using LabelAva.Models;

namespace LabelAva.Commands;

/// <summary>
/// 移动标注命令
/// </summary>
public class MoveLabelCommand : IUndoableCommand
{
    private readonly LabelItem _label;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly double _newX;
    private readonly double _newY;
    
    public string Description => "移动标注";
    
    public MoveLabelCommand(LabelItem label, double oldX, double oldY, double newX, double newY)
    {
        _label = label;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }
    
    public void Execute()
    {
        _label.X = _newX;
        _label.Y = _newY;
    }
    
    public void Undo()
    {
        _label.X = _oldX;
        _label.Y = _oldY;
    }
}
