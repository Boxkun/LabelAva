using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabelAva.Models;

public enum ImageValidationStatus
{
    OK,
    Missing,
}

public class ImageAssociationItem : INotifyPropertyChanged
{
    private ImageValidationStatus _status;
    private string _statusText = string.Empty;
    private string? _newPath;

    public string ImageName { get; set; } = string.Empty;

    public ImageValidationStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusForeground));
            }
        }
    }

    public string StatusForeground => _status == ImageValidationStatus.Missing ? "#F44336" : "#000000";

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string? NewPath
    {
        get => _newPath;
        set
        {
            if (_newPath != value)
            {
                _newPath = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ImageAssociationResult
{
    public string FolderPath { get; set; } = string.Empty;
    public bool WriteToFile { get; set; }
    public Dictionary<string, string> Remappings { get; set; } = new();
}
