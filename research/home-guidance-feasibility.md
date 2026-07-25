# YAPYAP 回家指引 Mod 可行性研究

> 整理自 2026-07-25 对话。目标：分析 R.E.P.O. 的回家指引功能，评估在 YAPYAP 中复刻的可行性。

---

## 1. 反编译环境

| 游戏 | DLL 路径 | 大小 | 反编译输出 |
|---|---|---|---|
| R.E.P.O. | `steamapps/common/REPO/REPO_Data/Managed/Assembly-CSharp.dll` | 2.9 MB | `decompiled/REPO/FullAssembly.cs`（6.7M 字符，224k 行） |
| YAPYAP | `steamapps/common/YAPYAP/yapyap_Data/Managed/Assembly-CSharp.dll` | 3.2 MB | `decompiled/YAPYAP/FullAssembly.cs`（7.3M 字符，216k 行） |

反编译工具：`tools/decompiler/Decompile/`（ICSharpCode.Decompiler 8.2 NuGet 包）

---

## 2. R.E.P.O. 的回家指引系统

### 2.1 ArrowUI（屏幕方向箭头）

- **文件位置**：`decompiled/REPO/FullAssembly.cs:194814`
- **状态**：有定义，但几乎未被调用。仅存在于教程/UI 演示中
- **功能**：屏幕空间方向箭头，支持 `ArrowShow`（屏幕坐标）和 `ArrowShowWorldPos`（世界坐标）
- **结论**：不是 R.E.P.O. 实际使用的导航方式

### 2.2 MapBacktrack（地图面包屑 — 核心导航）

- **文件位置**：`decompiled/REPO/FullAssembly.cs:144399`
- **状态**：实际在用的导航系统
- **工作原理**：
  ```
  1. 找到 Truck 房间的世界坐标
  2. 如果所有提取完成 → target = truckDestination
     否则如果当前提取点存在 → target = extractionPointCurrent
  3. NavMesh.CalculatePath(玩家位置, target, path)
  4. 在地图上按间距投射 path.corners → 动画面包屑点
  ```

### 2.3 ExtractionPoint（提取点管理）

- **文件位置**：`decompiled/REPO/FullAssembly.cs:81533`
- **状态**：完整的提取点系统，包含按钮交互、吸气动画、警报音效等
- **关键字段**：`extractionArea`, `safetySpawn`, `haulGoal`

### 2.4 RoundDirector（回合管理）

- **文件位置**：`decompiled/REPO/FullAssembly.cs:187403`
- **关键字段**：
  - `extractionPointCurrent` — 当前活跃的提取点
  - `allExtractionPointsCompleted` — 所有提取点是否完成
  - `extractionPointList` — 提取点列表

---

## 3. YAPYAP 现有基础设施分析

### 3.1 实际在用的后端（可依赖）

#### DungeonPathing（静态工具类）

- **文件位置**：`decompiled/YAPYAP/FullAssembly.cs:56364`
- **公开方法**：
  - `GetDetailedGuidancePath(generator, startPos, startRoom, targetRoom, maxPoints, verticalOffset)` → `List<Vector3>`
  - `GetPathToTarget(generator, startRoom, endRoom)` → `List<RoomData>`
  - `CanPathBetweenRooms(generator, roomA, roomB)` → `bool`
  - `GetRandomReachableRoom(...)` / `GetAllReachableRooms(...)`
- **内部流程**：
  ```
  GetDetailedGuidancePath:
    1. GetPathToTarget → BFS 房间图寻路
    2. FindConnectingDoors → 相邻房间的门对
    3. GetDetailedPathBetweenDoors → NavMesh.CalculatePath(doorA, doorB)
    4. SimplifyPath → 均匀采样
  → 返回 List<Vector3> 世界坐标路径点
  ```

#### DungeonGenerator（地图生成器）

- `FindRoomsOfType("ExtractionRoom")` → `List<RoomData>`
- `GetRoomAtPosition(worldPos)` → `RoomData`
- `HasPath(startNode, endNode)` → BFS 设置 pathfindingWeight → `bool`
- `DungeonGraph` → 房间图（GraphNode 列表）

#### TeleportExtractionCircle（提取圈）

- **文件位置**：`decompiled/YAPYAP/FullAssembly.cs:80315`
- **事件**：`OnSpawned`, `OnExtractionStarted`, `OnExtractionEnded`
- **关键字段**：`circleRadius`, `extractLoopVfx`

#### DungeonMinimap（小地图）

- **文件位置**：`decompiled/YAPYAP/FullAssembly.cs:55232`
- **功能**：3D 俯视角渲染小地图，可缩放、旋转

