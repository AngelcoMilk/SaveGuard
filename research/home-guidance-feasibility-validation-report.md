# YAPYAP 回家指引可行性研究准确性核验报告

> 核验日期：2026-07-25  
> 核验对象：`research/home-guidance-feasibility.md`  
> 核验范围：R.E.P.O.、YAPYAP、ExtractionTrail 反编译源码，本地开发规范与反编译工具复现

---

## 1. 结论摘要

原研究报告的**总体方向成立**：

- R.E.P.O. 确实存在 `MapBacktrack` 地图路径点逻辑
- YAPYAP 确实提供公开的 `DungeonPathing`、提取圈事件和指南针接口
- ExtractionTrail 确实实现了 NavMesh 路径、路径点平滑和流动圆点
- 在 YAPYAP 中制作“回家/撤离指引”Mod 具有技术可行性

但报告中有数项会直接影响实现方案的关键误判：

1. `DungeonPathing.GetDetailedGuidancePath` **不是完整的逐段 NavMesh 行走路径**
2. 该方法的终点是 `targetRoom.transform.position`，**不是提取圈位置**
3. `UICompass.TargetObjectPivot` **没有按目标的水平左右方向旋转**
4. 仅调用 `UICompass.SetTarget()` **不足以获得左右方向箭头导航**
5. `UICompass`、`GuidanceSpell` 和 `UICompassReferences` 不应直接定性为“死代码”
6. ExtractionTrail 不应描述为可以“直接移植”；更适合借鉴行为后独立重写
7. ExtractionTrail README 宣称使用 `NavMeshAgent`，但本地 1.5.1 DLL 中实际上没有该类型

综合评价：

| 维度 | 评价 |
|---|---|
| 可行性方向 | 正确 |
| R.E.P.O. 导航概述 | 基本正确，措辞需收敛 |
| YAPYAP 公共 API | 基本正确 |
| YAPYAP 路径数据语义 | 存在关键错误 |
| UICompass 行为分析 | 存在关键错误 |
| ExtractionTrail 分析 | 主流程正确，细节需修正 |
| 复刻方案 | 可行，但需重新设计路径终点、指南针显示和容错 |

建议将原文标记为：**“可行性成立，但实现设计需要重大修订”**。

---

## 2. 核验方法与文件状态

用户列出的 8 个路径均存在：

1. `research/home-guidance-feasibility.md`
2. `decompiled/REPO/FullAssembly.cs`
3. `decompiled/YAPYAP/FullAssembly.cs`
4. `decompiled/ExtractionTrail/ExtractionTrail.dll`
5. `decompiled/ExtractionTrail/source/FullAssembly.cs`
6. `tools/decompiler/Decompile/`
7. `DEVELOPMENT.md`
8. `README.md`

已读取本地 `DEVELOPMENT.md`，核验工作遵循其中的主要原则：

- 追踪方法调用链，不只检查类是否存在
- 区分 Server、Client 和 Mirror RPC 边界
- 公共 API 优先直接调用，私有字段才考虑反射
- 不将反编译类定义直接等同于场景中已挂载、已启用
- 将运行时验证、Host/Client 测试和游戏更新兼容性列为必要步骤

### 2.1 反编译工具复现

使用本地项目：

```text
tools/decompiler/Decompile/Decompile.csproj
```

对以下 DLL 重新执行反编译：

```text
decompiled/ExtractionTrail/ExtractionTrail.dll
```

复现结果：

- 工具成功运行
- 新生成 `FullAssembly.cs` 为 26,906 字节
- 与已有 `decompiled/ExtractionTrail/source/FullAssembly.cs` 的 SHA-256 完全相同
- SHA-256：`31ea42c9103e54eaf6fd63a6c79e2d62f588d1d5295f1806304f613f5f1111b6`

因此 ExtractionTrail 本地反编译结果可以稳定复现。

---

## 3. R.E.P.O. 部分核验

### 3.1 ArrowUI

原文结论：

> 有定义，但几乎未被调用；仅存在于教程/UI 演示中；不是实际导航方式

核验结果：**部分正确，结论过强**。

源码证据：

