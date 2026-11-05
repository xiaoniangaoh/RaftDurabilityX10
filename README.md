# Raft Durability x10

Raft 的 BepInEx 模组：把工具、武器、装备的**耐久消耗降为原来的 1/10**，也就是工具寿命变成 **10 倍**。

不需要新建世界，也不需要迁移存档 —— 模组只改变"每次使用时扣多少耐久"，物品定义、存档数据和网络同步都不动，所以
**已有的存档可以直接用**，卸载模组后物品也会恢复正常消耗。

> 默认倍率 10 倍，可以在配置文件里改成任意倍数（例如 2 倍、50 倍）。

---

## 效果

以普通难度为例（每次使用掉 1 点耐久）：

| | 原版 | 安装模组后（默认 10x） |
|---|---|---|
| 砍树一次 | -1 耐久 | -0.1 耐久（每 10 次才 -1） |
| 一把 100 耐久的斧头 | 100 次 | 1000 次 |
| 抓钩 / 滑索钩 | 每次 -1 | 每 10 次 -1 |

**生效范围**（所有走游戏统一耐久消耗接口的物品）：

- 斧头、镐、铲子、砍刀（Machete）、近战武器
- 鱼竿、船桨、网兜、金属探测器
- 抓钩（Hook）、弓箭、网枪（Net Gun）
- 装备栏里走"装备耐久"接口的物品（例如滑索钩 Zipline 的挂钩）

**不生效**（这些物品用另一套独立的耐久逻辑，不经过被修改的方法）：

- 护甲（盔甲）、氧气瓶、脚蹼、头灯
- 建筑 / 农田 / 烹饪锅等非工具物品

---

## 安装

前置条件：游戏已安装 **BepInEx 5.4.x（x64，Mono 版）**，并且能正常加载其他插件。

1. 下载 `RaftDurabilityX10.dll`（见 [Releases](../../releases)）或自行构建；
2. 放到：

   ```
   <Raft 安装目录>\BepInEx\plugins\RaftDurabilityX10\RaftDurabilityX10.dll
   ```

   （直接放在 `plugins\` 根目录下也可以）
3. 启动游戏。首次启动会生成配置文件：
   `BepInEx\config\raft.durability.x10.cfg`
4. 如果你用 r2modman / Thunderstore Mod Manager，也可以用仓库里的打包 zip（`dist\RaftDurabilityX10-1.0.0.zip`）安装。

## 配置

`BepInEx\config\raft.durability.x10.cfg`：

```ini
[General]

## How many times longer tools last compared to vanilla. 10 = 10x durability
## (one durability point is consumed every 10 uses). 1 = vanilla behaviour (mod effectively disabled).
# Setting type: Single
# Default value: 10
DurabilityMultiplier = 10

