# C盘清理助手

[English](README.md) · [完整使用说明](docs/使用说明.md)

一个 Windows 桌面程序：先看清 C 盘到底被什么占着，再按类别挑出可清理项，**只删除你确认过的内容**。

C# WinForms 编写，用 Windows 自带的 .NET Framework 编译器构建，单文件约 130 KB，
免安装、无运行库依赖、无联网、无遥测。

![清理清单界面](docs/screenshots/clean-list.png)

> ### ⚠️ 先读这一段
> 本程序会**永久删除文件**，不进回收站，程序本身也无法恢复。
>
> - 第一次请先勾选确认框里的「**仅演练**」，完整看一遍会删什么。
> - 需要管理员权限，因为系统缓存位于受保护目录；启动时会自动请求 UAC。
> - 软件按「现状」提供，不附带任何担保，删什么由你决定。
> - 源码刻意写得短小直白，建议在授予管理员权限前先读一遍。

---

## 它能做什么

- **真实占用分析**：遍历整个 C 盘，按目录层级 1–4 聚合体积，列出最大的文件，
  并对硬链接做说明。
- **76 个精选清理目标**，分 6 类：系统垃圾、开发与运行缓存、系统组件（DISM / 还原点）、
  GPU 与游戏着色器缓存、第三方应用缓存、用户大文件（仅统计）。
- **个人数据只统计不删除**：微信 / QQ / 钉钉、下载、桌面、文档、视频、游戏库、Docker
  镜像都不在删除清单里。
- **白名单校验**：每条待删路径都要通过校验，任何一条不合法就中止整次清理；
  不跟随 junction / 符号链接。
- **演练模式**：完整走一遍流程，但一个字节都不删。
- **完整日志**：每一项、每个删不掉的文件都记录在
  `文档\C盘清理助手\C-Clean-Log-*.log`。
- **联网极少**：每天最多一次 HTTPS 请求，只用来查 GitHub 上有没有新版本；无遥测、无广告、
  不会自动下载安装。可在「设置」里关闭，或加 `--no-update-check`。

## 环境要求

| | |
|---|---|
| 系统 | Windows 10 1809+ / Windows 11（64 位） |
| 运行库 | .NET Framework 4.8（Win10 1903+ 与 Win11 自带） |
| 权限 | 管理员（程序通过 UAC 自动提权） |

## 快速开始

### 下载

在 [Releases](../../releases) 页面下载：

| 文件 | 说明 |
|------|------|
| `c-drive-cleaner-<version>-setup.exe` | **安装包**：装到当前用户目录，**安装不需要管理员权限**；带开始菜单、可选桌面快捷方式，以及「设置 → 应用」里的标准卸载项 |
| `c-drive-cleaner-<version>-portable.zip` | **便携版**：解压即用，含程序、说明和许可证 |
| `c-drive-cleaner-<version>-disks.zip` | **磁盘扩展包（可选）**：单独扫描 D/E 盘、U 盘、移动硬盘，不随安装包附带 |
| `SHA256SUMS.txt` | 所有文件的 SHA256 校验和 |

安装包未签名，首次运行可能出现 SmartScreen「已保护你的电脑」，点「**更多信息 → 仍要运行**」即可。

### 从源码构建

不需要 Visual Studio，也不需要 .NET SDK —— 直接用系统自带的 `csc.exe`：

```powershell
git clone https://github.com/Misaka-11736/c-drive-cleaner.git
cd c-drive-cleaner
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物为 `.\C盘清理助手.exe`（约 130 KB），可用 `-Out <路径>` 指定输出位置。

要一次生成全部发布文件（主程序 + 安装包 + 便携 ZIP + 校验和）：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 2.0.1
```

### 使用

1. 双击安装后的快捷方式（或便携版 EXE），在 UAC 弹窗点「是」。
2. 点「**重新扫描**」（或按 F5），扫描只读，SSD 上约 90 秒。
3. 到「**可清理**」页核对勾选项（默认只勾推荐项），点「**开始清理**」。
4. 确认框里输入 `DELETE` 才会解锁按钮；也可以勾「**仅演练**」先看一遍。
5. 完成后自动跳到「**日志**」页，显示释放空间与未能删除的条目。

安装过程本身不需要管理员权限，只有执行清理时才会提权。

## 工作原理

1. **扫描**：解析 76 个目标（含 `C:\Users\*\AppData\Local\Temp` 这类通配符），
   只遍历 C 盘一次，把文件体积归属到命中的目标；同时聚合 1–4 层目录体积，
   并记录 ≥ 50 MB 的文件排行。
2. **复核**：界面按类别展示合计、风险等级、文件数与说明；个人数据单独列出且不可勾选。
3. **删除**：确认后逐项删除（`contents` 模式保留目录本身），清除只读属性，
   跳过被占用文件并记录；DISM、还原点、回收站调用系统原生接口。

## 安全设计