- 类：`decompiled/REPO/FullAssembly.cs:194814`
- `ArrowShow`：`:194849-194863`
- `ArrowShowWorldPos`：`:194865-194877`
- 更新与显隐动画：`:194879-194924`

程序集内可见的直接调用主要位于：

- `TruckScreenText`：`:90601`
- `TutorialDirector`：`:198463`、`:198533`、`:198544`、`:198609`

没有发现世界坐标包装接口的直接 C# 调用。

建议改写为：

> `ArrowUI` 是 HUD 提示箭头。当前程序集内只发现少量直接调用，集中于教程与卡车 UI 提示；未发现它被直接用于撤离路径导航。是否还存在 Prefab、UnityEvent、反射或其他程序集调用，需要资产或运行时验证。

不能只根据程序集内直接调用断言“仅存在于教程”或“绝对没有实际使用”。

### 3.2 MapBacktrack

原文的目标分支和 NavMesh 面包屑思路基本正确。

源码证据：

- 类：`decompiled/REPO/FullAssembly.cs:144399`
- 创建路径点并启动协程：`:144424-144434`
- Truck 目标查找：`:144443-144449`
- 玩家起点：`:144453`
- 全部提取后指向 Truck：`:144455-144458`
- 否则指向 `extractionPointCurrent`：`:144459-144462`
- NavMesh 路径计算：`:144478`
- 按 `spacing` 沿 corners 插值：`:144482-144509`
- 按地图楼层设置透明度：`:144497-144504`
- 投影到地图坐标：`:144505-144506`

需要修订的细节：

1. Truck 目标不是简单的“Truck 房间中心”，而是首个关联 Truck 房间的 `LevelPoint.transform.position`
2. `MapBacktrack` 不会自己寻找最近的未完成提取点
3. `extractionPointCurrent == null` 时，目标仍保持玩家当前位置
4. 地图未开启时不显示
5. 同层且距离小于 10 米时不显示
6. 原代码没有检查 `NavMesh.CalculatePath` 返回值或 `path.status`
7. `resetWait` 字段存在，但当前源码中没有被使用

建议将流程改为：

```text
1. 等待关卡生成完成，并排除商店场景
2. 从 Truck 房间关联的 LevelPoint 取得回程目标
3. 最终撤离标志为真 → 指向 Truck
4. 否则仅在 extractionPointCurrent 存在时指向当前提取点
5. 地图关闭，或同层且不足 10 米 → 暂停显示
6. 计算 NavMeshPath，但原版未验证路径是否完整
7. 按 spacing 沿 corners 投放地图点，并按楼层调整透明度
```

“实际在用的核心导航系统”应改成更谨慎的表述：

> 源码中存在完整的地图回溯实现；是否在目标场景中挂载并启用，需要检查 Prefab/场景资产或运行时对象。

### 3.3 ExtractionPoint 与 RoundDirector

原文所列类和主要字段存在：

- `ExtractionPoint`：`:81533`
- `extractionArea`：`:81549`
- `safetySpawn`：`:81633`
- `haulGoal`：`:81836`
- `RoundDirector`：`:187403`
- `extractionPointList`：`:187437`
- `extractionPointCurrent`：`:187439`
- `allExtractionPointsCompleted`：`:187446`

需要补充：

- `ExtractionPoint.Start()` 会注册自身并增加提取点总数：`:81939-81964`
- 激活时设置 current 并锁定其他点：`:82030-82053`
- 多人模式通过 `RoundDirector` RPC 处理激活
- 完成阶段清 active、解锁并增加完成计数：`:83535-83562`

`allExtractionPointsCompleted` 不宜简单解释为“完成计数已经等于全部提取点”。最后一个提取点可能在正式递增完成计数前就触发该标志。更准确的语义是：

> 最终提取已经确认，系统进入回卡车阶段的全局标志。

另外，第一提取点的最近点选择发生在 `TruckDoor` 流程，而不是 `MapBacktrack`：

- `TruckDoor` 获取最近提取点：`:91803-91834`
- `SemiFunc.ExtractionPointGetNearest` 使用普通欧氏距离：`:110020-110034`

---

## 4. YAPYAP 部分核验

