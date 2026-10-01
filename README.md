# Windows 更新暂停助手（WinUpdatePauser）

一个轻量的 Windows 小工具：不用手动打开注册表，即可使用新版日历模式或旧版暂停天数模式管理 Windows 更新暂停状态。

- 单文件 `WUPause.exe`，Release 产物约 **386 KB**
- **零运行时依赖**：基于 .NET Framework 4.8（Windows 10 1903+ / Windows 11 系统自带）
- 支持 Windows 10 / Windows 11，浅色 Win11 风格界面，高 DPI 清晰显示
- 新版日期和旧版天数使用独立守护服务，系统重置配置后自动恢复

---

## 原理

Windows 的暂停更新配置保存在以下注册表路径中：

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
```

程序启动时默认进入新版日期配置；用户可根据 Windows 更新界面，通过手动切换入口选择旧版天数配置。

### 新版日历暂停

新版 Windows 使用具体日期表示暂停更新的结束时间。首次使用时，需要先在 **设置 → Windows 更新** 中手动暂停一次，让系统生成对应的注册表值；本工具不会自行创建这三个日期值。

| 项目 | 说明 |
| --- | --- |
| 默认行为 | 程序默认进入此模式；未初始化时先显示引导页 |
| 配置方法 | 手动填写暂停结束的年、月、日、时、分 |
| 注册表类型 | 3 个 `REG_SZ` 值，内容为 ISO 8601 UTC 时间 |
| 初始化要求 | 先在 Windows 设置中暂停一次更新 |
| 写入规则 | 将本地时间转换为 UTC，并让三个结束时间值保持一致 |

新版模式涉及以下三个值：

| 值名 | 含义 |
| --- | --- |
| `PauseFeatureUpdatesEndTime` | 功能更新暂停结束时间 |
| `PauseQualityUpdatesEndTime` | 质量更新暂停结束时间 |
| `PauseUpdatesExpiryTime` | 暂停更新的总到期时间 |

例如，写入注册表的时间格式为 `2077-01-01T00:00:00Z`。界面显示和输入使用本地时间，程序会在写入时自动转换为 UTC。

### 守护服务

守护服务用于处理 Windows 自动重置暂停配置的情况。程序会把同一个 `WUPause.exe` 复制到 `%ProgramFiles%\WUPause\WUPauseGuard.exe`，新版与旧版服务共用该程序文件，但分别保存目标并独立运行。

| 配置方式 | 服务名称 | 目标配置 | 监听并恢复的值 |
| --- | --- | --- | --- |
| 新版日期 | `WUPauseDateGuard` | `HKLM\SOFTWARE\WUPause\Guard\Modern` | 三个日期 `REG_SZ` 值 |
| 旧版天数 | `WUPauseDaysGuard` | `HKLM\SOFTWARE\WUPause\Guard\Legacy` | `FlightSettingsMaxPauseDays` |

两个服务均以 LocalSystem 身份自动延迟启动，使用注册表变更通知，并辅以 60 秒检查。程序不禁用 `wuauserv`、`WaaSMedicSvc`，也不修改注册表权限。每次应用当前模式的暂停配置时，会安装或更新对应服务；“当前状态”只显示当前模式的守护状态，服务运行时才显示“卸载服务”按钮。使用“恢复正常更新”时，程序会停止两套服务，防止暂停值被重新写回。

### 旧版天数暂停

旧版 Windows 更新界面通过 `FlightSettingsMaxPauseDays` 控制可选择的最大暂停天数。本工具将用户填写的天数写入该值，作用与手动执行 `reg add` 命令相同。

| 项目 | 说明 |
| --- | --- |
| 使用方式 | 由用户通过页面入口手动切换 |
| 配置方法 | 手动填写暂停天数，范围为 1～36500 天 |
| 注册表值 | `FlightSettingsMaxPauseDays` |
| 注册表类型 | `REG_DWORD` |
| 初始化要求 | 无需先生成新版的三个日期值；设置键不存在时会按需创建 |

以暂停 36500 天为例，等价命令为：

```cmd
reg add HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings /v FlightSettingsMaxPauseDays /t REG_DWORD /d 36500 /f
```

无论使用哪种模式，程序都只管理上述暂停更新相关值。选择“恢复正常更新”时，程序会删除这些受管理的值。

## 使用方法

经过测试，`新版日历配置`适配所有版本，因此默认进入`新版日历配置`，如无特殊需求，建议使用`新版日历配置`。
当然用户也可在初始页面手动选择切换`旧版配置`。

### 新版日历配置

新版模式需要 Windows 先生成三个日期注册表值。如果此前没有在系统设置中暂停过更新，程序会显示首次使用引导页（一次初始化即可，如果后续恢复更新，则再重新初始化即可）：

1. 打开 Windows 设置
2. 进入 Windows 更新
3. 随便选择一个日期（或者选择暂停1周），暂停更新即可
4. 返回本软件点击「重新检测」

完成初始化后：

1. 在新版日历页面填写暂停结束的年、月、日、时、分。
2. 确认时间在明天至 2199-12-31 范围内。
3. 点击 **[应用暂停日期]**，写入新的暂停结束时间并自动安装、启用 `WUPauseDateGuard` 日期守护服务。
4. 打开或重新打开「设置 → Windows 更新」，查看新的暂停日期。

界面使用本地时间，写入注册表时会自动转换为 UTC。服务运行状态显示在“当前状态”卡片中；如不再需要守护，可点击其中的 **[卸载服务]**。守护服务使用固定安装副本运行，因此移动或删除最初下载的 `WUPause.exe` 不会影响已经启用的守护。

### 旧版天数配置

旧版模式不要求先完成新版初始化，适用于 Windows 10 或仍使用旧版更新界面的 Windows 11：

1. 启动 `WUPause.exe`，并通过管理员权限提示。
2. 如果当前显示新版页面，点击顶部的 **[切换到旧版天数配置]**。
3. 在“旧版暂停天数”中填写 1～36500 之间的整数。
4. 点击 **[应用旧版暂停天数]**，写入 `FlightSettingsMaxPauseDays`，并自动安装、启用 `WUPauseDaysGuard` 天数守护服务。
5. 打开或重新打开「设置 → Windows 更新」，确认暂停设置。

如果新版尚未初始化，也可以直接在首次使用引导页点击 **[切换到旧版天数配置]**。

### 配置方式切换

首次启动默认进入新版日期配置。程序会记住用户最后使用的新版或旧版界面，下次启动时直接打开对应配置；主页面顶部的动态切换按钮仍可随时切换，两套输入内容不会同时显示。

### 恢复正常更新

可以在程序中点击 **[恢复正常更新]**，程序会先停止新版和旧版守护服务，再删除本工具管理的暂停值；也可以分别卸载两套守护服务，再在系统「设置 → Windows 更新」中点击「**继续更新**」。

## 构建方法

### 环境要求

- .NET SDK（任意 5.0 及以上版本均可构建 net48 目标）：

  ```
  winget install Microsoft.DotNet.SDK.10
  ```

  无需安装 .NET Framework 4.8 Developer Pack（项目通过 `Microsoft.NETFramework.ReferenceAssemblies` NuGet 包提供引用程序集）。

### 构建

双击运行 `build.cmd`，或手动执行：

```
dotnet build src\WinUpdatePauser\WinUpdatePauser.csproj -c Release
```

产物：`src\WinUpdatePauser\bin\Release\net48\WUPause.exe`
（同目录的 `.exe.config` 可一并分发，也可省略；`.pdb` 仅用于调试。）程序本身是单个 EXE；`build.cmd` 不会自动同步项目根目录的交付 EXE。

### 替换引导页截图

将新截图保存为 `src\WinUpdatePauser\Assets\guide.png` 后重新构建即可（截图作为资源内嵌进 EXE）。应用图标源文件为 `src\WinUpdatePauser\Assets\app-icon.png`，多尺寸图标由 `tools\make-icon.ps1` 生成。

## 项目结构

```
src/WinUpdatePauser/
├── WinUpdatePauser.csproj      # net48 + WPF，SDK 风格项目文件
├── app.manifest                # requireAdministrator（UAC）+ PerMonitorV2 高 DPI
├── App.xaml / App.xaml.cs      # 启动流程：管理员权限检测 → 创建主窗口
├── MainWindow.xaml / .cs       # 单窗口，按初始化状态切换引导页 / 主页
├── Views/
│   ├── GuideView.xaml / .cs    # 引导页（注册表未初始化时显示）
│   └── MainView.xaml / .cs     # 主页（新版/旧版互斥配置 + 状态与操作）
├── Services/
│   ├── PauseRegistryService.cs # 注册表读写模块
│   ├── UpdateGuardManager.cs   # 守护服务安装、配置、状态与卸载
│   ├── UpdateGuardWindowsService.cs # 新版日期与旧版天数监听服务
│   ├── UserPreferenceService.cs # 记住最后使用的配置界面
│   ├── SystemVersionDetector.cs # 系统版本信息显示
│   └── AdminHelper.cs          # 管理员权限检测 / runas 提权重启
├── Utils/
│   └── Iso8601Time.cs          # 本地时间 ↔ ISO 8601 UTC 转换模块
├── Themes/Styles.xaml          # 浅色 Win11 风格全局样式
└── Assets/                    # 引导截图与应用图标资源
    ├── app-icon.ico           # 应用与窗口图标（内嵌资源）
    ├── app-icon.png           # 图标源素材
    └── guide.png              # 引导页说明截图（内嵌资源）
