# 图片管理架构重构

## 背景

LabelAva 的翻译标注数据存储格式为自描述文本文件（`.txt`），内部以行为单位记录标注项，按图片文件名分组。存档格式不存储图片的绝对路径——打开翻译文件时，软件默认在与翻译文件相同的目录下按文件名匹配图片。

```
TranslationData.ImageLabels: Dictionary<string, List<LabelItem>>
                                  ↑                    ↑
                            图片文件名（如 "photo.png"）   该图片下的所有标注
```

当前代码库中存在**三个与图片管理相关的窗口**，分别在不同阶段介入：

| 窗口 | 触发时机 | 职责 |
|------|---------|------|
| `ImageSelectionWindow` | 新建翻译文件时 | 扫描文件夹、勾选图片、拖拽排序、输入文件名 |
| `ImageAssociationWindow` | 打开翻译文件时（自动）/ 菜单手动触发 | 验证图片是否存在、修复扩展名不匹配、修正格式不符 |
| （无） | 项目编辑阶段 | 增删图片、调整顺序 |

第三个窗口缺失——项目创建后，用户无法增删或重排图片。

---

## 现有架构

### ImageSelectionWindow（图片选择窗口）

**代码位置**：`Views/ImageSelectionWindow.axaml` + `.axaml.cs`（~400 行）

**流程**：
1. `DocumentViewModel.CreateNewTranslationAsync()` 扫描用户选定的文件夹，收集所有支持的图片格式（`.jpg/.jpeg/.png/.bmp/.gif/.tif/.tiff/.webp`）
2. 弹出 `ImageSelectionWindow`，按文件名自然排序展示所有图片
3. 用户勾选需要的图片（默认全选）、拖拽调整顺序、输入翻译文件名
4. 确认后返回 `SelectedImagePaths`（有序）和 `FileName`

**关键特征**：
- 内置自然排序（`NaturalSort`，按数字段数值比较，~40 行）
- 拖拽重排实现（~160 行）：`PointerPressed → PointerMoved → PointerReleased` 三阶段，含拖拽预览浮层和插入指示线
- 右侧预览面板：选中图片时异步加载缩略图（`ImageLoader.LoadScaled`）
- 在 Avalonia `ListBox` 上实现，`SelectionMode="Toggle"` 配合 `CheckBox` 做多选

**局限**：仅用于新建项目，项目创建后再也无法打开。

---

### ImageAssociationWindow（文件关联管理器）

**代码位置**：`Views/ImageAssociationWindow.axaml` + `.axaml.cs`（~790 行）

**流程**：
1. `DocumentViewModel` 解析翻译文件后，调用 `ImageValidationService.Validate()` 逐文件检查 `File.Exists`
2. 如有缺失或格式问题 → 弹出 `ImageAssociationWindow`（模态）
3. 窗口展示每张图片的状态（OK / Missing），用户可为缺失图片手动浏览定位新路径
4. 确认后返回 `ImageAssociationResult`（含 `FolderPath`、`Remappings`、`WriteToFile` 标志）

**内置两阶段自动检测**（`CheckAutoMatch()`，~170 行）：

| 阶段 | 检测内容 | 触发条件 | Banner 样式 |
|------|---------|---------|------------|
| Phase 1 | 图片缺失但文件夹中有同名不同扩展名的文件（如翻译文件引用 `photo.jpg`，文件夹中有 `photo.png`） | `Status == Missing && NewPath 为空` | 蓝色信息横幅，按钮"填入" |
| Phase 2 | 文件存在但魔数签名与扩展名不符（如 `photo.png` 实际是 JPEG 编码） | 文件存在，`CheckFormatConsistency()` 返回不一致 | 红色错误横幅，按钮"修正" |

**Banner 动画**（`FlashBanner()`）：先闪色 80ms → 0.8s 过渡到柔和背景色，支持亮/暗色模式。

**辅助弹窗**：
- `ShowWarningDialog()`（~70 行）：代码构建的警告弹窗，仍有未关联图片时提示"继续 / 返回"
- `OnViewDetails()`（~190 行）：代码构建的详情表格，展示 Phase 2 中每张问题图片的实际格式