### 4.1 DungeonPathing 公共 API

以下结论正确：

- `DungeonPathing` 为 `public static class`：`decompiled/YAPYAP/FullAssembly.cs:56364`
- `CanPathBetweenRooms`：`:56366`
- `GetRandomReachableRoom`：`:56400`
- `GetAllReachableRooms`：`:56427`
- `GetPathToTarget`：`:56498`
- `GetDetailedPathBetweenDoors`：`:56560`
- `GetDetailedGuidancePath`：`:56567`

因为类和方法均为 public，应该**直接引用调用**，不需要反射。

原文风险表中的：

> `DungeonPathing` 是 public static，可通过反射直接调用

建议改为：

> `DungeonPathing` 和目标方法为 public static，可直接调用；主要风险来自游戏版本变化、路径语义和运行时地图状态，而不是可见性。

### 4.2 GetDetailedGuidancePath 的关键误读

这是原报告最重要的问题。

原文认为流程是：

```text
房间 BFS → 找相邻房间门对 → 对门 A 到门 B 计算 NavMesh → 得到完整路径
```

源码实际行为：

```csharp
list.Add(startPosition + offset);

foreach (相邻房间对)
{
    FindConnectingDoors(roomA, roomB, out doorA, out doorB);
    list.AddRange(GetDetailedPathBetweenDoors(doorA, doorB));
}

list.Add(targetRoom.transform.position + offset);
```

关键问题：

- 一个 `GraphConnection` 只有一个 `doorRef`
- `FindConnectingDoors` 从两个房间节点中找到的是同一个共享连接
- `doorA` 与 `doorB` 通常最终指向同一 `doorRef`
- `GetDetailedPathBetweenDoors` 因此通常是在同一门位置到自身之间计算路径
- 方法没有计算“玩家到第一扇门”的完整 NavMesh 段
- 也没有计算“房间入口门到出口门”的完整房间内路径

因此返回结果更接近：

```text
玩家起点
+ 各房间连接处的 spawnedDoor 位置
+ 目标房间 Transform 原点
```

它适合作为**高层房间路标序列**，不应直接描述成完整可行走 NavMesh corners。

### 4.3 路径终点错误

原文写：

```text
path = [玩家位置, 门A, 门B, ..., 提取圈]
```

这是错误的。

`GetDetailedGuidancePath` 最后添加的是：

```csharp
targetRoom.transform.position
```

证据：`decompiled/YAPYAP/FullAssembly.cs:56590`

如果需要精确到提取圈，必须额外取得 `TeleportExtractionCircle.transform.position`，并追加目标房间内的尾段路径。

### 4.4 SimplifyPath 表述

原文称为“均匀采样”。

源码实际是按照**路径点列表索引**进行等间隔插值降采样：`:56616-56630`。

它不是按照世界距离或累计路径长度做真正的等距采样。

建议改为：

> 按输入点索引等间隔插值，将路径点数量压缩到 `maxPathPoints`。

### 4.5 `HasPath` 预调用是冗余的

原伪代码：

```csharp
gen.HasPath(playerRoom.node, extractRoom.node);
var path = DungeonPathing.GetDetailedGuidancePath(...);
```

但 `GetPathToTarget` 内部已经调用：

```csharp
dungeonGenerator.HasPath(startRoom.node, endRoom.node)
```

证据：`:56511`

因此显式调用可删除，除非只是用作单独的可达性预检查。

### 4.6 提取圈目标获取方式

原文从所有 `ExtractionRoom` 中选择第一个带 `TeleportExtractionCircle` 子组件的房间：

```csharp
FindRoomsOfType("ExtractionRoom")
    .FirstOrDefault(r => r.GetComponentInChildren<TeleportExtractionCircle>() != null)
```

问题：

- 不能保证圈组件一定是 `RoomData` 的子对象
- `GetComponentInChildren != null` 不代表“提取正在活跃”
- 多个候选房间时 `FirstOrDefault` 的选择语义不明确

更可靠的方式：

1. 监听 `TeleportExtractionCircle.OnSpawned`
2. 或查找当前活动的 `TeleportExtractionCircle`
3. 用 circle 的世界坐标调用 `GetRoomAtPosition`

