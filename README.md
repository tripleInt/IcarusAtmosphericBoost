# Icarus Atmospheric Boost / 伊卡洛斯大气弹射

用于《戴森球计划》的 BepInEx 5 + Harmony mod。

## 使用

- 在星球上长按跳跃键约 **0.8 秒**，沿当前星球的径向弹射一次。弹射后由游戏处理惯性与重力，松键后才能再次触发。默认键为空格，跟随游戏的跳跃改键。
- 支持地面、跳跃、下坠、漂浮、飞行。下坠时会直接改为向上速度，并保留原有横向速度。
- 无大气层的星球也可使用。弹射适用于地面和低空，初速度及上升段受高度限制，不会由弹射切换到航行模式。
- 短按不触发 mod，保持原版操作。暂停、输入框、全屏界面、星球/星图视角、截图、失焦、死亡、复活和开场引导时不触发。建造或蓝图模式遵循原版机甲移动限制。
- 每次弹射扣一次能量，默认相当于原版低空悬停 **2 秒**的耗能（根据当地重力和机甲耗能倍率计算）。能量不足时等待回充，仍按住且满足条件时才会触发。

## 安装

1. 游戏或所用 mod 管理器配置中需已安装 **BepInEx 5**（可使用 DSP 的 `xiaoye97-BepInEx` 包）。
2. 从 [GitHub Releases](https://github.com/tripleInt/IcarusAtmosphericBoost/releases) 下载 ZIP。关闭游戏，将包内的 DLL 放到该配置的 `BepInEx/plugins/IcarusAtmosphericBoost/` 中。
3. 用该 BepInEx 配置启动游戏。日志应出现 `Icarus Atmospheric Boost 1.2.0 loaded`。

使用 r2modman 时请复制到实际启动的 profile 的 `BepInEx/plugins`，不一定是游戏安装目录。

首次运行生成 `BepInEx/config/local.dsp.icarusatmosphericboost.cfg`：

| 配置 | 默认 | 含义 |
| --- | --- | --- |
| `Enabled` | `true` | 启用 mod |
| `HoldSeconds` | `0.8` | 触发需要持续按住的真实秒数，允许 0.1–2 |
| `LaunchSpeed` | `300` | 解锁星际航行后的目标初速度，允许 20–1000 m/s，实际速度受起航高度限制 |
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

回归检查包括：短按/长按/松键、计时重置、不同更新间隔、单次触发、下坠中触发、松键惯性、极大切向速度、球面速度约束和起航高度限制。

**尚未进行游戏内运行验证**。进入游戏后建议验证以下场景：

| 场景 | 预期 |
| --- | --- |
| 未解锁星际航行，地面长按空格 | 获得一次向上速度，持续按住不会再次弹射 |
| 未解锁，在上升途中松开空格 | 弹射惯性保持在起航高度以下 |
| 未解锁，在下坠/飞行中长按 | 下坠改为上升，仍受起航高度限制 |
| 在无大气层的星球上长按 | 正常弹射，保持原版行走或飞行状态 |
| 已解锁，接近起航高度时长按 | 弹射减速，弹射本身不触发航行 |
| 暂停/文本输入/全屏界面期间按空格 | 不触发；恢复后重新计算长按时间 |
| 换存档、换星球、死亡后复活 | 不沿用之前的长按状态 |

可能与同样修改机甲速度或飞行状态的 mod 相互影响；兼容性需要在实际组合中验证。

## 实现说明

弹射在原版移动动作更新之后施加一次径向速度。上升期间将下一物理步约束在星球半径加 47 m 的球面内，为原版 49 m 的起航高度条件留出余量。计算使用实际物理步长，并计入 Unity 延后应用的力。

项目结构参考了 [DSP-StationAssistant](https://github.com/cat-yummi-dspmod/DSP-StationAssistant) 的 BepInEx/Harmony 构建方式；此插件独立运行，不依赖 CatAPI 或 StationAssistant。
