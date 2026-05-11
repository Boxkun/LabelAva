# 光标视觉位置与 CaretIndex 不一致

## 问题描述

选中标记 → 选中图片 → 再次选中标记后，光标应显示在文本末尾，但**视觉上出现在文本开头**。此时 `CaretIndex` 值正确（末尾）。输入任意字符后，光标突然跳到正确位置，字符也插入到末尾。

触发条件：交替选中标记和图片时有几率触发，大概率显示在开头。

## 根因

```csharp
// ❌ 原顺序：Focus 在 CaretIndex 之前
_translationTextBox.Focus();          // → OnGotFocus → ShowCaret() 用旧位置渲染
var len = _translationTextBox.Text?.Length ?? 0;
_translationTextBox.CaretIndex = len; // CaretIndex 改了，但视觉光标已画在旧位置
```

`Focus()` 内部触发 `ShowCaret()`，此时 `CaretIndex` 尚未更新，渲染器用图片选中期间留下的位置 0 画出光标。后续设置 `CaretIndex=len` 只改数据不改渲染，直到下次文本变更才触发重绘。

## 修复

```csharp
// ✅ CaretIndex 移到 Focus 之前
var len = _translationTextBox.Text?.Length ?? 0;
_translationTextBox.CaretIndex = len;
_translationTextBox.Focus();  // ShowCaret 用已更新的 CaretIndex 渲染
```

---

## 涉及代码清单

### 1. OnTreeViewSelectionChanged — 选中 TranslationTreeItem / ImageTreeItem

```csharp
// MainWindow.axaml.cs ~L1667
if (selectedItem is TranslationTreeItem targetChildItem)
{
    // 切换图片视图中的编号高亮
    CanvasControl.HighlightLabel(targetChildItem.Index);

    // 视野居中（按需）
    double currentScale = CanvasWorkspace.ZoomPercent / 100;
    double fitScale = targetRootItem?.FitScale ?? 1.0;
    if (currentScale > fitScale && !_isSelectionFromCanvas)
        CenterOnLabel(targetChildItem.Index);
    _isSelectionFromCanvas = false;

    // ======== 将选中节点的文本写入编辑框 ========
    if (_translationTextBox != null)
    {
        // 1. 设置程序化标志，防止 TextChanged 事件中的逻辑干扰
        _isProgrammaticTextChange = true;

        // 2. 同步阶段：赋值
        _translationTextBox.IsEnabled = true;
        _translationTextBox.Watermark = "请输入文本";
        _translationTextBox.Text = targetChildItem.Text;

        _isProgrammaticTextChange = false;

        // 3. 异步渲染后置阶段：操作焦点与光标
        // 双重 Post：等 Loaded（布局完成）→ Background（落定）后操作
        // 关键：先设 CaretIndex，再 Focus（Focus 触发 ShowCaret 才能用正确位置渲染）
        if (Edit.IsEditMode && !_isUpdatingUI && _settingsProvider.Current.AutoFocusTextBox)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_translationTextBox.IsEnabled)
                    {
                        var len = _translationTextBox.Text?.Length ?? 0;
                        // ✅ 先设光标位置，再 Focus
                        _translationTextBox.CaretIndex = len;
                        _translationTextBox.SelectionStart = len;
                        _translationTextBox.SelectionEnd = len;
                        _translationTextBox.Focus();
                    }
                }, DispatcherPriority.Background);
            }, DispatcherPriority.Loaded);
        }
    }
}
else if (selectedItem is ImageTreeItem)
{
    // 如果选中的是图片本身，禁用编辑框
    CanvasControl.HighlightLabel(-1);

    if (_translationTextBox != null)
    {
        // ⚠ 注意：此处 Text = "" 未包裹 _isProgrammaticTextChange！
        // OnTranslationTextChanged 会以非程序化路径执行
        _translationTextBox.Text = string.Empty;
        _translationTextBox.IsEnabled = false;
        _translationTextBox.Watermark = "选中文本节点以编辑";
    }
}
```

### 2. OnTranslationTextChanged — 文本变更处理

```csharp
// MainWindow.axaml.cs ~L1082
private void OnTranslationTextChanged(object? sender, TextChangedEventArgs e)
{
    // 如果是程序化设置文本，则仅设置光标位置并返回
    if (_isProgrammaticTextChange)
    {
        if (!string.IsNullOrEmpty(_translationTextBox?.Text))
        {
            var len = _translationTextBox.Text.Length;
            _translationTextBox.CaretIndex = len;       // 强制光标到末尾
            _translationTextBox.SelectionStart = len;
            _translationTextBox.SelectionEnd = len;
        }
        return;
    }

    // 在 UI 重建期间，忽略文本改变事件
    if (_isUpdatingUI || !Edit.IsEditMode || _translationTextBox == null) return;

    if (Navigation.SelectedItem is TranslationTreeItem selectedTreeItem)
    {
        // 仅同步修改树节点的显示文本（不修改底层数据模型）
        selectedTreeItem.Text = _translationTextBox.Text ?? string.Empty;
    }
}
```

