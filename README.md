# Icarus Atmospheric Boost / 伊卡洛斯大气弹射

用于《戴森球计划》的 BepInEx 5 + Harmony mod。

## 使用

- 长按跳跃键至少 **0.8 秒**，松开时弹射一次。默认使用空格，跟随游戏的跳跃改键。
- 成功弹射后有 **3 秒冷却**。冷却期间松键会取消本次请求，冷却结束后可再次长按并松键。
- 默认在距行星基准表面 **300 米**内可以弹射。有无大气层均适用。下坠时会将径向速度转为向上，保留横向速度。
- **驱动引擎 2 解锁前**：目标初速度为 80 m/s，实际速度随剩余空间调整。弹射从 47 米以下触发，上升段保持在原版起航高度以下。
- **驱动引擎 2 解锁后**：默认向上初速度为 300 m/s，上升速度随原版重力变化。实际升至距行星基准表面 50 米且仍在向外飞时，切入原版航行。已经在近地航行时也可以触发弹射。
- 蓄力时暂停跳跃键带来的原版持续爬升。松键成功弹射时扣一次能量，默认等于原版低空悬停 **2 秒**的耗能，最多扣核心容量的 5%。扣费会保留至少 5% 的核心能量，余额不足时取消弹射。
- 成功弹射时，伊卡洛斯背部引擎会喷发一次原版火焰粒子。
- 暂停、全屏界面、文本输入、失焦、死亡和开场引导期间不触发。建造或蓝图模式遵循原版机甲移动限制。

## 安装

1. 游戏或所用 mod 管理器配置中需已安装 **BepInEx 5**（可使用 DSP 的 `xiaoye97-BepInEx` 包）。
2. 从 [GitHub Releases](https://github.com/tripleInt/IcarusAtmosphericBoost/releases) 下载 ZIP。关闭游戏，将包内的 DLL 放到该配置的 `BepInEx/plugins/IcarusAtmosphericBoost/` 中。
3. 用该 BepInEx 配置启动游戏。日志应出现 `Icarus Atmospheric Boost 2.1.2 loaded`。

使用 r2modman 时请复制到实际启动的 profile 的 `BepInEx/plugins`，不一定是游戏安装目录。

首次运行生成 `BepInEx/config/local.dsp.icarusatmosphericboost.cfg`：

| 配置 | 默认 | 含义 |
| --- | --- | --- |
| `Enabled` | `true` | 启用 mod |
| `HoldSeconds` | `0.8` | 松键触发所需的最短按住时间（真实秒），允许 0.1–2 |
| `LaunchSpeed` | `300` | 驱动引擎 2 解锁后的径向初速度，允许 20–1000 m/s |
| `LaunchRange` | `300` | 最大触发高度，按行星基准表面计，允许 50–400 米 |
| `LockedLaunchSpeed` | `80` | 解锁之前的目标初速度，允许 10–300 m/s，实际速度受起航高度限制 |
| `BoostPowerMultiplier` | `2` | 单次弹射相当于几秒低空悬停耗能，允许 0–20，设为 0 关闭额外耗能 |

旧配置会保留已保存的长按时间。如仍为 0.35 秒，可手动改为 0.8 秒。修改配置后重启游戏。卸载时移除 DLL；mod 不添加自定义存档字段。

## 编译

构建 SDK 为 **.NET 10**；插件运行目标为游戏 Unity/Mono 支持的 **.NET Framework 4.7.2**。

在仓库根目录运行。将变量设为自己的游戏安装目录和含有 `core` 子目录的 BepInEx 目录。

```powershell
.\build.ps1 -GameDirectory $GameDirectory -BepInExDirectory $BepInExDirectory
```

构建需要 .NET Framework 4.7.2 引用程序集。DLL 和 Thunderstore ZIP 输出到仓库的 `dist/` 目录。

## 验证范围

已通过编译、输入计时与球面速度约束的回归检查，以及 Harmony/BepInEx 静态兼容性检查。

```powershell
dotnet run --project .\tests\RegressionChecks.csproj --configuration Release
dotnet build .\tests\CompatibilityCheck\CompatibilityCheck.csproj --configuration Release
.\tests\CompatibilityCheck\bin\Release\net472\CompatibilityCheck.exe $GameDirectory $BepInExDirectory
```

回归检查覆盖长按松键触发、重复采样、3 秒冷却、输入取消，以及不同物理步长下的球面速度约束。

尚未进行游戏内运行验证。可在有大气和无大气的星球分别验证地面起步、下坠反向弹射、冷却，以及驱动引擎 2 解锁前后的航行表现。

## 实现说明

弹射在松键后施加一次背离行星中心的速度。驱动引擎 2 解锁前，上升段限制在行星半径加 47 米的球面内。解锁后，离地阶段保留每个物理步受重力更新后的径向速度。达到起航高度后，交接时把当前速度同步到原版航行。航行的近星修正会逐渐恢复。

项目结构参考了 [DSP-StationAssistant](https://github.com/cat-yummi-dspmod/DSP-StationAssistant) 的 BepInEx/Harmony 构建方式；此插件独立运行，不依赖 CatAPI 或 StationAssistant。