## Also slow down durability loss for equipped gear that uses the equipment durability path
## (for example the zipline hook).
# Setting type: Boolean
# Default value: true
AffectEquipment = true
```

- `DurabilityMultiplier`：倍数。想更耐用可以设 50、100；设 1 等于关闭模组（完全原版行为）。
- `AffectEquipment`：是否也影响装备栏里走装备耐久接口的物品（滑索钩）。关掉则只有手持工具受保护。

修改后重启游戏生效。

## 验证是否生效

1. 查看 BepInEx 控制台或 `BepInEx\LogOutput.log`，应能看到：

   ```
   [Info   :   BepInEx] Loading [Raft Durability x10 1.0.0]
   [Info   : Raft Durability x10] Raft Durability x10 v1.0.0 loaded. Tools now last 10x longer (durability loss divided by 10).
   [Info   : Raft Durability x10] Harmony patch state: hotSlot=1, equipment=1 (expected: hotSlot=1, equipment=1).
   ```

   如果出现红字 `A durability patch did not attach - the mod is NOT active.`，说明游戏版本更新后目标方法变了，需要更新模组。

2. 游戏内：拿斧头砍树 10 次，耐久条只应下降"1 次"的量；第一次实际扣耐久时，日志里会有一条：

   ```
   First scaled durability loss applied: item=Axe, consumed=1 durability point(s), multiplier=10x.
   ```

3. 或者直接打开物品栏看耐久百分比：原版砍 1 次掉 1%，装模组后砍 10 次才掉 1%。

## 多人游戏说明

- 模组的计算在**本地**完成，主机 / 客机都能用；
- 但是耐久值本身是同步的物品状态。如果同一局里有人没装模组，他的工具仍然按原版速度损耗（其他人不受影响）；
- 想获得一致体验，建议同局所有人都装。

## 已知限制

- 未消耗完的"零头"耐久（例如 0.9 点）只保存在内存里，保存并重载后最多损失不到 1 点耐久，可忽略；
- 护甲、氧气瓶等不走该接口的物品不受影响（见上文"不生效"）；
- 游戏更新后若官方改动了 `PlayerInventory` 的耐久方法，模组会记录警告并自动退回原版行为（不会崩游戏），需要更新模组；
- 与其他"修改物品最大耐久 `maxUses`"的模组叠加时，两个效果会相乘（例如 10x 寿命 × 2 倍最大耐久 = 20 倍）。

## 卸载

删除 `BepInEx\plugins\RaftDurabilityX10\`（或对应的 dll）即可，存档不受影响。

## 自行构建

不需要安装 .NET SDK：`build.ps1` 会使用 Windows 自带的 .NET Framework 编译器（`csc.exe`），并自动在 Steam 库中查找 Raft 目录。

```powershell
# 自动检测 Steam 版 Raft，只构建
.\build.ps1

# 指定游戏目录，并直接复制到 BepInEx\plugins
.\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Raft" -Install
```

构建产物：`dist\RaftDurabilityX10.dll`。需要 .NET Framework 4.x（Windows 10/11 默认自带）。

## 工作原理

游戏里所有工具的耐久消耗最终都经过 `PlayerInventory` 的两个方法：

- `bool RemoveDurabillityFromHotSlot(int durabilityStacksToRemove)`（手持工具 / 武器）
- `bool RemoveDurabillityFromEquipment(EquipSlotType equipmentType, int durabilityStacksToRemove)`（装备）

模组用 Harmony 前缀（prefix）接管这两个方法：按原版公式算出这次应该损耗多少点耐久，然后**除以配置的倍率**，
把不足 1 点的零头累计到该物品实例上（`ConditionalWeakTable<ItemInstance, StrongBox<float>>`），凑满 1 点才真正调用游戏私有的
`RemoveStacksFromSlot(slot, loss)` 去扣耐久 —— 这样破损音效、堆叠消耗等原版行为完全保留。

相比"把物品最大耐久 ×10"的做法，改消耗量不需要迁移存档，也不会破坏物品定义和多人同步。

## License

MIT，见 [LICENSE](LICENSE)。

---

## English

**Raft Durability x10** — a BepInEx 5 plugin for Raft that divides tool/weapon durability loss by 10 (configurable),
making tools last 10x longer without touching item definitions or save data.

- Covers every item that goes through `PlayerInventory.RemoveDurabillityFromHotSlot` /
  `RemoveDurabillityFromEquipment`: axes, pickaxes, shovels, machetes, melee weapons, fishing rods, paddles, nets,
  metal detectors, hooks, bows, net guns, and the zipline hook.
- Does **not** cover armour, air bottles, flippers or headlights (they use a separate durability path).
- Install: drop `RaftDurabilityX10.dll` into `BepInEx\plugins\RaftDurabilityX10\`.
- Config: `BepInEx\config\raft.durability.x10.cfg` → `DurabilityMultiplier` (default 10, 1 = disabled),
  `AffectEquipment` (default true).
- Requires BepInEx 5.4.x (x64, Mono). Build with `.\build.ps1` — it uses the C# compiler bundled with Windows, so no .NET SDK is needed.

Existing saves keep working; removing the plugin restores vanilla behaviour.
