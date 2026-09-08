# Windows 更新暂停助手（WinUpdatePauser）

一个极简的 Windows 小工具：不用打开注册表，选个日期、点一下，即可把 Windows 更新的**暂停结束日期**改到任意未来时间（如 2077 年）。

- 单文件 `WinUpdatePauser.exe`，仅约 **156 KB**
- **零运行时依赖**：基于 .NET Framework 4.8（Windows 10 1903+ / Windows 11 系统自带）
- 支持 Windows 10 / Windows 11，浅色 Win11 风格界面，高 DPI 清晰显示

---

## 原理

Windows 11 新版「暂停更新」改成了日期选择器，无法再无限延长。但当用户在
**设置 → Windows 更新 → 暂停更新** 中选择过一次日期后，系统会在注册表生成：

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
```

下的三个 REG_SZ 值（ISO 8601 UTC 格式，三值必须保持完全一致）；旧版模式另外使用同一键下的 `FlightSettingsMaxPauseDays` REG_DWORD：

| 值名 | 含义 |
|---|---|
| `PauseFeatureUpdatesEndTime` | 功能更新暂停结束时间 |
| `PauseQualityUpdatesEndTime` | 质量更新暂停结束时间 |
| `PauseUpdatesExpiryTime` | 暂停总到期时间 |

格式示例：`2077-01-01T00:00:00Z`

本工具做的事就是：把新版三个值统一改成你选择的时间，或按旧版模式写入暂停天数。修改后 Windows 设置中的暂停状态会按新配置显示。

> 注意：新版不会凭空创建三个日期 REG_SZ 值，必须先由 Windows 设置完成一次初始化；旧版按 `reg add` 方式写入 `FlightSettingsMaxPauseDays`，设置键不存在时会按需创建。

## 使用方法

### 首次使用（新版初始化，只需一次）

如果选择新版且从未在系统设置里暂停过更新，程序会显示引导页：

1. 打开 Windows 设置
2. 进入 Windows 更新
3. 点击「暂停更新」
4. 随便选择一个日期
5. 返回本软件点击「重新检测」

引导页提供 **[打开 Windows 更新设置]** 按钮可直接跳转系统设置页，也可以直接点击 **[切换到旧版天数配置]** 使用旧版方式。

### 日常使用

1. 双击 `WinUpdatePauser.exe`（会弹出 UAC 提示，修改 HKLM 必须管理员权限）。
2. 程序按 Windows Build 给出新版/旧版推荐；顶部只有一个动态按钮，可切换到另一种配置方式。
3. 使用新版时填写年/月/日/时/分（可选范围：明天 ~ 2099-12-31），点击 **[应用暂停日期]**。
4. 使用旧版时手动填写暂停天数（1～36500），点击 **[应用旧版暂停天数]**。
5. 完成后可打开「设置 → Windows 更新」查看效果。

时间按**本地时间**选择，与 Windows 设置页显示一致；写入注册表时自动转换为 UTC。

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
（同目录的 `.exe.config` 可一并分发，也可省略；`.pdb` 仅用于调试。）

### 替换引导页截图

将新截图保存为 `src\WinUpdatePauser\Assets\guide.png` 后重新构建即可（截图作为资源内嵌进 exe）。

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
