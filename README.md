# 稳序桌面

> 一个看似简洁、实际高度可定制的 Windows 学习桌面壁纸工具。

## 当前能力

- 自动生成课程表、倒计时、进度条、实时时间和激励语壁纸
- 一模、中考以及任意自定义考试事件
- 托盘驻留、开机启动、原壁纸备份与恢复
- 屏幕分辨率变化、跨分钟和跨天自动刷新
- 自定义底图、布局、配色、文案、课程和显示组件
- 周一至周五可分别设置课程类型与时间，空课不会被误判为正在上课
- 支持星期五等特殊作息，以及按具体日期停课、套用工作日或逐节覆盖
- 设置中心内置实时预览、配置导入导出与默认恢复
- Schema v4 自动迁移旧课表，并在迁移前保留原配置备份

## 项目结构

- `SteadyDesk/Configuration.cs`：配置模型、默认数据与版本迁移
- `SteadyDesk/ScheduleEngine.cs`：课表标准化、解析、优先级和验证
- `SteadyDesk/WallpaperRenderer.cs`：纯配置驱动的壁纸绘制
- `SteadyDesk/SettingsForm.cs`：控制中心与实时预览
- `SteadyDesk/ScheduleCellEditorForm.cs`：普通周课表课程格编辑器
- `SteadyDesk/ScheduleDateOverrideEditorForm.cs`：具体日期课程覆盖编辑器
- `SteadyDesk/TrayApplicationContext.cs`：托盘、刷新和系统生命周期
- `SteadyDesk/WindowsServices.cs`：开机启动、设置壁纸和 Wallpaper Engine 检测
- `SteadyDesk.ScheduleChecks/`：不依赖第三方测试框架的课表边界检查
- `assets/`：内置默认底图

## 运行方式

开发环境需要 .NET 8 SDK 和 Windows。

```powershell
dotnet run --project SteadyDesk/SteadyDesk.csproj
```

运行课表行为检查：

```powershell
dotnet run --project SteadyDesk.ScheduleChecks/SteadyDesk.ScheduleChecks.csproj -c Release
```

生成 Windows x64 单文件程序：

```powershell
dotnet publish SteadyDesk/SteadyDesk.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

程序会在首次启动时创建：

```text
%LOCALAPPDATA%/稳序桌面/
```

其中保存配置、当前壁纸、预览图、默认底图和原壁纸备份。

## 课表判定规则

课表先按普通工作日模板解析，再叠加具体日期规则。具体日期规则优先，单个课程格的特殊时间又优先于该节默认时间。当前课程采用“开始时刻包含、结束时刻不包含”的边界，并明确排除空课。

星期五等特殊作息不需要复制整张课表：双击对应课程格，只覆盖当天这一节的起止时间即可。临时停课、补课或调课则在“特殊日期”中配置。

## 兼容说明

Schema v3 及更早课表会在首次加载时迁移到 v4；迁移前的 JSON 会以带版本号和时间戳的文件名保留。旧本地目录名称只用于读取历史设置，不再作为软件显示名称。
