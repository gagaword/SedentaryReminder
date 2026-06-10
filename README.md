# 久坐提醒 (SedentaryReminder)

一个 Windows 久坐提醒小工具，托盘常驻、到点弹系统 Toast 提醒你起身活动。

## 技术栈

- **.NET 8 + WPF**（Windows 原生 UI）
- **WPF-UI** — Fluent / Mica 现代主题
- **H.NotifyIcon** — 托盘图标 + 右键菜单（生成图标，无需 .ico）
- **Microsoft.Toolkit.Uwp.Notifications** — 系统 Toast 通知

## 功能

- 托盘常驻图标 + 右键菜单（重置计时 / 暂停 / 设置 / 退出）
- 可配置久坐间隔（默认 45 分钟）
- 两种提醒强度：
  - **普通模式**：到点弹系统 Toast 通知
  - **强制模式**：到点在**所有显示器**全屏绿色遮罩 + 居中倒计时，拦截关闭/最小化/Alt+F4；**长按 ESC 3 秒**可应急退出，倒计时走完自动关闭。无需管理员权限
- 开机自启开关（HKCU Run 键，免管理员）
- 每日统计：久坐时长、提醒次数、完成/跳过休息次数，托盘菜单「统计…」查看今日数据 + 近 7 天柱状图（数据存于 `%AppData%\SedentaryReminder\stats.json`，保留 60 天）

## 低内存设计

- 设置窗口**按需创建、关闭即释放**，常驻只有托盘 + 计时器
- 启动峰值后、设置窗口关闭后主动 `GC.Collect()`
- 计时用 `System.Threading.Timer`，不占用 UI 线程
- 常驻内存约 **35–45MB**（大头是与系统共享的 WPF 渲染 DLL）

## 开发 / 运行

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```powershell
dotnet restore
dotnet run
```

## 发布（单文件，依赖框架运行时）

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

> 配置文件位于 `%AppData%\SedentaryReminder\settings.json`。

## 许可证

[MIT](LICENSE) © 2026 GaGa