建议：

```csharp
var circle = Object.FindFirstObjectByType<TeleportExtractionCircle>(
    FindObjectsInactive.Exclude);

var extractRoom = circle != null
    ? gen.GetRoomAtPosition(circle.transform.position)
    : null;
```

`"ExtractionRoom"` 字符串本身并非完全未知，因为原版 `GuidanceSpell` 已使用该字符串：`:130374-130378`。运行时仍需验证当前地图是否存在、是否唯一和是否可达。

### 4.7 TeleportExtractionCircle

以下事件确实存在：

- `OnSpawned`：`:80388`
- `OnExtractionStarted`：`:80390`
- `OnExtractionEnded`：`:80392`

其中 `OnSpawned` 在 `OnStartClient` 中触发：`:80399-80402`。

但原报告列出的字段：

- `circleRadius`
- `extractLoopVfx`

均为 `[SerializeField] private`，不是公共 API。如确实需要读取，才考虑 Harmony `AccessTools` 或反射。

### 4.8 UICompass 不是“死代码”

原文将以下类归入“存在于代码但未激活”：

- `UICompass`
- `GuidanceSpell`
- `UICompassReferences`

这一定性缺乏充分依据。

源码显示：

- `UICompass.OnEnable()` 订阅游戏状态和提取圈生成事件：`:176115-176128`
- 提取圈生成后会调用 `SetTarget`：`:176130-176143`
- `UICompassReferences` 会从序列化 prefab 实例化：`:176095-176104`
- 原生设置系统会查找 `UICompass` 并调用 `SetEnabled`
- `GuidanceSpell.OnSpellActivate()` 会调用完整的引导逻辑：`:130355-130381`

仅从程序集无法确认所有场景和资产绑定，但同样不能将其判为死代码。

建议将章节标题改为：

> 存在完整代码实现、但需要资产或运行时确认绑定状态的组件

### 4.9 UICompass 水平指向分析错误

原报告写：

> `TargetObjectPivot` 旋转到目标的水平方向

源码实际为：

```csharp
float cameraYaw = camTransform.eulerAngles.y;
visualObjPivot.localRotation = Quaternion.Euler(0, cameraYaw, zSpin);
```

证据：`:176242-176261`

虽然代码计算了 `target.position - camera.position`，但该向量只用于：

- 垂直高度差
- 距离
- 与摄像机 forward 的点积 alignment

水平目标向量没有用于 `TargetObjectPivot` 的 yaw。

`UpdateCompassAlignment` 使用 `Vector3.Dot`：`:176281-176315`。点积只能表示“朝向目标的程度”，不能区分目标在左侧还是右侧。

因此更准确的解释是：

> 该指南针提供摄像机朝向与目标方向的对齐程度、目标距离和高度差反馈，但当前 C# 逻辑中没有直接产生带左右符号的目标方位旋转。

### 4.10 仅调用 SetTarget 不足以实现路径方向箭头

`UICompass.SetTarget()` 主要做两件事：

- 保存 `currentTarget`
- 切换 active/inactive UI 和渲染状态

证据：`:176219-176240`

它不会自动把目标左右方位映射到 `TargetObjectPivot`。

此外，以下原版逻辑可能重新把目标设回提取圈：

- `OnExtractionCircleSpawned`
- `RefreshTarget`
- `SetEnabled`

所以如果要复用 UICompass，应至少：

1. 用 Harmony 管理原版自动目标刷新，避免覆盖 Mod 路点
2. 自行计算目标的 signed yaw，例如 `Vector3.SignedAngle`
3. 修改目标 Pivot 或自建独立方向标记
4. 保留原版距离、alignment 与 elevation 逻辑时，明确它们分别承担什么反馈

---

## 5. ExtractionTrail 部分核验

### 5.1 版本信息

本地文件存在版本信息不一致：

- `manifest.json`：1.5.1
- `README.md`：1.5.1
- `[BepInPlugin]`：1.5.1，源码 `:46`
- DLL `AssemblyVersion`、`AssemblyFileVersion`、`AssemblyInformationalVersion`：1.3.0，源码 `:7-12`
- DLL 构建配置为 Debug，源码 `:6`