### 3.2 存在于代码但未激活（死代码）

| 类 | 位置 | 说明 |
|---|---|---|
| `UICompass` | `176000` | 方向罗盘，指向提取圈。代码完整但未被场景引用 |
| `GuidanceSpell` | `130344` | 主动施法的引导线法术，用 LineRenderer 画路径。代码完整但未绑定到 Grimoire |
| `UICompassReferences` | `176324` | UICompass 的视觉引用组件 |

---

## 4. ExtractionTrail Mod 反编译分析

### 4.1 基本信息

- **Thunderstore**：https://thunderstore.io/c/repo/p/DSH/ExtractionTrail/
- **作者**：DSH
- **版本**：1.5.1
- **开源**：❌ 无公开仓库（API 返回 `website_url: ""`，GitHub/GitLab 搜索均为 0 结果）
- **分类**：`AI Generated`
- **大小**：130KB
- **依赖**：仅 `BepInEx-BepInExPack-5.4.2100`

### 4.2 源码结构（726 行，反编译自 DLL）

#### Plugin.cs

```csharp
[BepInPlugin("com.repo.extractiontrail", "Extraction Trail", "1.5.1")]
public class Plugin : BaseUnityPlugin
{
    // 配置项
    ConfigEntry<string> ColorExtraction;  // 提取点颜色 #00FFFF
    ConfigEntry<string> ColorTruck;      // 卡车颜色 #00FF00
    ConfigEntry<string> ColorCart;       // 推车颜色 #FFFF00
    ConfigEntry<float> DotSize;          // 点大小 0.25
    ConfigEntry<float> DotSpeed;         // 流动速度 4
    ConfigEntry<int> DotCount;           // 点数量 30
    ConfigEntry<string> ToggleKeyStr;    // 开关 F4
    ConfigEntry<string> NavNextKeyStr;   // 下一目标 F5
    ConfigEntry<string> NavPrevKeyStr;   // 上一目标 F6
    ConfigEntry<float> ToastPosX/Y;      // UI 提示位置
    
    void Awake() {
        // 绑定配置 → 解析按键 → 挂载 TrailManager
        gameObject.AddComponent<TrailManager>();
    }
}
```

#### TrailManager.cs（核心 ~200 行）

##### 目标获取
```csharp
enum TargetMode { ExtractionPoint, Truck, Cart }

// 玩家位置
var avatar = Access.Get<PlayerAvatar>(PlayerController.instance, "playerAvatarScript");
Vector3 playerPos = avatar.LastNavmeshPosition;

// 目标位置
switch (_currentMode):
  ExtractionPoint:
    if (allExtractionPointsCompleted) → TryGetTruckPosition()
    else → RoundDirector.instance.extractionPointCurrent.position
  Truck:
    → LevelGenerator.Instance.LevelPathTruck.position
  Cart:
    → PhysGrabCart（排除 Item 组件的口袋车）
```

##### 寻路（UpdatePath）
```csharp
// 1. 基础寻路
NavMesh.CalculatePath(playerPos, targetPos, -1, currentPath);

// 2. 深渊过滤（FilterPitCorners）
// 对每个 corner + Vector3.up * 0.3f 向下 Physics.Raycast
// 检查是否站在地面上，掉出地形的丢弃
// 重新 CalculatePath 到最后有效点

// 3. String Pulling（SmoothPath）
// 从 corner[0] 出发
// while (i < corners.Length - 1):
//   for (j = corners.Length-1; j > i+1; j--):
//     if (!NavMesh.Raycast(corners[i], corners[j], out hit, -1)):
//       // 直线可达 → 跳过中间所有 corner
//       nextCorner = j; break;
//   list.Add(corners[nextCorner])
//   i = nextCorner
```

##### 动画（AnimateDots）
```csharp
// 30 个 Sphere primitive (GameObject.CreatePrimitive)
// offset += Time.deltaTime * speed
// 每个 dot:
//   dist = (i * spacing + offset) % pathLength
//   position = GetPositionOnPath(dist, smoothedCorners)
//   首尾 2 米内渐变缩小（淡入淡出效果）
```

##### 反射工具（Access）
```csharp
public static T Get<T>(object inst, string name)   // 实例字段
public static T GetStatic<T>(Type t, string name)   // 静态字段
```

---

## 5. YAPYAP 复刻方案

### 5.1 可复用的部分（直接移植）

