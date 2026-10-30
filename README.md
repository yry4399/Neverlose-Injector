# Neverlose Injector

拥有 Neverlose 风格界面与动画的 DLL 注入器加载器。支持将任意 DLL 加密打包进单个 exe，运行时自动启动目标程序并完成注入。

> 本项目为 Windows 平台 .NET 8 / WPF 技术研究项目，演示了 WPF 动画界面、Win32 进程注入、AES 加密资源、.NET 混淆打包等技术。

---

## 功能特性

- **Neverlose 风格 UI**：雨滴玻璃拟态背景、卡片展开启动动画、注入进度动画
- **双主题**：雨滴玻璃 / 经典深色，支持自定义背景图、背景透明度、卡片透明度
- **个性化**：可更换挂件、自定义头像（头像文件名自动作为昵称）
- **一键打包**：双击 `set-dll.bat` 选择 DLL，自动完成 **AES-256 加密 → ConfuserEx 混淆 → 单文件打包**
- **注入流程**：启动目标程序 → 等待进程 → 内存解密负载 → 调用注入器 → 临时文件即用即删
- **反分析**：负载以 AES-256-CBC 加密形式嵌入（密钥每次打包随机生成），主程序经 ConfuserEx 混淆（改名 / 字符串加密 / 控制流 / 反 ILDASM）

## 技术栈

| 组件 | 技术 |
|---|---|
| 前端界面 | C# WPF (.NET 8)，纯 XAML 手绘 UI，无第三方 UI 库 |
| 注入器 | C++ Win32（位于 `injtrol/`，CreateRemoteThread + LoadLibraryW / Manual Map） |
| 负载保护 | AES-256-CBC（.NET `System.Security.Cryptography`），IV 随文件头 |
| 代码混淆 | ConfuserEx 1.6（mkaring fork，内置于 `tools/ConfuserEx/`） |
| 打包 | `dotnet publish` 单文件自包含 + LZ4 压缩，约 77 MB |

## 项目结构

```
├── App.xaml(.cs)          # 程序入口、全局样式
├── MainWindow.xaml(.cs)   # 主界面：游戏卡片、注入动画、主题逻辑
├── GameWindow.xaml(.cs)   # 游戏选择窗口：选择游戏 exe、注入信息
├── ProfileWindow.xaml(.cs)# 设置窗口：主题、背景图、头像、透明度
├── PickerWindow.xaml(.cs) # 通用图片选择器（挂件/背景/头像）
├── AboutWindow.xaml(.cs)  # 关于页
├── Injector.cs            # 注入器封装：解密负载、调用注入、用后删除
├── Crypto.cs              # AES-256 解密
├── CryptoKeys.g.cs        # 【自动生成】每次打包由脚本重新生成，不入库
├── set-dll.ps1 / .bat     # 一键打包脚本（选 DLL → 加密 → 混淆 → 打包）
├── Loader.crproj          # ConfuserEx 混淆配置
├── injtrol/               # C++ 注入器（源码 + 编译产物）
└── tools/ConfuserEx/      # 混淆器（免安装）
```

## 构建说明

**环境要求**：Windows 10/11 x64 + [.NET 8 SDK](https://dotnet.microsoft.com/download)

```powershell
git clone https://github.com/yry4399/Neverlose-Injector.git
cd Neverlose-Injector

# 方式一：一键流程（推荐）——选择 DLL、加密、混淆、打包一次完成
.\set-dll.bat

# 方式二：命令行参数
.\set-dll.ps1 -DllPath "C:\path\to\your.dll" -Build

# 跳过混淆（调试用）
.\set-dll.ps1 -DllPath "C:\path\to\your.dll" -Build -SkipObfuscate
```

产物输出：`bin\Release\net8.0-windows\win-x64\publish\Loader.exe`

> 注意：仓库不包含 `CryptoKeys.g.cs`（密钥文件）与 `cheat/`（加密负载），
> 首次构建必须先运行一次 `set-dll.ps1` 生成密钥，否则无法编译。

## 使用说明

1. **以管理员身份运行** Loader.exe（打开目标进程需要权限）
2. 点击游戏卡片（CS:GO / CS2），选择游戏 exe 路径
3. 点击 ▷ 启动：程序自动启动游戏、等待进程出现并完成注入
4. 注入成功显示 "loaded!" 后自动退出；失败会显示注入器返回的错误日志

## 工作原理

**打包期**：`set-dll.ps1` 生成随机 AES-256 密钥写入 `CryptoKeys.g.cs`，将注入器与所选 DLL 加密为 `cheat\*.dat` 嵌入程序集，然后 ConfuserEx 混淆主程序，最后打成单文件 exe。

**运行期**：点击启动后，主程序在内存中解密负载，写入 `%TEMP%` 下随机文件名（12 位随机字符），调用注入器对目标进程执行 `CreateRemoteThread + LoadLibraryW`，注入完成后立即删除临时文件。

## 免责声明与法律声明

- 本项目**仅供学习与技术研究之用**，用于演示 WPF 开发、Win32 API、加解密与 .NET 保护技术，不提供任何作弊功能本体。
- 本项目中不包含任何游戏作弊代码；`injtrol/` 为通用 DLL 注入器，与游戏无任何耦合。
- 使用本工具向任何第三方程序（包括但不限于受反作弊系统保护的游戏）注入代码，可能违反该程序的**最终用户许可协议与服务条款**，由此产生的一切后果（包括但不限于账号封禁）由**使用者自行承担**，作者概不负责。
- 本项目与 Valve Corporation、完美世界及任何游戏厂商**无任何关联**；CS:GO、CS2 等名称与商标归其各自所有者所有。
- 使用者须遵守所在国家/地区的法律法规。**不得**将本项目用于任何非法用途，**不得**用于破坏计算机信息系统或侵犯他人合法权益。
- 本项目按"现状"提供，不附带任何明示或默示的担保。源代码以 [AGPL-3.0](LICENSE) 协议开源，任何二次分发须遵守该协议。

## License

[GNU AGPL-3.0](LICENSE)