建议原文改为：

> 包清单、README 和 BepInEx 插件版本为 1.5.1，但 DLL 程序集元数据仍停留在 1.3.0，并标记为 Debug 构建。

### 5.2 依赖描述

`manifest.json` 只声明：

```text
BepInEx-BepInExPack-5.4.2100
```

应写成“Thunderstore manifest 声明依赖仅为……”，不能据此推导：

- DLL 没有 Unity 和游戏程序集依赖
- YAPYAP 与 R.E.P.O. 使用同一套可直接复用的运行时类型
- 目标游戏一定使用相同的 BepInEx 包版本

### 5.3 README 与 DLL 实现不一致

README 1.5.0/1.5.1 更新记录宣称使用内部 `NavMeshAgent`。

但本地 DLL 反编译源码中没有 `NavMeshAgent`，实际使用：

```csharp
NavMesh.CalculatePath(...)
```

证据：`decompiled/ExtractionTrail/source/FullAssembly.cs:488`、`:573`

这可能意味着 README 陈旧、构建包不匹配或实现被回退。研究报告应明确记录这一异常。

### 5.4 源码结构与行数

整个反编译文件确为 726 行。

但：

- `Plugin`：`:46-130`
- `TrailManager`：`:131-711`
- `Access`：`:712-725`

`TrailManager` 实际约 581 行，不是“核心约 200 行”。另外，单文件反编译结果不能还原原作者真实的 `Plugin.cs` 与 `TrailManager.cs` 文件边界。

### 5.5 目标获取

原报告的主要流程正确：

- 玩家与 `LastNavmeshPosition`：`:435-449`
- 提取点目标：`:453-469`
- Truck 目标：`:527-542`
- Cart 目标：`:474-483`

但推车过滤还会排除根层级中包含 `PlayerAvatar` 的对象：`:395-415`，不只是排除 `Item`。

### 5.6 NavMesh 与坑洞过滤

基础寻路正确：

```csharp
NavMesh.CalculatePath(playerPos, targetPos, -1, currentPath)
```

但实现存在问题：

- 没有检查 `currentPath.status`
- `currentPath.corners.CopyTo(currentPath.corners, 0)` 是无效自拷贝：`:491`
- 创建并计算的第二个 `NavMeshPath val5` 没有被使用：`:497-499`

`FilterPitCorners` 不是通用“深渊过滤器”，而是有限启发式：

- 从 corner 上方 0.3 米向下射线
- 射线长度 5 米
- 只检查 `Default` 层
- 只检查 corner，不检查 corner 之间的线段
- 首个失败后截断后续点
- 有效点少于两个时保留原路径

因此建议改称：

> 基于向下射线的 corner 截断启发式，不能保证路径段不会跨越坑洞。

### 5.7 SmoothPath

原报告对 string pulling 循环的描述基本正确：`:611-639`。

但不应直接将 YAPYAP 默认上抬 0.5 米的 guidance 点传入 `NavMesh.Raycast`。应先保留贴近 NavMesh 的计算点，完成路径检测后再给渲染点增加高度偏移。

### 5.8 AnimateDots

应写为：

- 创建 `DotCount` 个 Sphere，默认值为 30
- 路径首尾 2 米按 localScale 缩小到 0
- 不是材质 alpha 淡入淡出

证据：`:195-208`、`:641-673`

`AnimateDots()` 依赖多个成员和辅助方法，不能只复制一个方法即直接工作。

### 5.9 Access 反射工具

`Access`：

- 只支持字段，不支持属性和方法
- 每次调用都重新查找字段，没有缓存
- 字段不存在时静默返回 `default(T)`

证据：`:712-725`

YAPYAP 的 public API 应直接调用。只有访问 private/internal 数据且没有稳定公共替代方案时，才使用 `AccessTools` 或反射，并应增加字段缺失日志与版本守卫。

### 5.10 “直接移植”表述

建议将原文：

> 可复用的部分（直接移植）

改为：

> 可借鉴的行为与通用算法思路（独立实现并运行时验证）

原因：

