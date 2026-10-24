# 开发环境与后续接续

本仓库当前实现《爆弹枪》一代助手。面向玩家的下载、运行和排障说明见 `README.md`；本文用于本地继续开发。

## 目录用途

| 路径 | 用途 | 是否保留 |
| --- | --- | --- |
| `.git/` | 源码历史、GitHub 远程配置 | 保留 |
| `src/` | C# 启动器、面板、数据处理和 Lua 运行时 | 保留 |
| `src/data/levels.json` | 一代关卡与秘密元数据 | 保留，不是临时扫描结果 |
| `tests/` | 自动检查与结算回归样例 | 保留，不是过程日志 |
| `tools/` | 依赖获取、构建、打包脚本 | 保留 |
| `licenses/` | 第三方许可证 | 保留 |
| `dist/BoltgunCompletion/` | 本机可运行版本 | 保留 |
| `dist/BoltgunCompletion/data/settings.json` | 本机游戏目录与面板偏好 | 保留；不提交、不打包 |
| `dist/*.zip`、`dist/SHA256SUMS.txt` | 本地发行文件（如存在） | 保留，避免重新生成后与线上校验值不一致 |
| `work/` | 构建时下载的依赖、解压缓存 | 可删除，下一次通过依赖脚本恢复 |
| `research/`、`__pycache__/`、日志和转储 | 扫描、诊断和临时运行产物 | 不纳入源码；当前已无此类残留 |

当前项目不依赖历史扫描目录或临时文件才能构建。

## 开发入口

- `src/Launcher.cs`：游戏识别、构建校验、独立运行时接入与命令行入口。
- `src/UserInterface.cs`：主窗口、双列悬浮面板与个人设置。
- `src/Telemetry.cs`：状态协议、日志增量读取、时效性和结果展示。
- `src/mod/profile.lua`：一代对象、事件、构建指纹及验证状态。
- `src/mod/adapter.lua`：关卡状态、秘密、敌人及结算适配。
- `src/mod/main.lua`：定时调度与消息发送。
- `tests/test_runtime.py`、`src/SelfTests.cs`：运行时回归与启动器自检。

## 恢复依赖、构建和检查

环境为 Windows x64、CPython 3.12 x64，以及 .NET Framework 4.x 自带的 C# 编译器。当前脚本固定使用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。

在项目根目录执行：

```powershell
python tools/fetch_dependencies.py
python tests/test_runtime.py
python tools/build.py --output dist/Development
dist/Development/BoltgunCompletion-cli.exe --selftest
```

依赖脚本会恢复 `work/`，并校验固定版本 UE4SS 和测试用 Lua 依赖。使用 `dist/Development/` 作为开发输出，避免覆盖当前可用的 `dist/BoltgunCompletion/`。

`tools/package.py` 当前版本号固定为 `0.1.0.1`，会重写根层发行 ZIP 和校验文件。准备下一版时先更新版本与打包目标，再生成新的发行文件；日常开发无需运行打包脚本。

## 清理原则

清理前退出助手；如果已经接入游戏，也先退出游戏，避免删除运行时仍在使用的文件。

只清理项目内确认可再生成的依赖缓存、诊断日志、转储和临时构建输出。保留源码、回归样例、关卡元数据、Git 历史、个人设置和已发布发行文件。不要在游戏安装目录或存档目录执行项目清理。

## 二代开发的接续边界

二代尚未适配。开始前先确认实际安装目录、游戏构建、引擎、加载方式和允许读取的接口，再决定接入技术；一代 UE4SS、对象名、地址和关卡元数据均不能直接视为二代适配依据。

后续为二代建立独立的适配、关卡数据、版本校验和构建输出，保留一代可运行版本。可以评估复用面板和状态协议，但需先核对语义。完美通关继续按同一次最终结算判断：击杀率 100%、秘密全收集。

继续遵循独立插件目录、游戏目录零写入、用户自行操作游戏的约定。只有拿到二代真实数据并完成对应验证后，才声明支持二代。

## 助手与运行时版本

助手文件版本和发行版本为 `0.1.0.1`；当前修复仅涉及 C# 数据接收与显示。Lua 状态消息的 `pluginVersion`、运行时清单版本仍为 `0.1.0`，运行时修订保持不变，以便兼容已接入游戏的运行时。不要仅为助手修复修改协议中的运行时版本或重新生成 Lua 修订。