### 3. ApplyDligConfig — 字体/FontFeatures 设置（完整）

```csharp
// MainWindow.axaml.cs ~L331
private void ApplyDligConfig()
{
    if (_translationTextBox == null) return;

    var configName = _settingsProvider.Current.ActiveDligConfig;
    if (string.IsNullOrWhiteSpace(configName))
    {
        _translationTextBox.ClearValue(TextBox.FontFamilyProperty);
        _translationTextBox.ClearValue(TextBox.FontFeaturesProperty);
        Edit.QuickInputSlots.Clear();
        return;
    }

    var config = DligConfigService.LoadConfig(configName);
    if (config == null)
    {
        _translationTextBox.ClearValue(TextBox.FontFamilyProperty);
        _translationTextBox.ClearValue(TextBox.FontFeaturesProperty);
        Edit.QuickInputSlots.Clear();
        StatusBar.UpdateStatus($"连字配置 '{configName}' 加载失败，已回退到默认",
            StatusBarViewModel.StatusType.Warn);
        return;
    }

    Edit.QuickInputSlots.Clear();
    if (config.QuickInputs != null)
        foreach (var slot in config.QuickInputs)
            Edit.QuickInputSlots.Add(slot);

    if (string.IsNullOrWhiteSpace(config.FontFamily)) return;

    var fontFamily = new FontFamily(config.FontFamily);
    var typeface = new Typeface(fontFamily);
    var fontInstalled = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface)
        && string.Equals(glyphTypeface.FamilyName, config.FontFamily,
            StringComparison.OrdinalIgnoreCase);

    if (!fontInstalled)
    {
        StatusBar.UpdateStatus($"字体 '{config.FontFamily}' 未安装，连字功能不可用",
            StatusBarViewModel.StatusType.Warn);
        return;
    }

    // ⚠ 这行触发 TextBox.InvalidateMeasure()，进而重建 TextLayout，影响光标坐标映射
    _translationTextBox.FontFamily = fontFamily;
    QuickInputItemsControl.FontFamily = fontFamily;

    FontFeatureCollection? features = null;
    if (!string.IsNullOrWhiteSpace(config.FontFeatures))
    {
        features = new FontFeatureCollection();
        foreach (var part in config.FontFeatures.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            features.Add(FontFeature.Parse(part));

        _translationTextBox.FontFeatures = features;
        QuickInputItemsControl.FontFeatures = features;
    }
}
```

### 4. ApplyDligConfig 的触发点

```csharp
// ① 启动初始化（MainWindow.axaml.cs ~L257）
_isInitialized = true;
DligConfigService.EnsureDirectory();
ApplyDligConfig();

// ② 进入编辑模式（OnEditModeChanged ~L936）
// 延迟到 Render 优先级——此时 EditPanel 才变为可见，TextBox 完成布局
if (Edit.IsEditMode)
{
    UpdateGroupButtonColors();
    Dispatcher.UIThread.Post(ApplyDligConfig, DispatcherPriority.Render);
}

// ③ 设置变更（OnSettingsChanged ~L300）
if (changes.HasFlag(SettingsChangeKind.DligConfig))
{
    ApplyDligConfig();
}
```

### 5. 时序图

```
选中标记                           选中图片                    再次选中标记
    │                                  │                            │
    │ Text = "..."                     │ Text = "" ⚠               │ Text = "..."
    │ CaretIndex=len (OnTextChanged)   │ IsEnabled = false          │ CaretIndex=len (OnTextChanged)
    │ Post(Loaded→Background):         │                            │ Post(Loaded→Background):
    │   CaretIndex=len                 │                            │   CaretIndex=len ✅
    │   Focus → ShowCaret(len) ✓       │                            │   Focus → ShowCaret(len) ✓
    │                                  │                            │
```

### 6. 干扰源列表

| 代码位置 | 操作 | 影响 |
|---------|------|------|
| `ApplyDligConfig` L383 | `_translationTextBox.FontFamily = fontFamily` | 触发 `InvalidateMeasure`，重建 TextLayout，可能导致 CaretIndex→像素坐标映射失效 |
| `ApplyDligConfig` L393 | `_translationTextBox.FontFeatures = features` | 触发文本重整形（HarfBuzz），改变 glyph 位置 |
| `OnTranslationTextChanged` L1089 | `CaretIndex = len`（`_isProgrammaticTextChange=true` 时） | 将光标强制移到末尾——但与图片选中时的 `Text = ""` 交互产生中间态 |
| `OnTreeViewSelectionChanged` L1725 | `Text = string.Empty`（**未包裹** `_isProgrammaticTextChange`） | 走非程序化路径，触发 `OnTranslationTextChanged` 正常分支 |
| `OnEditModeChanged` L942 | `Post ApplyDligConfig at Render` | 进入编辑模式时异步设置字体，与光标定位的 Loaded→Background Post 处于同一调度周期 |

---

## 附录：完整关键代码

### A. OnQuickInputButtonClick