- 所有待删路径必须满足：位于 `C:\` 下、不含 `..`、不含通配符、不在保护名单、
  不是用户目录根、不是重解析点。
- 任意一条校验失败，**整次清理在删除第一个文件之前中止**。
- 只处理 C 盘，不碰其他分区。
- 扫描时跳过 junction / 符号链接；删除时只删链接本身。
- 个人数据目录在设计上就是「仅统计」，没有隐藏开关可以把它们删掉。

## 更新提醒

启动后最多每 24 小时查询一次 `api.github.com` 的 `releases/latest`。如果标签比当前版本新，
窗口顶部会出现一条提示：

> 发现新版本 2.2.0（当前 2.1.0）· 查看 · 忽略此版本

- **查看**：用浏览器打开 Release 页面，程序**不会**自己下载或运行安装包。
- **忽略此版本**：记住这个版本，不再反复提示。
- 可在侧栏「设置」里关闭自动检查，或启动时加 `--no-update-check`。
- 如果 `api.github.com` 被拦截，会自动改为跟随 `github.com/.../releases/latest` 的跳转，
  从最终 URL 的 `/tag/vX.Y.Z` 读取版本。
- 网络失败时完全静默，不影响任何功能。

## 磁盘扩展包（可选）

主程序只分析 C 盘。**磁盘扩展包**是单独下载的可选工具，用来扫描其他磁盘：

- 内置数据分区（D:/E:）、USB 移动硬盘、U 盘、SD 卡；
- **快速扫描**（体积、回收站、Steam 缓存、下载目录）或**深度扫描**
  （大文件、重复文件、超过 2 年未修改的大文件）；
- **全盘查重**：扫描 `C:\Users` 与 D/E/F，找出 ≥1 MB 的字节级重复，
  同盘与跨盘都报，硬链接单独识别，只报告并可导出 CSV；
- 只有安全项能删——各盘回收站、Steam `shadercache` / `downloading` / `temp`，
  游戏、下载、媒体、文档一律只报告；
- 自动识别备份盘（`WindowsImageBackup`、`FileHistory`、卷标含 Backup/Passport/备份），
  只允许清空回收站（需输入 `DELETE F`），其余一律只读；网络盘不列出；
- 删除需输入 `DELETE D`（对应盘符），输错不执行；
- 不下载任何东西，也没有任何网络请求。

下载 `c-drive-cleaner-<版本>-disks.zip`，把 `磁盘扩展包.exe` 解压到主程序目录
（或 `%LOCALAPPDATA%\C盘清理助手\dlc\`），点侧栏「磁盘扩展包」即可。

## 命令行参数

GUI 也接受少量参数，主要用于自动化与截图：

```text
C盘清理助手.exe --scan                       启动后立即扫描
C盘清理助手.exe --plan="plan.json"           导入已保存的清理计划
C盘清理助手.exe --plan="plan.json" --dry-run 只演练，不删除
C盘清理助手.exe --page=3                     启动后停在指定页（0–5）
C盘清理助手.exe --no-elevate                 不请求管理员（只读模式）
C盘清理助手.exe --no-update-check            完全不联网检查更新
```

清理计划是普通 JSON（`schema: c-cleaner-plan/1`），由「导出报告」生成。

## 目录结构

```text
build.ps1                  一条命令构建（csc，无需 SDK）
build-installer.ps1        编译安装程序（内嵌主程序）
build-release.ps1          主程序 + 安装包 + 便携 ZIP + 校验和
setup/                     安装程序源码（快捷方式、卸载项）
app.manifest               DPI 感知、asInvoker（程序自行提权）
app.ico                    由 tools/make-icon.ps1 生成
src/                       全部 C# 源码与报告模板
docs/                      使用说明、设计决策、截图
tools/                     图标生成器与沙箱删除测试
.github/workflows/         Windows CI：编译 + 安全测试
```

## 测试

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1
```

测试会在 `%TEMP%` 里调用真实的 `Cleaner`，验证：

- 内容删除（含只读文件）成功且目录本身保留；
- 指向 `C:\Windows\System32` 的计划被拒绝；
- 演练模式不删除任何文件。

CI 每次推送都会跑同一套测试。

## 隐私

无遥测、无统计、无账号。唯一的联网行为是检查更新：

- 每 24 小时最多一次 HTTPS `GET` 到
  `api.github.com/repos/Misaka-11736/c-drive-cleaner/releases/latest`，
  只带 `User-Agent` 与 `Accept` 头；API 被拦截时改用 `github.com/.../releases/latest` 跳转；
- 可在「设置」关闭，或启动加 `--no-update-check`；
- 检查结果（上次时间、最新版本）只存在本机
  `%LOCALAPPDATA%\C盘清理助手\update-state.json`。

报告与日志只写到本机 `文档\C盘清理助手\`，内容仅包含你机器上的路径。

## 已知限制

- 更新只做提醒，不会自动下载或安装。
- 未做代码签名，首次运行可能触发 SmartScreen 提示。
- 硬链接（WinSxS、DriverStore）按路径重复计数，目录体积可能高于实际占用，界面已注明。
- DISM 组件清理要 10–20 分钟，且清理后无法卸载旧更新，因此**默认不勾选**；想多释放 1–4 GB 再手动勾上。
- 没有隔离区，删除即永久（请先用演练模式）。
- 与 Piriform CCleaner 等产品无关。

## 参与贡献

欢迎提 Issue 和 PR，请先看 [CONTRIBUTING.md](CONTRIBUTING.md)。
请不要削弱白名单校验，也不要新增「删除个人数据」的目标。

## 许可证

[MIT](LICENSE) © 2026 Misaka-11736
