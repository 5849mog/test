# 稳序桌面

> 一个看似简洁、实际高度可定制的 Windows 学习桌面壁纸工具。

## 当前能力

- 自动生成课程表、倒计时、进度条、实时时间和激励语壁纸
- 一模、中考以及任意自定义考试事件
- 托盘驻留、开机启动、原壁纸备份与恢复
- 屏幕分辨率变化、跨分钟和跨天自动刷新
- 自定义底图、课表位置、课程内容和显示组件
- 设置中心内置实时预览
- 支持配置导入、导出和恢复默认

## 项目结构

- SteadyDesk/Configuration.cs：统一配置模型、默认数据和配置迁移
- SteadyDesk/WallpaperRenderer.cs：根据配置绘制壁纸
- SteadyDesk/SettingsForm.cs：控制中心与实时预览
- SteadyDesk/TrayApplicationContext.cs：托盘、刷新和系统生命周期
- SteadyDesk/WindowsServices.cs：开机启动、设置壁纸和 Wallpaper Engine 检测
- assets/：内置默认底图

## 运行方式

开发环境需要 .NET 8 SDK 和 Windows。直接运行：

    dotnet run --project SteadyDesk/SteadyDesk.csproj

生成单文件程序：

    dotnet publish SteadyDesk/SteadyDesk.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

程序会在首次启动时创建：

    %LOCALAPPDATA%/稳序桌面/

其中保存配置、当前壁纸、预览图、默认底图和原壁纸备份。

## 目标日期

默认配置包含：

- 一模考试：2027-01-12
- 中考：2027-06-19

这些日期已经从渲染器中抽离，可以直接在设置中心新增、删除和修改。

## 兼容说明

旧版本的配置会尝试从原来的本地配置目录读取，并迁移底图、课表位置和原壁纸路径。旧目录名称只用于兼容迁移，不再作为软件显示名称。
