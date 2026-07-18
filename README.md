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

下的三个 REG_SZ 值（ISO 8601 UTC 格式，三值必须保持完全一致）：

| 值名 | 含义 |
|---|---|
| `PauseFeatureUpdatesEndTime` | 功能更新暂停结束时间 |
| `PauseQualityUpdatesEndTime` | 质量更新暂停结束时间 |
| `PauseUpdatesExpiryTime` | 暂停总到期时间 |

格式示例：`2077-01-01T00:00:00Z`

本工具做的事就是：把这三个值统一改成你选择的时间。修改后 Windows 设置中的暂停日期会立即按新时间显示。

> 注意：工具**绝不会凭空创建**这些注册表项——它们必须先由系统生成（见下方首次使用）。

## 使用方法

### 首次使用（初始化，只需一次）

如果从未在系统设置里暂停过更新，程序会显示引导页：

1. 打开 Windows 设置
2. 进入 Windows 更新
3. 点击「暂停更新」
4. 随便选择一个日期
5. 返回本软件点击「重新检测」

引导页提供 **[打开 Windows 更新设置]** 按钮可直接跳转系统设置页。

### 日常使用

1. 双击 `WinUpdatePauser.exe`（会弹出 UAC 提示，修改 HKLM 必须管理员权限）
2. 主界面显示当前暂停结束时间
3. 选择新的 年/月/日/时/分（可选范围：明天 ~ 2099-12-31）
4. 点击 **[应用暂停日期]**
5. 完成。可打开「设置 → Windows 更新」查看效果

时间按**本地时间**选择，与 Windows 设置页显示一致；写入注册表时自动转换为 UTC。

### 恢复正常更新

在系统「设置 → Windows 更新」中点击「**继续更新**」即可（系统会自行清除上述注册表值）。

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
│   └── MainView.xaml / .cs     # 主页（状态显示 + 日期选择 + 一键应用）
├── Services/
│   ├── PauseRegistryService.cs # 注册表操作模块（读取/判定/三值一致写入）
│   └── AdminHelper.cs          # 管理员权限检测 / runas 提权重启
├── Utils/
│   └── Iso8601Time.cs          # 本地时间 ↔ ISO 8601 UTC 转换模块
├── Themes/Styles.xaml          # 浅色 Win11 风格全局样式
└── Assets/guide.png            # 引导页说明截图（内嵌资源）
```

## 常见问题（FAQ）

**Q：双击 exe 弹出 SmartScreen「Windows 已保护你的电脑」？**
A：exe 未做代码签名，首次运行点「更多信息 → 仍要运行」即可。介意的话可自行从源码构建。

**Q：为什么必须管理员权限？**
A：三个注册表值位于 `HKEY_LOCAL_MACHINE`，写入必须提权。程序通过 manifest 声明 `requireAdministrator`，启动时自动弹出 UAC。

**Q：改完后 Windows 设置里没变化？**
A：若设置页当时开着，关掉重新打开「Windows 更新」页面即可看到新日期。

**Q：会不会破坏系统？**
A：工具只修改系统「暂停更新」功能本身使用的三个值，不触碰其他任何设置。想恢复时在系统设置点「继续更新」即可回到默认状态。

**Q：以后 Windows 版本更新导致方法失效怎么办？**
A：本工具利用的是 Windows 现有的暂停机制，微软未来可能调整该机制，届时需要相应更新工具。

## 免责声明

本工具仅修改 Windows 公开注册表中「暂停更新」相关的三个值，使用即表示你了解暂停系统更新可能带来的安全风险（错过安全补丁）。请自行斟酌暂停时长。