| 模块 | 来源 | 移植难度 |
|---|---|---|
| `TrailManager.AnimateDots()` | ExtractionTrail | 低 — Unity 标准 API，直接可用 |
| `TrailManager.SmoothPath()` | ExtractionTrail | 低 — `NavMesh.Raycast` 标准 API |
| `TrailManager.FilterPitCorners()` | ExtractionTrail | 低 — `Physics.Raycast` 标准 API |
| `Access` 反射工具 | ExtractionTrail | 低 — 独立工具类，直接可用 |
| BepInEx 配置框架 | ExtractionTrail | 低 — 标准模板 |

### 5.2 需要改写的部分

| 模块 | REPO 方式 | YAPYAP 方式 |
|---|---|---|
| 寻路 | `NavMesh.CalculatePath(player, target)` | `DungeonPathing.GetDetailedGuidancePath(generator, playerPos, playerRoom, targetRoom)` |
| 获取玩家房间 | 不需要（直接用 NavMesh） | `DungeonManager.Instance.Generator.GetRoomAtPosition(playerPos)` |
| 获取提取房间 | `RoundDirector.extractionPointCurrent` | `DungeonGenerator.FindRoomsOfType("ExtractionRoom")` |
| 卡车目标 | `LevelGenerator.LevelPathTruck` | 不需要（YAPYAP 没有卡车，使用 `TeleportExtractionCircle`） |
| 推车目标 | `PhysGrabCart` | 不需要（YAPYAP 无推车机制） |

### 5.3 YAPYAP 版完整路径计算伪代码

```csharp
var gen = DungeonManager.Instance.Generator;

// 1. 获取玩家房间
var playerRoom = gen.GetRoomAtPosition(playerTransform.position);

// 2. 获取提取房间
var extractRooms = gen.FindRoomsOfType("ExtractionRoom");
// 过滤到有活跃 TeleportExtractionCircle 的房间
var extractRoom = extractRooms.FirstOrDefault(r => 
    r.GetComponentInChildren<TeleportExtractionCircle>() != null
);

// 3. BFS 设置距离权重（DungeonPathing 内部需要）
gen.HasPath(playerRoom.node, extractRoom.node);

// 4. 获取详细路径
var path = DungeonPathing.GetDetailedGuidancePath(
    gen, playerTransform.position, playerRoom, extractRoom
);
// path 是 List<Vector3>，可以直接喂给 SmoothPath + AnimateDots
```

### 5.4 技术风险评估

| 风险 | 等级 | 说明 |
|---|---|---|
| `DungeonPathing` 是 public static | ✅ 无风险 | 可通过反射直接调用 |
| `DungeonGenerator` API 稳定 | ✅ 低风险 | 被多处调用，核心 API |
| NavMesh.Raycast 在 YAPYAP 可用 | ⚠️ 待验证 | YAPYAP 使用 NavMesh（`GetDetailedPathBetweenPositions` 内部调用），但不确认跨房间 Raycast 是否有效 |
| `SmoothPath` 跨房间效果 | ⚠️ 待验证 | YAPYAP 房间可能 NavMesh 不连续，string pulling 可能需要房间边界感知 |
| FindRoomsOfType("ExtractionRoom") | ⚠️ 待验证 | 需确认房间类型字符串是否匹配 |

---

## 6. 文件清单

```
research/
└── home-guidance-feasibility.md   ← 本文件

decompiled/
├── REPO/FullAssembly.cs           ← R.E.P.O. 完整反编译（6.7M）
├── YAPYAP/FullAssembly.cs         ← YAPYAP 完整反编译（7.3M）
└── ExtractionTrail/
    ├── ExtractionTrail.zip         ← 从 Thunderstore 下载（127KB）
    ├── ExtractionTrail.dll         ← 原始 DLL
    ├── README.md                   ← 原始 README
    ├── manifest.json               ← Thunderstore manifest
    ├── icon.png                    ← Mod 图标
    └── source/FullAssembly.cs      ← DLL 反编译（726 行）

tools/decompiler/Decompile/        ← 反编译工具项目
```

---

## 7. 指南针（UICompass）深度分析

> 用户确认游戏中右上角存在指南针组件。本节分析其实现细节，评估能否复用作路径导航方向指示器。

### 7.1 当前行为

指南针**始终指向提取圈**（`TeleportExtractionCircle`）：

```csharp
// OnExtractionCircleSpawned (source:176130)
extractionTarget = circle.GetComponentInChildren<Outline>().transform;
SetTarget(extractionTarget);
```

### 7.2 视觉结构