```csharp
// MainWindow.axaml.cs ~L974
private void OnQuickInputButtonClick(object? sender, RoutedEventArgs e)
{
    if (sender is Button button && button.DataContext is QuickInputSlot slot)
    {
        if (!string.IsNullOrEmpty(slot.Character) && _translationTextBox != null 
            && _translationTextBox.IsEnabled)
        {
            var caretIndex = _translationTextBox.CaretIndex;
            var currentText = _translationTextBox.Text ?? "";
            var newText = currentText.Insert(caretIndex, slot.Character);
            //                                    ↑ 在光标位置插入配置的字符

            _isProgrammaticTextChange = true;
            _translationTextBox.Text = newText;                     // → OnTranslationTextChanged
            _translationTextBox.CaretIndex = caretIndex + slot.Character.Length;
            _isProgrammaticTextChange = false;

            _translationTextBox.Focus();
            CommitCurrentEdit();    // → ChangeTextCommand → History.ExecuteCommand
        }
    }
}
```

**副作用**：`CommitCurrentEdit()` → `ChangeTextCommand` → `HistoryManager.ExecuteCommand` → `HistoryChanged` → `RebuildCurrentView`（重建 TreeView + Canvas）。

### B. EditViewModel.IsEditPanelVisible

```csharp
// ViewModels/EditViewModel.cs
public partial class EditViewModel : ObservableObject
{
    [ObservableProperty] private bool _isEditMode;        // 源字段

    public bool IsEditPanelVisible => IsEditMode;          // 派生属性，纯 getter，无 setter
    public string EditModeButtonText => IsEditMode ? "编辑模式" : "查看模式";
    public bool AreGroupButtonsVisible => IsEditMode;

    partial void OnIsEditModeChanged(bool value)
    {
        UpdateDerivedProperties();      // 触发 IsEditPanelVisible 等属性的 PropertyChanged
        ToggleEditModeCommand.NotifyCanExecuteChanged();
        if (CanToggleEditMode)
            EditModeChanged?.Invoke(this, EventArgs.Empty);  // → MainWindow.OnEditModeChanged
    }
}
```

`IsEditPanelVisible` 是 `IsEditMode` 的纯派生，无独立 setter。`IsEditMode` 变更 → `UpdateDerivedProperties()` → `OnPropertyChanged(nameof(IsEditPanelVisible))`。

### C. NavigationViewModel 选中链

```csharp
// ViewModels/NavigationViewModel.cs

// SelectedItem 由任意源（热键、代码、TreeView）修改时触发
partial void OnSelectedItemChanged(object? value)
{
    OnSelectedItemChangedFromVM();      // → SelectedItemChanged 事件
}

private void OnSelectedItemChangedFromVM()
{
    SelectedItemChanged?.Invoke(this, EventArgs.Empty);    // → MainWindow.OnNavigationSelectedItemChanged
    HandleImageSwitchFromSelection();                      // 自动切换图片
    HandleAccordionFromSelection();                        // 手风琴效果
}

// MainWindow.axaml.cs 中的订阅链:
// InitializeAsync():
ViewModel.Navigation.SelectedItemChanged += OnNavigationSelectedItemChanged;

private void OnNavigationSelectedItemChanged(object? sender, EventArgs e)
{
    if (_isSyncingSelection) return;
    if (Navigation.SelectedItem != null && ImageTreeView.SelectedItem != Navigation.SelectedItem)
    {
        _isSyncingSelection = true;
        ImageTreeView.SelectedItem = Navigation.SelectedItem;  // 同步 TreeView
        _isSyncingSelection = false;
    }
}

// 同步完成后 TreeView 触发 SelectionChanged:
// MainWindow.axaml:
<TreeView SelectionChanged="OnTreeViewSelectionChanged" />

// MainWindow.axaml.cs ~L1630:
private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    // 这里的代码就是上文 §1 的完整逻辑
    // selectedItem is TranslationTreeItem → 设 TextBox.Text + Post 光标
    // selectedItem is ImageTreeItem       → 清 TextBox.Text + 禁用
}
```

**事件链**：`SelectedItem` 属性变更 → `OnSelectedItemChanged`（partial）→ `OnSelectedItemChangedFromVM` → `SelectedItemChanged` 事件 → `OnNavigationSelectedItemChanged` → 同步 TreeView.SelectedItem → `OnTreeViewSelectionChanged` → 设 TextBox。

### D. 当前 TextBox 操作总览

| 操作 | 触发位置 | 设置方式 | `_isProgrammaticTextChange` |
|------|---------|---------|---------------------------|
| 选中标记 | `OnTreeViewSelectionChanged` | `TextBox.Text = targetChildItem.Text` | ✅ 包裹 |
| 选中图片 | `OnTreeViewSelectionChanged` | `TextBox.Text = string.Empty` | ✅ 已修复 |
| 快捷输入 | `OnQuickInputButtonClick` | `TextBox.Text.Insert(caret, char)` | ✅ 包裹 |
| 用户键入 | XAML `TextChanged` 事件 | 系统自动 | ❌ 无（正常路径） |
| Undo/Redo | `RebuildCurrentView` | `TextBox.Text = ...` | 由 `_isUpdatingUI` 保护 |
