# LabelAva

LabelAva 是由 [Avalonia UI](https://avaloniaui.net/) 驱动的现代跨平台 [LabelPlus](https://github.com/LabelPlus/LabelPlus) 实现，拥有重新设计的交互模式，原生支持 Windows、Linux 和 macOS。

[![AGPL License](https://img.shields.io/badge/License-AGPL-blue.svg)](http://www.gnu.org/licenses/agpl-3.0)
[![Publish](https://github.com/Boxkun/LabelAva/actions/workflows/publish.yml/badge.svg)](https://github.com/Boxkun/LabelAva/actions/workflows/publish.yml)
## 特性

- **跨平台** — 支持 x64 / arm64 架构，支持 Windows / Linux / macOS
- **图片扩展名校验** — 校验文件魔数是否与扩展名匹配（JPG/PNG/GIF/BMP/WebP/TIFF）
- **DLIG 连字** — 可使用支持 OpenType 自由连字特性的字体渲染文本

## 构建

```bash
git clone https://github.com/Boxkun/LabelAva
cd LabelAva
dotnet restore
dotnet build -c Release 
dotnet run
```

## 快速上手

### 新建与打开文件
 - 使用新建翻译按钮选择工作目录，预览并指定要添加到文件的图片
 - 使用打开翻译按钮或是拖动翻译文件到窗口内来打开翻译文件

### 画布导航方式
 - 按住鼠标右键拖动来移动画布
 - 滚动鼠标滚轮来缩放

### 编辑标记
 - 通过右上角按钮切换在查看模式与编辑模式间切换
 - 在编辑模式下，鼠标左键单击添加标记，按住并拖动来调整标记位置
 - 使用 Ctrl + ↑ / ↓ 切换标记，Ctrl + ← / → 切换图片
 - 使用 Ctrl + 1 / Ctrl + 2 随时切换 框内 / 框外 分组
  
> [!NOTE]
> 在首选项中可以调整在画布中新建 / 选中标记时是否自动聚焦到文本框，以获得与 [LabelPlus](https://github.com/LabelPlus/LabelPlus) / [LabelPlusFX](https://github.com/Meodinger/LabelPlusFX) 相似的体验

### 自由连字渲染与快速输入
LabelAva支持对标记列表与输入文本框应用支持自由连字特性的字体，并自定义连字快捷输入规则。`/dlig_conf` 文件夹中存放了一些示例文件。

在首选项字体标签页点击打开配置目录，在其中放置配置文件之后，重新开启首选项窗口即可启用功能。

#### 目前已有配置文件的字体
 - 攸望字体

## 许可
LabelAva 是自由软件。本项目遵循 [GNU Affero General Public License v3.0](LICENSE) 协议开源。
