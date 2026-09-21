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
- 设置中心右侧固定实时预览，修改文字、布局、主题、课表和倒计时后自动更新
- 预览支持当前/模拟日期时间、四种分辨率及上课、课间、周五特殊作息、停课、调课、多倒计时场景
- 预览采用 250ms 防抖且不会直接修改系统壁纸，只有“保存并应用”才会正式生效
- 配置输入未完成时保留最后一次有效预览，并继续支持导入导出与默认恢复
- Schema v4 自动迁移旧课表，并在迁移前保留原配置备份

## 项目结构

- `SteadyDesk/Configuration.cs`：配置模型、默认数据与版本迁移
- `SteadyDesk/ScheduleEngine.cs`：课表标准化、解析、优先级和验证
- `SteadyDesk/WallpaperRenderer.cs`：纯配置驱动的壁纸绘制
- `SteadyDesk/SettingsForm.cs`：设置中心入口与生命周期协调
- `SteadyDesk/SettingsForm.Layout.cs`：设置中心整体布局与七个功能页
- `SteadyDesk/SettingsForm.Data.cs`：控件、草稿配置与实时预览之间的数据同步
- `SteadyDesk/SettingsForm.Schedule.cs`：课表、课程格和特殊日期编辑逻辑
- `SteadyDesk/SettingsForm.ImportExport.cs`：底图与配置的导入、导出和默认恢复
- `SteadyDesk/SettingsForm.Style.cs`：设置中心统一视觉组件
- `SteadyDesk/SettingsPreviewPane.cs`：始终可见的预览画布与场景工具栏
- `SteadyDesk/PreviewCoordinator.cs`：预览防抖、渲染和最后有效画面保护
- `SteadyDesk/PreviewScenarioFactory.cs`：只作用于预览的日期、时间与场景模拟
- `SteadyDesk/SettingsDraftController.cs`：可回退的事务式设置草稿
- `SteadyDesk/ScheduleCellEditorForm.cs`：普通周课表课程格编辑器
- `SteadyDesk/ScheduleDateOverrideEditorForm.cs`：具体日期课程覆盖编辑器
- `SteadyDesk/TrayApplicationContext.cs`：托盘、刷新和系统生命周期
- `SteadyDesk/WindowsServices.cs`：开机启动、设置壁纸和 Wallpaper Engine 检测
- `SteadyDesk.ScheduleChecks/`：无第三方测试框架的最终稳定性验收程序
- `assets/`：内置默认底图

## 运行方式

开发环境需要 .NET 8 SDK 和 Windows。

```powershell
dotnet run --project SteadyDesk/SteadyDesk.csproj
```

运行最终稳定性验收：

```powershell
dotnet run --project SteadyDesk.ScheduleChecks/SteadyDesk.ScheduleChecks.csproj -c Release
```

验收程序覆盖普通与特殊作息边界、具体日期优先级、旧配置迁移、JSON 往返、固定种子随机压力、多分辨率渲染、文件句柄释放、实时预览全部场景、草稿回退和 WinForms 设置界面构造。GitHub Actions 还会发布单文件程序，并在隔离的首次运行目录中真实启动两次完成预览。

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