```

## 常见问题（FAQ）

**Q：双击 exe 弹出 SmartScreen「Windows 已保护你的电脑」？**
A：exe 未做代码签名，首次运行点「更多信息 → 仍要运行」即可。介意的话可自行从源码构建。

**Q：为什么必须管理员权限？**
A：相关注册表值位于 `HKEY_LOCAL_MACHINE`，写入必须提权。程序通过 manifest 声明 `requireAdministrator`，启动时自动弹出 UAC。

**Q：改完后 Windows 设置里没变化？**
A：若设置页当时开着，关掉重新打开「Windows 更新」页面即可看到新日期。

**Q：会不会破坏系统？**
A：工具只修改系统「暂停更新」功能本身使用的新版三个值和旧版天数值，不触碰其他设置。也可以使用程序中的「恢复正常更新」。

**Q：以后 Windows 版本更新导致方法失效怎么办？**
A：本工具利用的是 Windows 现有的暂停机制，微软未来可能调整该机制，届时需要相应更新工具。

## 免责声明

本工具仅修改 Windows 公开注册表中「暂停更新」相关的值，使用即表示你了解暂停系统更新可能带来的安全风险（错过安全补丁）。请自行斟酌暂停时长。

## 新版 / 旧版配置

程序启动后默认进入「新版日期配置」。新版尚未初始化时会显示引导页；完成初始化后直接进入新版页面。用户可以根据 Windows 更新界面，通过页面上的切换入口手动选择旧版；新版和旧版配置面板不会同时显示。

- **新版日历配置**：沿用现有的年、月、日、时、分手动输入，并将同一 ISO 8601 UTC 时间写入 `PauseFeatureUpdatesEndTime`、`PauseQualityUpdatesEndTime`、`PauseUpdatesExpiryTime` 三个 `REG_SZ` 值。首次使用新版前仍需先在 Windows 设置中手动暂停一次，让系统生成这些值。
- **旧版天数配置**：手动填写 1～36500 天，写入 `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings\FlightSettingsMaxPauseDays`（`REG_DWORD`），等价于旧版系统使用的 `reg add` 配置方式。未完成新版初始化时，也可以从引导页切换到旧版。

## 感谢

项目：[WinUpdatePauser](https://github.com/Alwayslikehaimeng/WinUpdatePauser)

## 许可证

本项目采用 [GNU General Public License v3.0](LICENSE)（GPL-3.0）发布。