**两种结果模式**（`ApplyAssociationResult()`）：

| 模式 | WriteToFile | 行为 |
|------|------------|------|
| Remap | `true` | 重命名 `ImageLabels` 字典 key + 所有 `LabelItem.ImageName` + 写回 `.txt` |
| Redirect | `false` | 存入 `ImagePathMapping` 字典，纯内存映射，不持久化 |

**局限**：
- 仅处理已有图片引用的路径验证，不涉及图片集合本身的编辑
- `ImageValidationStatus` 枚举只有 `OK` / `Missing` 两态——Phase 2 检测到格式问题时，Banner 报错但列表项仍显示 "✓ 正常"，用户感到困惑
- Redirect 模式存在价值存疑：翻译文件不存绝对路径，映射仅在当前会话有效

---

### ImageValidationService（图片验证服务）

**代码位置**：`Services/ImageValidationService.cs`（~200 行）

**核心能力**：

| 方法 | 功能 |
|------|------|
| `Validate(folder, imageNames)` | 批量 `File.Exists` 检查 |
| `ValidateSingleWithText(folder, imageName)` | 单文件存在检查 + 状态文本 |
| `ValidateFullPath(fullPath)` | 绝对路径检查（用于用户手动浏览后的验证） |
| `CheckFormatConsistency(filePath)` | 魔数签名检测：JPEG/Png/GIF/BMP/WebP/TIFF |
| `FindAlternateExtensionMatches(folder, missingNames)` | 对缺失图片，在文件夹中查找同名不同扩展名的文件 |
| `HasAnyFormatIssue(folder, items)` | 批量检查是否有任何格式不一致 |

---

### DocumentViewModel 中的调用链

```
OpenTranslationFileAsync()
  ├── TranslationParser.Load()          // 解析 .txt
  ├── ImageValidationService.Validate() // 逐文件检查存在性
  ├── [有缺失或格式问题]
  │     └── _showImageAssociationDialog → ImageAssociationWindow
  │           └── ApplyAssociationResult()
  │                 ├── Remap:  重命名 ImageLabels key → IsDirty = true
  │                 └── Redirect: 存入 ImagePathMapping
  └── DocumentOpened 事件 → Navigation.BuildTreeView() → MainWindow 加载第一张图

CreateNewTranslationAsync()
  ├── 扫描文件夹 → 收集支持的图片文件
  ├── _showImageSelectionDialog → ImageSelectionWindow
  │     └── 返回 SelectedImagePaths + FileName
  ├── 构建 TranslationData（每张图一个空 List<LabelItem>）
  ├── TranslationParser.Save()
  └── [有格式问题] → _showImageAssociationDialog // 新建后再次检查
```

---

## 识别的问题

### 问题一：图片组织功能缺失

项目创建后，`ImageLabels` 字典的内容无法通过 UI 修改。用户不能：
- 添加新图片到项目中
- 删除已有图片（及级联删除其标注）
- 调整图片在导航树中的显示顺序

`ImageSelectionWindow` 已经具备了列表展示、多选、拖拽排序的完整 UI 能力，但它仅服务于新建项目这一条路径。

**根因**：`ImageSelectionWindow` 的返回值为 `SelectedImagePaths`（新建语义），没有"接收已有数据、直接修改"的管理模式。

### 问题二：Banner 与列表项状态不一致

`ImageValidationStatus` 枚举只有两态：

```csharp
public enum ImageValidationStatus
{
    OK,      // 文件存在
    Missing, // 文件不存在
}
```

但 `CheckAutoMatch()` Phase 2 检测到的是**第三种状态**：文件存在，但扩展名与实际格式不符。此时：
- Banner 显示红色警告："发现 N 张图片的实际格式与扩展名不符"
- 列表项仍显示 "✓ 正常"（因为 `ValidateSingleWithText` 只检查 `File.Exists`）

用户在 Banner 下面看到的列表与 Banner 描述矛盾：Banner 说有问题，列表说一切正常。

### 问题三：Redirect 模式没有实际意义

