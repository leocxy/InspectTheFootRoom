# InspectTheFootRoom

检查战斗场景（Raid）地面上的东西，如果发现门卡、钥匙、皇冠、坦克电池、炮弹等贵重物品，就在屏幕上弹出提示文字，同时把它们的位置标到小地图上，方便通过小地图去找。

支持追踪 **地面散落物** 与 **箱子 / 柜子里** 的战利品，并可按 F9 打开配置窗口自由勾选想要搜寻的物品，选择会自动保存。

> 第一次搞这些东西，都是根据各个大神的 Mod 摸石头过河的，请多包涵。

Github: https://github.com/leocxy/InspectTheFootRoom

---

## 功能特性

- **地面扫描**：进入战斗场景后自动扫描全部掉落物，过滤出 `FromInfoKey == "Ground"` 的地面物品。
- **箱子扫描**：通过反射读取 `Loot Box Inventories` 下的战利品库存，标出箱子 / 柜子里包含的贵重物品。
- **弹字提示**：发现物品时用 `PopText` 在屏幕中央弹出提示，格式为「地上有:xxx 坐标(x,y,z)」或「箱子里有:xxx」。
  - 找到皇冠时额外弹出「找到你了!」。
  - 一件都没找到时连续弹出「什么都木有!」系列。
- **小地图标记**：在目标地图打开小地图（MiniMap）时，会用绿色圆圈（带阴影）+ 物品真实图标标记出每个目标位置（`SimplePointOfInterest`）。
- **物品配置窗口（F9）**：原生 IMGUI 窗口，可读中文（自动切换 CJK 字体、字体放大 2 倍）。
  - 搜索框按名称 / ID 过滤。
  - 勾选要追踪的物品，**最多 20 种**。选择为空 = 扫描地面上全部物品；非空 = 只扫描选中的物品。
  - 翻页浏览（每页 250 条），已选中的排在前面。
  - 「全选匹配」「清空选择」「保存配置」按钮。
- **选择持久化**：通过 `PlayerPrefs` 保存，下次启动自动加载；首次运行默认勾选 10 件高价值物品。

### 默认追踪的物品（首次运行）

| 物品 | ID | 物品 | ID |
| --- | --- | --- | --- |
| 皇冠 | 1254 | 黄色门卡 | 801 |
| X 钥匙 | 827 | 红色门卡 | 802 |
| O 钥匙 | 828 | 绿色门卡 | 803 |
| 坦克电池 | 1430 | 蓝色门卡 | 804 |
| 炮弹 | 1500 | 黑色门卡 | 886 |
| | | 紫色门卡 | 887 |

### 已支持的战斗场景（白名单）

仅以下场景会执行扫描 / 标记（其余菜单、Loading、过场等场景自动跳过）：

- 农场区域：`Level_Farm_01` / `Level_Farm_Main` / `Level_Farm_JLab_Facility`
- 地面零区：`Level_GroundZero_Main` / `Level_GroundZero_1` / `Level_GroundZero_Cave`
- 隐藏仓库：`Level_HiddenWarehouse_Main` / `Level_HiddenWarehouse` / `Level_HiddenWarehouse_CellarUnderGround`
- 实验室：`Level_JLab_Main` / `Level_JLab_1` / `Level_JLab_2`
- 沙漠：`Level_Desert_Main` / `Level_Desert` / `Level_Desert_Boss`
- 风暴区域：`Level_StormZone_Main` / `_1` / `B0`~`B4`
- 雪地军事基地：`Level_SnowMilitaryBase_Main` / `Level_SnowMilitaryBase` / `Level_SnowFactory` / `Level_SnowMilitaryBase_ColdStorage_Main` / `Level_SnowMilitaryBase_ColdStorage`
- 僵尸模式：`Level_Zombie_Main` / `Level_Zombie_1`
- 风暴过去：`Level_StormPast_Main` / `Level_StormPast_1`
- 生存挑战：`Level_SurivalChallenge_Main` / `Level_SurivalChallenge`

> 场景名取自 `ModBehaviour.cs` 中的 `RAID_MAPS`。游戏更新导致地图改名时，需要同步更新该白名单。

---

## 快捷键

| 按键 | 作用 |
| --- | --- |
| `F9` | 打开 / 关闭物品选择配置窗口（需先进入一场对局再按，菜单阶段数据库未就绪会提示重试） |

---

## 编译与安装

本项目基于 .NET SDK（`netstandard2.1`），依赖游戏本体的托管程序集。

### 1. 配置游戏路径

编辑 `DockovClass/InspectTheFootRoom.csproj`，把 `<DuckovPath>` 改成你的游戏安装目录（包含 `Duckov.exe` 的目录）：

```xml
<!-- Windows -->
<DuckovPath>E:\SteamLibrary\steamapps\common\Escape from Duckov</DuckovPath>
<!-- Mac -->
<DuckovPath>/Users/Somebody/Library/Application Support/Steam/steamapps/common/Escape from Duckov</DuckovPath>
```

脚本会自动拼接 `\Duckov_Data\Managed\`（Windows）或 `/Duckov.app/Contents/Resources/Data/Managed/`（Mac）来引用 `TeamSoda.*`、`ItemStatsSystem.dll`、`Unity*` 等程序集。

### 2. 编译

```bash
cd DockovClass
dotnet build -c Release
```

产物位于 `DockovClass/bin/Release/netstandard2.1/InspectTheFootRoom.dll`。

### 3. 装入游戏

将生成的 `InspectTheFootRoom.dll` 放到游戏 Mod 加载目录（配合 Duckov Mod 框架 / BepInEx 等，按对应框架要求放置），启动游戏进入对局即可。

---

## 目录结构

```
InspectTheFootRoom/
├── DockovClass/
│   ├── ModBehaviour.cs          # Mod 主逻辑：扫描、弹字、小地图标记、F9 配置窗口
│   └── InspectTheFootRoom.csproj # 项目文件（含游戏路径配置）
├── UnityExpoltor.cs             # 辅助脚本：运行时 dump 全量物品数据库（调试用，非 Mod 本体）
├── DockovClass.slnx
└── README.md
```

---

## Changes Logs

2026-08-14
- 新增箱子 / 柜子战利品扫描（反射读取 `Loot Box Inventories`）。
- 新增 F9 物品选择配置窗口：搜索、勾选、翻页，最多 20 种，支持中文显示。
- 物品选择通过 PlayerPrefs 持久化，首次运行默认勾选 10 件高价值物品。
- 小地图标记改用物品真实图标 + 绿色圆圈。
- 引入战斗场景白名单 `RAID_MAPS`，只在目标地图扫描，避免在菜单 / 过场报错。

2026-08-10
- 修复 ItemAgent 是 null 的问题。
- 现在是检查所有战斗场景的地面上面的贵重物品。

2025-12-02
- 在农场镇地上发现特定的贵重物品的时候，会在地图上标识出来。

2025-12-01
- 找到贵重物品的时候，弹出的提示会附上坐标
