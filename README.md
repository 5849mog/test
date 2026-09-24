# 稳序桌面

> 一个看似简洁、实际高度可定制的 Windows 学习桌面壁纸工具。

## 当前能力

- 自动生成课程表、倒计时、进度条、实时时间和激励语壁纸
- 一模、中考以及任意自定义考试事件
- 托盘驻留、开机启动、原壁纸备份与恢复
- 从腾讯云 COS 检查签名更新；下载后校验 SHA-256，再安全替换并在启动失败时回滚
- 学校网络短暂离线时继续使用当前版本；更新检查只访问 COS，不依赖 GitHub
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

- `SteadyDesk/Configuration.cs`：配置模型、默认数据与本产品配置版本迁移
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
- `SteadyDesk/AppUpdateClient.cs`：COS 更新清单验签、版本检查与限量下载
- `SteadyDesk.UpdateHelper/`：关闭主程序后替换文件、保留旧版并在启动失败时回滚
- `SteadyDesk/WindowsServices.cs`：开机启动、设置壁纸和 Wallpaper Engine 检测
- `SteadyDesk.ScheduleChecks/`：无第三方测试框架的稳定性与更新签名验收程序
- `scripts/`：本机生成签名密钥，以及为 COS 更新清单签名
- `.github/workflows/release.yml`：构建发布 ZIP、生成签名清单并按配置上传 COS
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

验收程序覆盖普通与特殊作息边界、具体日期优先级、旧配置迁移、JSON 往返、固定种子随机压力、多分辨率渲染、文件句柄释放、实时预览全部场景、草稿回退、WinForms 设置界面构造，以及更新清单签名、源站、大小和异常字段。GitHub Actions 还会在隔离的首次运行目录中启动发布版并打出首次安装包与增量更新包。

生成 Windows x64 单文件程序：

```powershell
dotnet publish SteadyDesk/SteadyDesk.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

程序会在首次启动时创建独立的数据目录：

```text
%LOCALAPPDATA%/稳序桌面/
```

其中保存配置、当前壁纸、预览图、默认底图和原壁纸备份。首次运行使用稳序桌面的默认配置，不读取“郑老师中考倒计时”的历史设置；两款程序的数据与后续更新生命周期相互独立。

## 安装与自动更新

更新版采用固定的当前用户安装目录：

```text
%LOCALAPPDATA%/Programs/SteadyDesk/
```

首次部署请使用完整包 `steady-desk-win-x64.zip`，解压到上面的目录后，从该目录运行 `稳序桌面.exe`。包内包含主程序和更新辅助程序。之后的更新包 `steady-desk-update-win-x64.zip` 只包含主程序，由辅助程序关闭并替换文件。旧版“郑老师中考倒计时”不能原地升级成自动更新版，因此需要先人工部署这一次；此后才由稳序桌面自己更新。

程序会安静地检查 COS 上的 `releases/stable/latest.json`，发现新版时提示用户。更新包下载完成后，会核对清单签名、文件大小和 SHA-256；只有全部通过才会安装。网络中断时保留已下载片段，下次检查通过 HTTP Range 继续下载；安装失败会恢复上一份程序。断网时保留当前版本，稍后可在托盘菜单中手动选择“检查更新”。学校电脑无需连接 GitHub。

### 维护者发布更新

发布流程已放入 GitHub Actions。仓库管理员（腾讯云账户由家长或监护人管理）先配置以下项目；不要把私钥或腾讯云密钥写入源码：

| 类型 | 名称 | 内容 |
|---|---|---|
| Repository variable | `UPDATE_PUBLIC_KEY_BASE64` | 公钥 PEM 内容的 Base64，由 `scripts/Generate-UpdateSigningKey.ps1` 生成 |
| Repository secret | `STEADYDESK_UPDATE_PRIVATE_KEY_PEM` | 对应的 RSA 私钥 PEM |
| Repository variable | `COS_BUCKET` | COS 存储桶完整名称；当前 URL 对应 `my-1253786342` |
| Repository secret | `TENCENT_SECRET_ID` | 仅供发布流程使用的 COS 上传身份 |
| Repository secret | `TENCENT_SECRET_KEY` | 对应的 COS 上传密钥 |

在 Windows PowerShell 7 中生成密钥对：

```powershell
pwsh -File scripts/Generate-UpdateSigningKey.ps1
```

公钥文件可配置为 GitHub 仓库变量；私钥只交由仓库管理员安全保存并配置为 Secret，切勿提交到 Git 或发到聊天里。签名私钥和程序内置公钥必须配对，否则发布流程会停止。

配置完成后，发布 `v1.2.0` 这类稳定版本标签，`Release SteadyDesk` 工作流会构建完整安装包、增量更新包和已签名的 `latest.json`。它先上传版本目录下的两个 ZIP，最后才覆盖 `releases/stable/latest.json`。COSCMD 按腾讯云文档通过 pip 安装，并以存储桶名和地域配置；当前发布地域为 `ap-shanghai`。若没有设置 COS 上传凭据，工作流仍会生成 GitHub Release 文件，但会跳过 COS 上传。

初次将旧版机器切换到本方案时，用完整包进行一次人工部署。后续发版不需要把程序拷进 U 盘；学校电脑只需能访问 COS 下载地址。首次发布前，需要先让新的更新版安装包在可联网处构建并部署到学校电脑。

## 课表判定规则

课表先按普通工作日模板解析，再叠加具体日期规则。具体日期规则优先，单个课程格的特殊时间又优先于该节默认时间。当前课程采用“开始时刻包含、结束时刻不包含”的边界，并明确排除空课。

星期五等特殊作息不需要复制整张课表：双击对应课程格，只覆盖当天这一节的起止时间即可。临时停课、补课或调课则在“特殊日期”中配置。

## 配置版本说明

Schema v3 及更早的稳序桌面配置会在首次加载时迁移到 v4；迁移前的 JSON 会以带版本号和时间戳的文件名保留。这是稳序桌面自身的配置格式升级，不会导入其他产品的设置。