翻译文件不存储绝对路径，`ImagePathMapping` 是纯内存结构。用户选择"不写入文件"时，映射在当前会话有效，关闭文档后丢失。

唯一可能合理的场景：格式冲突修正时，用户想先预览修正后的图片再决定是否持久化。但这个场景完全可以用"确认前预览"替代，不需要一个持久存在的双模式架构。

### 问题四：FormatMismatch 缺乏可见度

`CheckAutoMatch` Phase 2 使用魔数签名检测格式不一致。这个问题对标注流程有实际影响——格式错误的图片可能导致 `ImageLoader` 解码失败。但目前：
- 问题通过顶部横幅展示，不体现在列表项状态中
- 用户只能通过"详情"弹窗查看具体是哪些图片
- 如果用户关闭窗口后再次打开，检测重新运行，体验不连贯

---

## 最终决定

### 目标一：图片管理功能合并进 ImageAssociationWindow

**决定**：不新建独立窗口，不升级 `ImageSelectionWindow`。将图片增删排序能力直接合并进 `ImageAssociationWindow`。

**原因**：`ImageAssociationWindow` 的弹出条件是完备的自动机——无冲突不弹窗，有冲突必须解决才能继续。不存在"用户想主动打开但当前无冲突"的场景，因此不存在两个窗口语义混淆的风险。菜单入口可以保留用于手动触发，但本质上用户只面对一个统一的"图片管理+关联修复"界面。

**新增能力**：

| 能力 | 来源 | 说明 |
|------|------|------|
| 排序 | 复用 `ImageSelectionWindow` 现成的拖拽排序实现（~160 行） | 图片顺序由 `.txt` 文件中行块的出现顺序决定，无独立索引字段，保存时按新顺序重写对应行块即可 |
| 添加图片 | 新增 | 仅对新增图片调用 `CheckFormatConsistency`；已有列表项不重复全量校验——它们必然已在创建或打开流程中被验证过一次 |
| 删除图片 | 新增 | 破坏性操作（级联删除 `LabelItem`），需要独立的二次确认流程，不复用 `ShowWarningDialog` |

### 目标二：ImageValidationStatus 增加 FormatMismatch 状态

**决定**：维持原判。独立优先实施，与其他改动无耦合，可单独上线。

- 枚举增加 `FormatMismatch`（文件存在但扩展名与实际格式不符）
- `CheckAutoMatch` Phase 2 同步更新对应 item 的 `Status` 和 `StatusText`
- `StatusForeground` 对 `FormatMismatch` 使用琥珀/橙色，区别于 Missing 的红色
- Banner 与列表项语义一致：Banner 报什么，列表就显示什么

### 目标三：WriteToFile/Redirect 双模式

**决定**：保留"不写盘"的能力，改变呈现方式。

不删除 `ImagePathMapping` 机制本身——"应用但不写盘"是一个有意义的操作。问题出在原方案的 UI：一个隐藏在确认对话框里的 `CheckBox` 容易引起误解，且语义不明确。

**改为两个明确按钮**：

```
[应用但不保存]  [保存并应用]
```

- "保存并应用"：持久化到 `.txt` 文件（原 `WriteToFile = true`）
- "应用但不保存"：仅写入 `ImagePathMapping`，会话有效（原 `WriteToFile = false`）

语义外显，用户不会误解每个按钮的后果。

### 目标四：图片顺序的实现基础

**决定**：无需额外索引迁移层。

图片顺序完全由 `.txt` 文件中出现的行序决定，没有独立存储的索引字段。管理窗口的拖拽重排在保存时直接按新顺序重写对应行块，与 `ImageSelectionWindow` 现有的写入逻辑本质相同，可直接复用。

---

## 遗留的工程取舍（非架构问题）

`ImageAssociationWindow` 目前 ~790 行 code-behind，本次改动将新增拖拽排序（~160 行）和级联删除确认逻辑。是否需要将「列表 + 状态徽标 + 拖拽排序」抽成独立可复用单元（`UserControl` 或 `partial class`），取决于团队后续迭代频率，不影响功能正确性。