```
UICompass (3D 球体 → RenderToTexture → RawImage)
├── compassActiveFrame       ← 有目标时显示
├── compassInactiveFrame     ← 无目标时显示（灰色）
├── RenderObjectToDiffuseNormal  ← 独立相机渲染 3D 球体 → RenderTexture
├── UberLitUIGraphic         ← 光照 UI 着色器
└── UICompassReferences (Prefab)
    ├── CardinalDirectionsPivot   ← 锁定摄像机 yaw，始终显示 N/S/E/W 方位
    ├── TargetObjectPivot         ← 旋转指向目标方向（当前=提取圈）
    ├── ElevationIndicatorPivot   ← 上下仰角（0°=同层，90°=正上方）
    └── DistanceScaleTransform   ← 距离越近越大（0.5x~1.5x，50m~0.1m 区间映射）
```

### 7.3 各组件指向（用户视角）

| 组件 | 含义 |
|---|---|
| 罗盘刻度（CardinalDirectionsPivot） | 世界方位 N/S/E/W，随摄像机转动同步旋转 |
| 球体上的标记（TargetObjectPivot） | **提取圈的方向** |
| 上下箭头（ElevationIndicatorPivot） | 提取圈在你上方还是下方 |
| 球体大小（DistanceScaleTransform） | 离提取圈越近越大 |

### 7.4 更新机制

```csharp
// LateUpdate (source:176146)
if (currentTarget == null)
    → 显示 inactiveFrame，隐藏 activeFrame
else
    → UpdateCompassRotation()    // 计算目标方向、旋转各 Pivot
    → UpdateCompassAlignment()   // 计算对齐度、距离缩放
    → Shader.SetGlobalFloat      // 对齐度传给 shader（可能用于发光/颜色）
```

**方向计算**（`UpdateCompassRotation`，source:176242）：
```csharp
// 1. 摄像机 → 目标 的方向向量
Vector3 toTarget = target.position - camera.position;

// 2. TargetObjectPivot: 旋转到指向 toTarget 的水平方向
visualObjPivot.rotation = Slerp(..., Quaternion.Euler(0, cameraYaw, 0));

// 3. CardinalDirectionsPivot: 反向摄像机 yaw（底座不动）
cardinalPivot.rotation = Slerp(..., Quaternion.Euler(0, -cameraYaw, 0));

// 4. ElevationIndicatorPivot: 根据垂直高度差旋转
float elevation = GetElevationFromVerticalDistance(toTarget.y);
elevationPivot.rotation = Slerp(..., Quaternion.Euler(elevation, 0, 0));
```

**距离缩放**（`UpdateCompassAlignment`，source:176281）：
```csharp
float distance = Vector3.Distance(camera.position, target.position);
float scale = Remap(distance, 0.1f, 50f, 1.5f, 0.5f);
// 50m 外 → 0.5x ; 紧贴 → 1.5x
```

### 7.5 如何改为路径导航

核心思路：不指向提取圈，改为指向**路径的下一个路点**。

```
现有：              改为：
  玩家 → 提取圈       玩家 → 路点1 → 路点2 → ... → 提取圈
```

**实现方式**：
```csharp
// 1. 通过反射获取 UICompass 实例（游戏中已有）
var compass = FindObjectOfType<UICompass>();

// 2. 计算路径
var path = DungeonPathing.GetDetailedGuidancePath(
    gen, playerPos, playerRoom, extractRoom);
// path = [玩家位置, 门A, 门B, ..., 提取圈]

// 3. 创建 dummy Transform 指向下个路点
int nextIdx = FindNextWaypoint(playerPos, path);

// 4. 替换指南针目标（核心：SetTarget 是 public 方法）
compass.SetTarget(waypointDummy);
```

**优点**：
- 不破坏现有指南针的外观
- `SetTarget` 是 public，直接可调
- 指南针本身已有距离缩放、对齐度等反馈
- 比 30 个 Sphere 干净得多

**需要注意**：
- 需要持续更新 target（玩家移动后路点变化）
- 到达路点后需要切换到下一个
- 首次获取 compass 实例需要反射或 `FindObjectOfType`

### 7.6 设置开关链路

```
UISettings.enableCompassSetting (UISettingToggle)
  → OnSettingChanged → SetCompassEnabled(bool)
    → FindObjectsByType<UICompass>(Include inactive)
    → compass.SetEnabled(bool)
```

指南针开关在游戏设置里是真实存在的，可以正常开关。

---

## 8. 行动建议

1. **验证 API 可用性**：写一个简单 Harmony 补丁，测试 `DungeonPathing.GetDetailedGuidancePath` 是否在运行时返回有效路径
2. **劫持指南针**：`FindObjectOfType<UICompass>` → 替换 `SetTarget` 为路径路点
3. **实现路点跟随逻辑**：到达路点时自动切换下一个
4. **验证 NavMesh.Raycast**：测试跨房间的 string pulling 是否可行（可选，路径本身已足够平滑）
5. **打包发布**：按 DEVELOPMENT.md 规范走完整流程
