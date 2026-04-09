using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabelAva.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    /// <summary>
    /// 状态栏类型枚举
    /// </summary>
    public enum StatusType
    {
        Default,
        Info,
        Success,
        Warn,
        Error
    }

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _zoomText = "缩放: 100%";

    [ObservableProperty]
    private StatusType _currentStatusType = StatusType.Default;

    private int _statusMessageId = 0;

    public bool IsSuccess => CurrentStatusType == StatusType.Success;
    public bool IsWarn => CurrentStatusType == StatusType.Warn;
    public bool IsError => CurrentStatusType == StatusType.Error;


    /// <summary>
    /// 更新状态栏消息
    /// </summary>
    public void UpdateStatus(string message)
    {
        UpdateStatus(message, StatusType.Default);
    }

    /// <summary>
    /// 更新状态栏消息（带类型）
    /// </summary>

    /// to do: change to task
    public async void UpdateStatus(string message, StatusType statusType)
    {
        StatusText = message;
        CurrentStatusType = statusType;
        int currentId = ++_statusMessageId;
        
        if (statusType != StatusType.Default)
    {
        await Task.Delay(100);

        if (currentId == _statusMessageId)
        {
            CurrentStatusType = StatusType.Default;
        }
    }
    }

    /// <summary>
    /// 更新缩放比例显示
    /// </summary>
    public void UpdateZoom(double scalePercent)
    {
        ZoomText = $"缩放: {scalePercent:F0}%";
    }

    partial void OnCurrentStatusTypeChanged(StatusType value)
    {
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsError));
    }

}