- 反编译代码包含游戏专用类型和隐藏假设
- 当前包内未见 LICENSE
- `AnimateDots`、`FilterPitCorners` 和 `SmoothPath` 并非完全独立方法
- YAPYAP 的房间图、NavMesh 和目标生命周期与 R.E.P.O. 不同
- 直接复制会增加维护、兼容和发布风险

该段属于工程与发布风险提示，不构成法律意见。

---

## 6. 修订后的推荐实现方案

### 6.1 推荐总体架构

建议不要把 `GetDetailedGuidancePath` 当作最终路线，而是分层处理：

```text
目标发现层
  TeleportExtractionCircle.OnSpawned / 当前活动 circle

房间路径层
  DungeonPathing.GetPathToTarget

门路标层
  从 GraphConnection.doorRef 获取相邻房间连接门

房间内 NavMesh 层
  玩家 → 第一扇门
  当前入口门 → 当前出口门
  最后一扇门 → 提取圈

显示层
  A. 世界空间流动点
  B. LineRenderer
  C. 自定义 UI 方位箭头
  D. 对 UICompass 做受控补丁
```

### 6.2 推荐伪代码

```csharp
var gen = DungeonManager.Instance?.Generator;
var circle = Object.FindFirstObjectByType<TeleportExtractionCircle>(
    FindObjectsInactive.Exclude);

if (gen == null || circle == null || playerTransform == null)
    return;

Vector3 playerPos = playerTransform.position;
Vector3 targetPos = circle.transform.position;

var playerRoom = gen.GetRoomAtPosition(playerPos);
var targetRoom = gen.GetRoomAtPosition(targetPos);

if (playerRoom == null || targetRoom == null)
    return;

var roomPath = DungeonPathing.GetPathToTarget(
    gen, playerRoom, targetRoom);

if (roomPath == null || roomPath.Count == 0)
    return;

// 根据每个 GraphConnection.doorRef 构建门路标序列
// 对玩家→门、门→门、最后门→circle 分段执行：
// 1. NavMesh.SamplePosition
// 2. NavMesh.CalculatePath
// 3. 检查 bool、path.status 和 corners
// 4. 拼接并去除重复点
// 5. 完成计算后再添加显示高度偏移
```

如果先做最小原型，可以暂时使用：

```csharp
var guidance = DungeonPathing.GetDetailedGuidancePath(
    gen,
    playerPos,
    playerRoom,
    targetRoom,
    maxPathPoints: 100,
    verticalOffset: 0f);
```

但必须把它标记为：

> 高层房间连接路标原型，不保证是完整的行走路径。

并额外将尾段连接到实际 `circle.transform.position`。

### 6.3 指南针方案选择

#### 方案 A：独立 HUD 箭头，推荐

- 根据相机到下一路点的方向计算 signed angle
- 独立显示左、右、前、后或屏幕边缘箭头
- 不修改原版 UICompass 内部状态
- 与原版提取圈指南针可并存

优点：实现语义清晰，受原版自动刷新影响较小。

#### 方案 B：复用 UICompass 外观

需要：

- 控制或 Patch `RefreshTarget`
- 防止原版提取圈事件覆盖路点目标
- 自行修改目标 Pivot 的 signed yaw
- 保留或重写 alignment、elevation、distance 行为

仅调用 `SetTarget(dummyTransform)` 不足以实现左右方向显示。

#### 方案 C：世界空间路径点

- 视觉上最直观
- 可以借鉴流动点设计，但应独立实现
- 必须限制对象数量、更新频率与材质实例
- 应优先使用对象池，而不是频繁创建销毁 Sphere

---

## 7. 按 DEVELOPMENT.md 补充的工程要求

原研究报告的行动建议应增加以下项目：

### 7.1 Build Guard

- 记录当前 `Assembly-CSharp.dll` 哈希
- 游戏更新后若关键方法签名变化，自动停用路径功能
- 日志说明不兼容原因，不要静默继续运行

### 7.2 空值与路径状态检查

至少检查：

