# 存档守护 (SaveGuard)

[中文](#中文) | [English](#english)

---

## 中文

> 为 YAPYAP 提供温和的失败处理——撤离失败物品回收，三夜任务失败不删档

---

### 功能

**撤离失败物品回收**

- 保留原版"先从玩家身上掉落到世界"的行为
- 默认将所有符合条件的掉落物都进行回收，可以在回收处拿到
- 回收率可在游戏内设置页选择：`0%` `25%` `50%` `75%` `100%`
- 尊重原版物品黑名单、物品状态和网络生成规则

**任务失败保档**

三夜任务没能打满分数时：

> **保留** 存档槽 · 金币 · 库存 · 装备 · Hub 物品 · 地下室钥匙 · Grimoire 进度 · 任务层数和目标 · 总回合数

> **重置** Session 分数 → `0`，从第 `1` 夜重新挑战同一层任务

- 成功达成任务时保持原版流程
- 保留完整 Game Over 动画、界面和 Host 重建，仅跳过失败流程中的存档槽删除
- 不会触发 `FrogDataLib` 等模组的删档逻辑

---

### 游戏内设置

进入 **Settings** → **存档守护 / SaveGuard** 页签：

| 设置项 | 类型 | 默认值 | 可选值 |
|---|:---:|---:|---|
| 任务失败存档 | 开关 | 开 | |
| 物品回收率 | 下拉 | `100%` | `0%` `25%` `50%` `75%` `100%` |

> 界面使用游戏原生控件风格，配置保存在 BepInEx 配置文件中，**不覆盖原版设置**

---

### 安装

**推荐**：通过 Thunderstore Mod Manager / r2modman 安装

**手动安装**：

```
1. 安装 BepInEx 5
2. 将 SaveGuard.dll 放入 BepInEx/plugins/SaveGuard/
3. 启动游戏
```

配置文件路径：`BepInEx/config/com.saveguard.yapyap.cfg`

---

### 联机说明

> **Host 必须安装**，客户端可以不装

---

### 配置文件

```ini
[Quota Failure]
ProtectSave = true

[Recovery]
RecoveryPercent = 100

[Safety]
CreateEmergencyBackup = true
MaxEmergencyBackups = 5

[Compatibility]
EnforceBuildGuard = true

[General]
DebugLog = false
```

---

### 已知限制

- 黑名单物品不会进入回收列表
- 回收时非空闲状态的物品可能无法搬回大厅
- 大厅物品受原版持久化规则和数量上限约束
- 游戏 UI 大更新后设置页可能需要适配

---

### 致谢

`BepInEx` · `Harmony` · `Thunderstore` · `YapYap Graphics Enhancer`

---

## English

> A gentle safety net for YAPYAP — recover dropped items after a failed extraction, keep your progress when you miss quota

---

### Features

**Failed Extraction Item Recovery**

- Preserves the vanilla "items drop to the ground first" flow
- All eligible dropped items are recovered by default — grab them at the recovery point
- Recovery rate adjustable in-game: `0%` `25%` `50%` `75%` `100%`
- Respects vanilla item blacklists, prop states, and network spawning rules

**Quota Failure Protection**

If you fall short of the quota after three nights:

> **Preserved** Save slot · Gold · Inventory · Equipment · Hub props · Basement key · Grimoire progress · Quota tier & goal · Total rounds played

> **Reset** Session score → `0`, restart from Night `1` on the same quota tier

- Meeting the quota works exactly as in vanilla
- The full Game Over sequence plays normally — only the save-slot deletion is skipped
- Won't interfere with `FrogDataLib` or other mods that hook `DeleteSlot`

---

### In-Game Settings

Open **Settings** → **存档守护 / SaveGuard** tab:

| Setting | Type | Default | Options |
|---|:---:|---:|---|
| Keep save after mission failure | Toggle | On | |
| Item recovery rate | Dropdown | `100%` | `0%` `25%` `50%` `75%` `100%` |

> Uses native-style UI controls. Stored in the BepInEx config — **does not touch vanilla PlayerPrefs**

---

### Installation

**Recommended**: Install via Thunderstore Mod Manager / r2modman

**Manual**:

```
1. Install BepInEx 5
2. Place SaveGuard.dll in BepInEx/plugins/SaveGuard/
3. Launch the game
```

Config file: `BepInEx/config/com.saveguard.yapyap.cfg`

---

### Multiplayer

> **Host must install**; clients are optional

---

### Configuration

```ini
[Quota Failure]
ProtectSave = true

[Recovery]
RecoveryPercent = 100

[Safety]
CreateEmergencyBackup = true
MaxEmergencyBackups = 5

[Compatibility]
EnforceBuildGuard = true

[General]
DebugLog = false
```

---

### Known Limitations

- Blacklisted items are never eligible for recovery
- Props not in an idle state may fail to transfer back to the Lobby
- Lobby props are still subject to vanilla persistence rules and prop limits
- The in-game settings tab may need updates after major game UI changes

---

### Credits

`BepInEx` · `Harmony` · `Thunderstore` · `YapYap Graphics Enhancer`
