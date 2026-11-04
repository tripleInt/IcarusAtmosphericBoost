# Icarus Atmospheric Boost / 伊卡洛斯弹射起步

用于《戴森球计划》的 BepInEx 5 + Harmony mod。按住跳跃键积累层数，松开后瞬间获得对应的向上初速度。

## 使用

- 默认每按住跳跃键 **0.75 秒**获得一层，每层增加 **75 m/s** 的弹射目标初速度。跟随游戏的跳跃改键，默认空格。
- 最大蓄力层数等于**已经完成研究的驱动引擎等级**。驱动引擎 1 最多一层，驱动引擎 2 最多两层，后续升级依次增加。尚未解锁驱动引擎 1 时保留原版跳跃。
- 松键立即按已完成层数弹射。未满一层时松键不会弹射，未完成的下一层不计入速度。蓄满后继续按住不会自动发射。
- 按下任一移动方向键会清空本次蓄力。默认 WASD，也支持游戏改键。检测每个按键本身，即使抵住障碍物或同时按下相反方向也会打断。下坠、滑行和惯性位移不会打断。
- 打断后必须松开跳跃键，再重新按住蓄力。
- 每完成一层，伊卡洛斯头顶弹出层数和速度提示，例如 `蓄力 2/3 · 150 m/s`。满层时提示松键。已有完整层数时打断会提示 `蓄力已打断`，不足一层时静默取消。
- 成功弹射后有 **3 秒冷却**。冷却期间不积累层数，持续按住时从冷却结束后开始计时。

| 已完成层数 | 蓄力时间（默认） | 目标向上初速度 |
| --- | --- | --- |
| 1 | 0.75 秒 | 75 m/s |
| 2 | 1.50 秒 | 150 m/s |
| 3 | 2.25 秒 | 225 m/s |
| 4 | 3.00 秒 | 300 m/s |
| 5 | 3.75 秒 | 375 m/s |
| 6 | 4.50 秒 | 450 m/s |

## 飞行与耗能

默认在距行星基准表面 **300 米**内可以开始蓄力和释放，有无大气层均适用。弹射方向为背离行星中心，适用于地面、低空飞行、下坠和近地航行。保留横向速度，解锁航行后已有更快的向外速度也会保留。

松键时一次性赋予初速度，同时喷发一次原版背部引擎火焰粒子。之后速度受原版重力影响。玩家通过提前松键选择较低层数，也可以蓄满后高速起飞。

- **驱动引擎 2 解锁前**，弹射从 47 米以下触发，实际初速度和上升过程受剩余高度约束，保持在原版起航高度以下。提示中的速度是层数对应的目标值。
- **驱动引擎 2 解锁后**，实际升至距行星基准表面 50 米且仍向外运动时，保留当前速度交接到航行模式。起飞后没有 mod 额外施加的高度刹车。
- 蓄力期间屏蔽跳跃键带来的原版持续爬升，原版低空悬停仍正常耗能。
- 成功释放时只扣一次额外能量，默认等于原版低空悬停 **2 秒**的耗能。每次最多扣核心容量的 5%，并保留至少 5% 的核心能量。层数提示、取消蓄力和保持满层均不额外扣费。
- 能量不足时提示并取消本次弹射。暂停、全屏界面、文本输入、失焦、死亡、开场引导或离开触发范围会取消蓄力，恢复后需重新按键。

## 安装与更新

1. 游戏或 mod 管理器配置需安装 **BepInEx 5**，可使用 DSP 的 `xiaoye97-BepInEx` 包。
2. 从 [GitHub Releases](https://github.com/tripleInt/IcarusAtmosphericBoost/releases) 下载 ZIP。关闭游戏，将 DLL 放到所用配置的 `BepInEx/plugins/IcarusAtmosphericBoost/` 中，替换旧版 DLL。
3. 用该配置启动游戏。日志应出现 `Icarus Atmospheric Boost 2.2.0 loaded`。

使用 r2modman 时，文件需放入实际启动的 profile。卸载时移除 DLL，mod 不添加自定义存档字段。

首次运行生成 `BepInEx/config/local.dsp.icarusatmosphericboost.cfg`。

| 配置 | 默认 | 含义 |
| --- | --- | --- |
| `Enabled` | `true` | 启用 mod |
| `ChargeSecondsPerLayer` | `0.75` | 每层蓄力时间，真实秒，允许 0.25–3 |
| `SpeedPerLayer` | `75` | 每层对应的向上初速度，允许 10–150 m/s |
| `ShowTips` | `true` | 显示头顶蓄力、打断和能量不足提示 |
| `LaunchRange` | `300` | 可蓄力和释放的最大高度，允许 50–400 米 |
| `BoostPowerMultiplier` | `2` | 单次弹射相当于几秒低空悬停耗能，允许 0–20，0 关闭额外扣费 |

2.2.0 使用新的分层配置。旧版 `HoldSeconds`、`LaunchSpeed` 和 `LockedLaunchSpeed` 不再参与计算，无需手动删除旧配置即可获得新的默认蓄力规则。其他已有配置继续保留，修改后重启游戏。

## 编译与检查

构建 SDK 为 **.NET 10**，插件运行目标为游戏 Unity/Mono 支持的 **.NET Framework 4.7.2**。需要相应引用程序集。

在仓库根目录运行，将变量设为自己的游戏安装目录和含有 `core` 子目录的 BepInEx 目录。

```powershell
.\build.ps1 -GameDirectory $GameDirectory -BepInExDirectory $BepInExDirectory

dotnet run --project .\tests\RegressionChecks.csproj --configuration Release
dotnet run --project .\tests\RuntimeChecks\RuntimeChecks.csproj --configuration Release
dotnet build .\tests\CompatibilityCheck\CompatibilityCheck.csproj --configuration Release
.\tests\CompatibilityCheck\bin\Release\net472\CompatibilityCheck.exe $GameDirectory $BepInExDirectory
```

DLL 和 Thunderstore ZIP 输出到仓库的 `dist/` 目录。ZIP 根目录包含 `manifest.json`、`README.md`、`CHANGELOG.md`、`icon.png` 和许可证。

自动检查覆盖分层计时、重复采样、方向打断、科技上限、3 秒冷却、单次扣费、松键立即赋速、重力滑行、航行交接以及不同物理步长下的高度保护。运行逻辑测试使用简化的游戏宿主替身，不运行 Unity。

**尚未完成本版本的游戏内实测。** 头顶提示排布、粒子效果、实际碰撞和相机过渡仍需在游戏中验证。

## 参考

构建方式参考 [DSP-StationAssistant](https://github.com/cat-yummi-dspmod/DSP-StationAssistant)。头顶浮动提示的使用方式参考 [DSP-Deproliferation 的 Feedback.cs](https://github.com/cat-yummi-dspmod/DSP-Deproliferation/blob/main/Feedback.cs)，使用游戏原生 `UIRealtimeTip` 并支持中英文。

本插件独立运行，不依赖上述 mod 或 CatAPI。MIT 许可证见 `LICENSE`。