- `DungeonManager.Instance`
- `Generator`
- `DungeonGraph`
- 玩家 Transform/房间
- `TeleportExtractionCircle`
- 目标房间
- `GraphConnection.doorRef`
- `spawnedDoor`
- `NavMesh.CalculatePath` 返回值
- `NavMeshPath.status == PathComplete`
- corners 数量

### 7.3 Mirror 边界

- 纯本地 UI 和路径显示原则上应保持 Client-side
- 不要无条件调用带 `[Mirror.Server]` 限制的方法
- `DungeonGameplay.TryGetGuidancePath` 有服务器语义，客户端 Mod 不能默认把它当普通本地 API
- Host 与普通 Client 都需要实测

### 7.4 设置与冲突控制

建议游戏内只暴露核心设置：

- 启用回家指引
- 显示方式：世界路径点 / HUD 箭头 / 指南针实验模式
- 路点密度或最大数量

高级设置保留在 BepInEx 配置文件。

如果修改原生指南针目标，应提供：

- 恢复原版目标的方法
- 场景卸载与回合结束清理
- Mod 禁用时清理 dummy Transform 和 Harmony 状态
- Finalizer 或等效异常恢复逻辑

### 7.5 运行时测试矩阵

至少测试：

- 单人/Host
- 普通 Client
- 玩家与提取圈在同一房间
- 跨多个房间
- 跨楼层
- 提取圈未生成
- 玩家不在有效 NavMesh
- 目标不在有效 NavMesh
- 路径为 Partial/Invalid
- 地图生成尚未结束
- 回合结束、场景切换和断线重连
- 原版指南针开关关闭
- 与其他 UI 或导航 Mod 共存

---

## 8. 建议直接修改原研究文档的项目

### 必须修改

1. 删除或改写“UICompass/GuidanceSpell/UICompassReferences 是死代码”
2. 修正 `GetDetailedGuidancePath` 的路径数据语义
3. 将路径终点从“提取圈”改为“目标房间 Transform 原点”
4. 删除“TargetObjectPivot 指向目标水平方向”的结论
5. 删除“只需 SetTarget 即可劫持指南针实现方向导航”的结论
6. 删除冗余的 `gen.HasPath(...)` 必需步骤
7. 将“直接移植”改为“独立重写、借鉴行为”
8. 补充 ExtractionTrail README 与 DLL 的 `NavMeshAgent` 不一致

### 建议修改

1. 将“实际在用”改成“源码实现完整；场景挂载需验证”
2. 将“所有提取点完成”解释为“进入最终回卡车阶段标志”
3. 将“均匀采样”改成“按点索引插值降采样”
4. 将固定“30 个 Sphere”改成“`DotCount` 个，默认 30”
5. 将“淡入淡出”改成“首尾缩放”
6. 明确 `circleRadius`、`extractLoopVfx` 是 private
7. 明确 `DungeonPathing` 应直接调用，而不是反射调用
8. 补充 `NavMesh.CalculatePath` 和 `path.status` 检查

---

## 9. 最终判断

### 是否可以开发

**可以。**

YAPYAP 已具备：

- 房间图与 BFS 路径
- 房间定位
- 提取圈生命周期事件
- NavMesh API 使用基础
- 原生指南针与 LineRenderer 引导实现参考

### 是否可以按原报告直接实现

**不建议。**

如果直接按照原报告实施，最可能遇到的问题是：

- 路线只连接房门路标，无法形成完整地面路径
- 最终路线停在目标房间原点而不是提取圈
- 对上抬路径点执行 NavMesh.Raycast 导致错误判断
- 调用 `SetTarget` 后指南针仍不显示正确的左右方向
- 原版刷新逻辑覆盖 Mod 目标
- 客户端误用服务器方法

### 推荐下一步

1. 先实现“提取圈发现 + 房间路径 + 调试线”最小原型
2. 在运行时记录每个 guidance 点、门位置、房间原点和 circle 位置
3. 验证 `GetDetailedGuidancePath` 的实际输出，确认重复门点问题
4. 编写真正的分段 NavMesh 拼接器
5. 首版优先采用独立 HUD 箭头或 LineRenderer
6. 将 UICompass 深度复用作为实验性后续功能
7. 完成 Host/Client、跨层和路径失败测试后再进入打包阶段
