# Windows 更新暂停助手（WinUpdatePauser）

一个轻量的 Windows 小工具：不用手动打开注册表，即可使用新版日历模式或旧版暂停天数模式管理 Windows 更新暂停状态。

- 单文件 `WinUpdatePauser.exe`，Release 产物约 **350 KB**
- **零运行时依赖**：基于 .NET Framework 4.8（Windows 10 1903+ / Windows 11 系统自带）
- 支持 Windows 10 / Windows 11，浅色 Win11 风格界面，高 DPI 清晰显示

---

## 原理

Windows 的暂停更新配置保存在以下注册表路径中：

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
```

本工具根据当前`Windows Build`版本推荐配置方式，用户也可以手动切换。新版模式修改暂停结束时间，旧版模式修改允许暂停的天数；两种模式在写入注册表前都会自动备份当前配置。

### 新版日历暂停

新版 Windows 使用具体日期表示暂停更新的结束时间。首次使用时，需要先在 **设置 → Windows 更新** 中手动暂停一次，让系统生成对应的注册表值；本工具不会自行创建这三个日期值。

| 项目 | 说明 |
| --- | --- |
| 推荐系统 | Windows 11 24H2（Build 26100）及更高版本 |
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

### 旧版天数暂停

旧版 Windows 更新界面通过 `FlightSettingsMaxPauseDays` 控制可选择的最大暂停天数。本工具将用户填写的天数写入该值，作用与手动执行 `reg add` 命令相同。

| 项目 | 说明 |
| --- | --- |
| 推荐系统 | Windows 10 或使用旧版更新界面的 Windows 11 |
| 配置方法 | 手动填写暂停天数，范围为 1～36500 天 |
| 注册表值 | `FlightSettingsMaxPauseDays` |
| 注册表类型 | `REG_DWORD` |
| 初始化要求 | 无需先生成新版的三个日期值；设置键不存在时会按需创建 |

以暂停 36500 天为例，等价命令为：

```cmd
reg add HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings /v FlightSettingsMaxPauseDays /t REG_DWORD /d 36500 /f
```

无论使用哪种模式，程序都只管理上述暂停更新相关值。选择“恢复正常更新”时，程序会先备份当前配置，再删除这些受管理的值。

## 使用方法

### 新版日历配置

新版模式需要 Windows 先生成三个日期注册表值。如果此前没有在系统设置中暂停过更新，程序会显示首次使用引导页（一次初始化即可，如果后续恢复更新，则再重新初始化即可）：

1. 打开 Windows 设置
2. 进入 Windows 更新
3. 点击「暂停更新」
4. 随便选择一个日期
5. 返回本软件点击「重新检测」

完成初始化后：

1. 在新版日历页面填写暂停结束的年、月、日、时、分。
2. 确认时间在明天至 2099-12-31 范围内。
3. 点击 **[应用暂停日期]**。程序会先备份当前配置，再写入新的暂停结束时间。
4. 打开或重新打开「设置 → Windows 更新」，查看新的暂停日期。

界面使用本地时间，写入注册表时会自动转换为 UTC。

### 旧版天数配置

旧版模式不要求先完成新版初始化，适用于 Windows 10 或仍使用旧版更新界面的 Windows 11：

1. 启动 `WinUpdatePauser.exe`，并通过管理员权限提示。
2. 如果当前显示新版页面，点击顶部的 **[切换到旧版天数配置]**。
3. 在“旧版暂停天数”中填写 1～36500 之间的整数。
4. 点击 **[应用旧版暂停天数]**。程序会先备份当前配置，再写入 `FlightSettingsMaxPauseDays`。
5. 打开或重新打开「设置 → Windows 更新」，确认暂停设置。

如果新版尚未初始化，也可以直接在首次使用引导页点击 **[切换到旧版天数配置]**。

### 配置方式切换

程序会根据 Windows Build 推荐配置方式，但不会限制用户选择。主页面顶部只有一个动态切换按钮，可随时在新版日历配置和旧版天数配置之间切换；两套输入内容不会同时显示。

### 恢复正常更新

可以在程序中点击 **[恢复正常更新]**，程序会先备份当前配置，再删除本工具管理的暂停值；也可以在系统「设置 → Windows 更新」中点击「**继续更新**」。

### 备份管理

- 默认备份目录为程序 EXE 同级的相对目录 `backup`，可在 **[备份管理]** 中选择自定义目录或恢复默认路径。
- 每次应用新版、应用旧版、恢复正常更新或恢复备份前，都会自动保存相关注册表值。
- 备份管理支持恢复、重命名和删除；恢复、删除会显示确认提示，重命名直接执行。所有操作都会限制在当前备份目录内。

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

产物：`src\WinUpdatePauser\bin\Release\net48\WinUpdatePauser.exe`
（同目录的 `.exe.config` 可一并分发，也可省略；`.pdb` 仅用于调试。）程序本身是单个 EXE；`build.cmd` 不会自动覆盖项目根目录的旧 EXE。

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
│   ├── MainView.xaml / .cs     # 主页（新版/旧版互斥配置 + 状态与操作）
│   └── BackupWindow.xaml / .cs # 备份列表、恢复、重命名、删除与路径设置
├── Services/
│   ├── PauseRegistryService.cs # 注册表操作与备份模块
│   ├── SystemVersionDetector.cs # Windows Build 检测与新版/旧版推荐
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

程序启动后会读取当前 Windows Build：Build 26100（Windows 11 24H2）及更高版本默认推荐「新版日历配置」，其它版本默认推荐「旧版天数配置」。推荐方式只影响初始选择，页面上的切换入口可以随时切换；新版和旧版配置面板不会同时显示。

- **新版日历配置**：沿用现有的年、月、日、时、分手动输入，并将同一 ISO 8601 UTC 时间写入 `PauseFeatureUpdatesEndTime`、`PauseQualityUpdatesEndTime`、`PauseUpdatesExpiryTime` 三个 `REG_SZ` 值。首次使用新版前仍需先在 Windows 设置中手动暂停一次，让系统生成这些值。
- **旧版天数配置**：手动填写 1～36500 天，写入 `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings\FlightSettingsMaxPauseDays`（`REG_DWORD`），等价于旧版系统使用的 `reg add` 配置方式。未完成新版初始化时，也可以从引导页切换到旧版。

## 注册表备份与恢复

每次写入新版、写入旧版、恢复正常更新或恢复某个备份前，程序都会先保存相关注册表键和值。备份记录值是否存在、原始类型和原始数据，并同时生成可阅读的 JSON 与 `.reg` 文件。

- 默认目录是程序 EXE 同级的相对目录 `backup`；在「备份管理」中可以选择自定义目录或恢复默认路径。
- 文件名采用「操作前的配置_时间」格式，例如 `应用新版配置前的配置_20260908-120000-123.json`。
- 「备份管理」支持恢复、重命名和删除。恢复、删除会二次确认，重命名直接执行；服务端会校验目标必须位于当前备份目录内。恢复操作本身也会先生成一份“恢复备份前的配置”。
- 「恢复正常更新」会先备份，再删除本工具管理的新版三个值和旧版天数值。若需恢复以前的状态，可在备份管理中选择对应 JSON 备份。
