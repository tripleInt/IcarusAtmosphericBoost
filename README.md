# Icarus Atmospheric Boost / 伊卡洛斯大气弹射

用于《戴森球计划》的 BepInEx 5 + Harmony mod。全部开发文件位于 `DSPDev/IcarusAtmosphericBoost`，不修改游戏源码。

## 使用

- 在大气层内长按跳跃键约 **0.8 秒**，沿当前星球的径向弹射一次。弹射后由游戏处理惯性与重力，松键后才能再次触发。默认键为空格，跟随游戏的跳跃改键。
- 支持地面、跳跃、下坠、漂浮、飞行，以及大气层内的航行状态。下坠时会直接改为向上速度，并保留原有横向速度。
- **解锁星际航行之前**：最大向上速度默认 **80 m/s**，接近大气层顶时自动减速，在边界内留安全距离。持续按住及松键后的惯性都受保护，不会借助本 mod 越过大气层。
- **解锁星际航行之后**：向上速度默认 **300 m/s**，接近起航高度时可接入原版航行状态。
- 短按不触发 mod，保持原版操作。暂停、输入框、全屏界面、星球/星图视角、截图、失焦、死亡、复活和开场引导时不触发。建造或蓝图模式遵循原版机甲移动限制。
- 每次弹射扣一次能量，默认相当于原版低空悬停 **2 秒**的耗能（根据当地重力和机甲耗能倍率计算）。能量不足时等待回充，仍按住且满足条件时才会触发。

大气边界读取 `PlanetData.atmosphereHeight`，星际航行能力读取原版的 `mecha.thrusterLevel >= 2`，因此无需硬编码科技 ID。没有可读取大气层的星球不启用弹射。

## 安装

1. 游戏或所用 mod 管理器配置中需已安装 **BepInEx 5**（可使用 DSP 的 `xiaoye97-BepInEx` 包）。
2. 关闭游戏，将 `dist/IcarusAtmosphericBoost.dll` 复制到该配置的 `BepInEx/plugins/IcarusAtmosphericBoost/` 中。
3. 用该 BepInEx 配置启动游戏。日志应出现 `Icarus Atmospheric Boost 1.1.0 loaded`。

使用 r2modman 时请复制到实际启动的 profile 的 `BepInEx/plugins`，不一定是游戏安装目录。

首次运行生成 `BepInEx/config/local.dsp.icarusatmosphericboost.cfg`：

| 配置 | 默认 | 含义 |
| --- | --- | --- |
| `Enabled` | `true` | 启用 mod |
| `HoldSeconds` | `0.8` | 触发需要持续按住的真实秒数，允许 0.1–2 |
| `LaunchSpeed` | `300` | 解锁星际航行后的向上速度，允许 20–1000 m/s |
| `LockedLaunchSpeed` | `80` | 解锁之前的速度上限，允许 10–300 m/s；始终受边界保护 |
| `BoostPowerMultiplier` | `2` | 单次弹射相当于几秒低空悬停耗能，允许 0–20，设为 0 关闭额外耗能 |

旧配置会保留已保存的长按时间。如仍为 0.35 秒，可手动改为 0.8 秒。修改配置后重启游戏。卸载时移除 DLL；mod 不添加自定义存档字段。

## 编译

构建 SDK 为 **.NET 10**；插件运行目标为游戏 Unity/Mono 支持的 **.NET Framework 4.7.2**。

在项目目录运行：

```powershell
.\build.ps1
```

脚本默认读取本机 Steam 安装中的游戏程序集，以及 `r2modmanPlus-local/DysonSphereProgram/profiles/ue-test/BepInEx/core` 中的 BepInEx/Harmony。不同位置可覆盖：

```powershell
.\build.ps1 -GameDirectory 'D:\Games\Dyson Sphere Program' -BepInExDirectory 'D:\Mods\MyProfile\BepInEx'
```

需要本地 .NET Framework 4.7.2 引用程序集（本机已存在）。不重编译反编译出的 `Assembly-CSharp`，也不将游戏或 BepInEx 依赖打包到产物中。构建后 DLL、调试符号和本说明位于 `dist/`，不会自动部署到游戏。构建缓存与临时目录位于 `DSPDev` 下。

## 验证范围

已完成本机游戏程序集编译、输入计时和球面速度约束的独立回归检查，以及 39 项 Harmony/BepInEx 静态兼容性检查。测试不需要启动游戏：

```powershell
dotnet run --project .\tests\RegressionChecks.csproj --configuration Release
dotnet build .\tests\CompatibilityCheck\CompatibilityCheck.csproj --configuration Release
.\tests\CompatibilityCheck\bin\Release\net472\CompatibilityCheck.exe
```

回归检查包括：短按/长按/松键、计时重置、不同更新间隔、单次触发、下坠中触发、松键惯性、极大切向速度、球面边界和解锁后的离星速度。

**尚未进行游戏内运行验证**。进入游戏后建议验证以下场景：

| 场景 | 预期 |
| --- | --- |
| 未解锁星际航行，地面长按空格 | 获得一次向上速度，持续按住不会再次弹射 |
| 未解锁，在上升途中松开空格 | 不因惯性越过大气边界 |
| 未解锁，在下坠/飞行中长按 | 下坠改为上升，仍受边界限制 |
| 已解锁，在起航高度附近长按 | 可向上弹射并进入航行，无需原版横向起航条件 |
| 已解锁，在大气内航行时长按 | 径向上升；离开大气层后交回原版控制 |
| 暂停/文本输入/全屏界面期间按空格 | 不触发；恢复后重新计算长按时间 |
| 换存档、换星球、死亡后复活 | 不沿用之前的长按状态 |

可能与同样修改机甲速度或飞行状态的 mod 相互影响；兼容性需要在实际组合中验证。

## 实现说明

在移动控制器处理完原版步行/飞行动作后修改径向速度，避开原版飞行竖直速度上限与悬停推力。航行状态单独同步宇宙速度并计入星球运动。未解锁时先随剩余高度减速，再将下一物理步的径向/切向位移约束在安全球面内；位置同步前还有越界修正。速度预测包含 Unity 延后应用的力，使用实际物理步长处理低 UPS。

项目结构参考了 [DSP-StationAssistant](https://github.com/cat-yummi-dspmod/DSP-StationAssistant) 的 BepInEx/Harmony 构建方式；此插件独立运行，不依赖 CatAPI 或 StationAssistant。
