# YAPYAP 撤离指引运行时设计

> 日期：2026-07-25  
> 目标：完整保留指南针区域的大背景、外框、布局与设置开关，只替换内部方向显示动画为实时最快路线箭头；第一名玩家到达撤离点后，为尚未到达过的其他玩家显示当前步行段光点；路径考虑有向传送点

---

## 1. 最终行为定义

### 1.1 正常状态

- 原版 `UICompass` 保持不变
- 指南针继续以 `TeleportExtractionCircle` 为目标
- 不显示 Mod 的世界空间路径光点

### 1.2 第一名玩家到达撤离点

当任意一名存活、未提取玩家首次进入撤离点到达半径后：

- 本轮全局指引解锁
- 该玩家被记录为“已经到达过”
- 该玩家自己不显示路径光点，但导航箭头继续工作
- 其他存活、未提取、尚未到达过的玩家开始显示当前最快路线的当前步行段光点

### 1.3 后续玩家到达

当其他玩家进入到达半径后：

- 将其加入本轮已到达集合
- 立即隐藏该玩家本地的路径光点
- 即使该玩家随后离开撤离点，本轮也不再显示路径

### 1.4 回合结束

以下状态全部清空：

- `guidanceUnlocked`
- `reachedPlayerNetIds`
- 当前路线缓存
- 光点对象
- 传送点图缓存

---

## 2. 源码事实

### 2.1 原版没有“玩家首次进入撤离点”事件

`TeleportExtractionCircle` 只有三个公共静态事件：

- `OnSpawned`：`decompiled/YAPYAP/FullAssembly.cs:80388`
- `OnExtractionStarted`：`:80390`
- `OnExtractionEnded`：`:80392`

其中：

- `OnSpawned` 在客户端 `OnStartClient` 触发：`:80399-80402`
- `OnExtractionStarted` 表示正式提取流程已经开始：`:80555-80559`
- `OnExtractionEnded` 表示提取协程结束：`:80606-80609`

所以不能用 `OnExtractionStarted` 代表“第一名玩家刚刚到达”。

### 2.2 原版存在两个不同半径

`missingPlayerMinDist` 默认 10 米：`:80355-80358`

- 只在玩家尝试交互提取时检查
- 用于判断是否有玩家距离撤离点太远
- 逻辑位于 `:80489-80499`

`circleRadius` 默认 1 米：`:80323-80324`

- 真正决定玩家是否被提取
- 服务器在 `SvExtractPlayers` 中检查
- 逻辑位于 `:80618-80627`

推荐把“已经到达撤离点”定义为进入 `circleRadius`。

已确认使用较宽松的独立到达半径：

```text
ArrivalRadius = 2.0m
```

该判定用于解锁 Mod 指引和记录“到达过”，不改变原版实际提取仍使用的 private `circleRadius`。因此玩家可能先被 Mod 记录为到达，仍需再靠近才能满足原版提取判定。

### 2.3 玩家集合与本地玩家

玩家字典：

```csharp
GameManager.Instance.playersByPlayerId
```

定义：`:95887-95890`

玩家在客户端和服务器分别注册：

- 客户端：`:105515-105522`
- 服务器：`:105556-105563`
- 销毁时移除：`:105585-105599`

本地玩家：

```csharp
Pawn.LocalInstance
```

- 获得 authority 时设置：`:105602-105610`
- 失去 authority 时清空：`:105634-105640`

玩家网络 ID：

```csharp
pawn.netId // uint
```

`PlayerNetId` 只是转为 `int`：`:105070-105073`。

本 Mod 内部建议统一使用原始 `uint netId`。

### 2.4 `IsExtracted` 不能代表“到达过”

`Pawn.IsExtracted` 是 SyncVar：`:104938-104942`、`:105086-105096`。

它只在正式完成提取后设置：

```csharp
pawn.IsExtracted = true;
```

位置：`:96718-96736`。

因此必须维护独立的“曾到达”集合。

---

## 3. 到达状态模型

推荐状态：

```csharp
bool guidanceUnlocked;
HashSet<uint> reachedPlayerNetIds;
```

语义：

- `guidanceUnlocked`：本轮是否曾经有玩家到达撤离点
- `reachedPlayerNetIds`：本轮已经到达过撤离点的玩家

`guidanceUnlocked` 不应简单等于 `reachedPlayerNetIds.Count > 0`，原因是：

- 第一名到达者可能断线
- 断线后仍应保持本轮指引已经解锁

### 3.1 本地显示门控

```csharp
Pawn local = Pawn.LocalInstance;

bool shouldShowTrail =
    GameManager.Instance != null &&
    GameManager.Instance.RoundActive &&
    guidanceUnlocked &&
    local != null &&
    !local.IsDead &&
    !local.IsExtracted &&
    !reachedPlayerNetIds.Contains(local.netId);
```

导航箭头始终对存活、未提取的本地玩家启用；该门控只控制额外的世界路径光点。

---

## 4. 状态同步方案

## 4.1 推荐首版：各客户端独立观察

每个安装 Mod 的客户端自行执行：

```text
每 0.1–0.25 秒：
1. 获取当前 TeleportExtractionCircle
2. 遍历 playersByPlayerId.Values
3. 判断玩家是否进入 ArrivalRadius
4. 将满足条件的 netId 加入本地 reached 集合
5. 第一次加入时将 guidanceUnlocked 置为 true
```

优点：

- 不增加自定义 Mirror 消息
- 不增加网络 Prefab
- 不依赖 Mirror Weaver 注入 NetworkBehaviour
- 未安装 Mod 的玩家仍可正常联机
- 光点本来就是客户端视觉，无需同步 GameObject

缺点：

- 不同客户端的首次检测时间可能相差少量帧
- 晚加入客户端不知道之前是否有人“到达后又离开”
- 如果远端位置同步延迟严重，状态可能短暂不一致

对于首版 QoL Mod，这通常是兼容性最好的方案。

### 安装要求

- 安装 Mod 的玩家可以看到指引光点
- 未安装的玩家看不到光点，但不影响游戏
- Host 不一定必须安装
- 若希望所有玩家看到，所有玩家都应安装

## 4.2 可选严格模式：Host 权威同步

Host/服务器负责范围检测，并同步：

```csharp
bool guidanceUnlocked;
uint[] reachedPlayerNetIds;
```

推荐使用自定义快照消息，而不是动态添加 `NetworkBehaviour`：

```csharp
struct GuidanceSnapshotMessage : NetworkMessage
{
    public uint roundEpoch;
    public bool unlocked;
    public uint[] reachedNetIds;
}
```

必须处理：

- 新客户端连接后的完整快照
- 回合 epoch，避免旧消息污染下一回合
- 自定义消息 Reader/Writer 或 Mirror Weaver
- 客户端和 Host 的 Mod 版本一致性
- 未安装 Mod 的客户端不能接收未知自定义消息

严格模式建议规定：

```text
Host 必须安装
所有希望加入房间的客户端也必须安装兼容版本
```

如果需要兼容未安装客户端，必须设计能力协商，并且只向已确认安装的连接发送 Mod 消息。这个方案比首版复杂，不建议先做。

---

## 5. 传送点事实

### 5.1 传送点类型

地下城死路传送点：

```csharp
TeleportDeadEndCircle
```

类位置：`decompiled/YAPYAP/FullAssembly.cs:78710`。

### 5.2 配对规则

服务器在生成完房间对象后查找传送点并配对：`:52725-52758`。

`PairTeleporters`：`:52769-52814`。

规则：

- 剩余数量恰好为 3 时，按当前列表顺序形成 A→B、B→C、C→A
- 其他情况下，只要剩余数量至少为 2（包括恰好 2），就选择欧氏距离最远的一对，设置双方互指并移除，然后继续处理余项
- 因此传送关系不保证都是双向边
- 总数只有一个传送点时，服务器调用该点的 `OnExtractionStarted(extractionCircle)`：`:52761-52765`；只有其 private prefab 配置 `enableExtractionMode == true` 时，才会保存撤离圈引用并同步进入提取模式

最短路算法必须使用**有向传送边**。

### 5.3 客户端可以读取配对关系

`TeleportDeadEndCircle` 已同步以下客户端所需成员：

```csharp
NetworkpairedCircle
NetworkisInExtractionMode
```

定义：`:78881-78905`。

Mirror 序列化：

- 全量：`:79587-79596`
- 增量：`:79598-79614`
- 客户端反序列化：`:79617-79644`

因此路径计算可以在客户端完成，不需要 Host 同步完整路线。

### 5.4 提取模式会动态改变传送目标

只有 private prefab 配置 `enableExtractionMode == true` 的传送点才订阅撤离开始/结束事件：`:78907-78916`。

提取开始时，仅对启用该配置的实例：

- 服务器保存非同步的撤离圈引用
- 服务器设置并同步 `NetworkisInExtractionMode = true`

位置：`:79282-79291`。纯客户端只依赖同步 bool，并自行把当前观察到的撤离圈作为 E。

实际传送目标：`:79130-79145`。

```text
普通模式：入口 → NetworkpairedCircle
提取模式：入口 → TeleportExtractionCircle
```

因此传送图需要在以下情况重建：

- 传送点配对引用首次完成同步
- `NetworkisInExtractionMode` 改变
- 撤离圈生成或销毁
- 地牢重新生成

### 5.5 传送耗时

源码默认值：

- 激活倒计时：3 秒，`:78728-78730`
- 传送等待：0.5 秒，`:78732-78733`
- 传送作用半径：5 米，`:78735-78739`

Idle 状态首次使用的时间成本约为 3.5 秒。

传送落点位于目标中心周围随机位置，而不是精确中心：`:79192-79199`。

玩家完成传送后必须基于实际位置重新规划。

---

## 6. 含传送点的最短路径图

## 6.1 图节点

```text
S              当前玩家位置
E              撤离圈位置
T_i.in         第 i 个传送点的入口 NavMesh 采样点
T_i.out        第 i 个传送目标附近的 NavMesh 采样点
```

首版必须在逻辑上区分 `.in` 与 `.out`。二者可使用同一 NavMesh 采样位置，但 node ID 不同：

- S、E 和所有 `T.in` 参与步行边计算
- 每个传送点添加不可渲染、零成本的 `LocalTransition(T.out → T.in)`
- `T.out` 不直接参与 NavMesh 步行计算

这样 `A.in → B.out → B.in → C.out` 的链式有向传送可达，同时不会把同点零长度过渡伪装成不满足 `corners.Length >= 2` 的 Walk 边。

## 6.2 步行边

对 S、E 和所有 `T.in` 候选节点执行：

```csharp
NavMesh.CalculatePath(a, b, -1, path)
```

只有同时满足以下条件才添加步行边：

```text
CalculatePath == true
path.status == PathComplete
corners.Length >= 2
```

边权：

```csharp
walkCost = SumDistance(path.corners);
```

步行边通常是双向的，但应分别计算两个方向，避免假设所有 NavMesh 连接对称。

候选节点数量通常很少，可以接受 O(N²) 的路径预计算。

## 6.3 传送边

对每个 `TeleportDeadEndCircle source`：

```text
普通模式：source.in → source.NetworkpairedCircle.out
提取模式：source.in → E
```

提取模式只依赖已同步的 `NetworkisInExtractionMode`。服务器使用的 private `activeExtractionCircle` 不是 SyncVar，纯客户端不得依赖它；客户端把当前已观察到的 `TeleportExtractionCircle` 地面采样点作为 E。

传送边必须是有向边。

不要自动添加反向边；只有目标传送点自身的 `NetworkpairedCircle` 指回 source 时，图中才自然存在反向传送边。

## 6.4 边权选择

已确认采用**最快到达**作为默认目标：

```text
步行边权 = NavMesh 路径长度 / 估算移动速度
传送边权 = 激活剩余时间 + teleportWaitTime
```

传送状态必须按“玩家预计抵达入口时该 sweep 是否仍可搭乘”评估：

```text
Idle:
    incrementalCost = countdownDuration + teleportWaitTime

Activating:
    currentSweepAt = countdownSecondsLeft + teleportWaitTime
    若 arrivalAtEntrance <= currentSweepAt：
        incrementalCost = currentSweepAt - arrivalAtEntrance
    否则：
        本次传送边不可用

Finished:
    不添加当前可用传送边

Unknown:
    不添加当前可用传送边
```

`Finished` 的同步状态无法区分约 0.5 秒 sweep 前窗口和 sweep 后约 2 秒保持期，不能固定按 `teleportWaitTime` 计算。源码默认从 Idle 新触发的成本约为 `3 + 0.5 = 3.5` 秒。为避免路线因极小成本差频繁切换，建议增加约 `0.25–0.5` 秒的路线切换滞后：只有新路线明显更快时才替换当前路线。

玩家实际移动速度会受状态影响。首版可使用配置化估算速度，后续再根据 Pawn 当前速度动态更新。

`Networkstate` 和 `NetworkcountdownSecondsLeft` 已同步。`Networkstate` getter 虽在元数据中为 public，但返回类型是 private 嵌套枚举，外部程序集不得通过普通 C# 属性表达式静态访问；必须缓存 getter 的 `MethodInfo`，调用 `getter.Invoke(source, null)` 取得 boxed enum，再用 `Convert.ToInt32` 读取状态码。

## 6.5 算法

使用 Dijkstra，而不是 BFS：

- 步行边长度不同
- 传送边成本不同
- 传送边是有向的

每条边保存：

```csharp
enum RouteEdgeType
{
    Walk,
    Teleport
}

sealed class RouteEdge
{
    public int From;
    public int To;
    public float Cost;
    public RouteEdgeType Type;
    public Vector3[] WalkCorners;
    public TeleportDeadEndCircle Teleporter;
}
```

Dijkstra 结果不是单个 `Vector3[]`，而是 `List<RouteEdge>`。

---

## 7. 路径光点渲染

### 7.1 不跨传送边画直线

错误效果：

```text
传送入口 ··················· 远端出口
```

这会让玩家误以为需要直接穿墙或跨越地图行走。

正确效果：

```text
玩家 ······· 传送入口  [传送中断]

传送出口 ······· 撤离点
```

每个连续步行段分别拥有自己的光点序列。

### 7.2 传送入口提示

在传送边入口使用区别于普通路径的视觉：

- 更大的光圈
- 不同颜色
- 向下箭头
- “使用传送点”文字或图标

不要把传送边本身渲染成连续光点。

### 7.3 只显示当前步行段

已确认采用：

- 当前路线不使用传送时，显示玩家到撤离点的当前连续步行段
- 当前路线使用传送时，只显示玩家到当前传送入口的光点
- 不预先生成传送出口后的远端光点
- 不在传送边两端之间连线
- 玩家实际传送后，从落点重新规划并生成下一步行段

这样不会让玩家在传送前看到远端房间的光点，也能显著减少光点对象数量。

### 7.4 光点必须完全客户端化

不要为每个光点生成网络对象。

每个客户端只为自己的本地玩家计算路线并渲染：

```text
网络只负责玩家、撤离圈和传送点原版状态
Mod 光点均为本地 GameObject/Object Pool
```

这样不同玩家可以得到不同的最短路线和显示状态。

---

## 8. 重算触发条件

以下情况立即或延迟一帧重新规划：

- 本地玩家首次获得 authority
- `guidanceUnlocked` 变为 true
- 本地玩家被加入 reached 集合：隐藏世界路径光点，但继续为导航箭头计算路线
- 本地玩家切换房间
- 本地玩家发生传送
- 传送点进入/退出 extraction mode
- 传送配对引用从 null 变为有效
- 撤离圈生成
- 当前路线偏离超过阈值
- NavMesh 路径失效

客户端传送完成信号不能使用 `Pawn.OnTeleportedEvent`。该事件只由带 `[Mirror.Server]` 的 `Pawn.NotifyTeleported()` 调用：`decompiled/YAPYAP/FullAssembly.cs:105912-105923`，纯客户端不会因原版 RPC 自动收到该 C# 事件。

首选方案是对客户端和 Host 本地都会执行的 private `Pawn.OnTeleport(Vector3)` 添加观察型 Harmony Postfix：

- `SvTeleport` 在服务器直接调用 `OnTeleport`：`:105934-105957`
- 非服务器客户端在 `UserCode_RpcTeleport__Vector3` 中调用 `OnTeleport`：`:106755-106761`
- Postfix 只在 `__instance.isLocalPlayer` 时标记立即重算
- 同时保留本地玩家位置跃迁检测作为回退，避免更新后调用路径变化
- 不 Patch Server-only `NotifyTeleported()`，也不修改 Mirror RPC 注册或序列化

传送后的推荐处理：

```text
1. 立即隐藏现有光点
2. 等待一帧或 0.1 秒
3. 优先从本地玩家实际 `transform.position` 重新 SamplePosition；仅在采样失败时把 LastGroundedPosition 作为辅助候选
4. 重建候选边并运行 Dijkstra
5. 重新显示路线
```

普通移动不需要每帧重算。建议：

- 每 0.5–1 秒检查偏离
- 与当前路线距离超过 2–4 米时重算
- 进入新房间时立即重算

---

## 8.1 导航箭头实现

原版 `UICompass` 不能直接满足该设计。其 `TargetObjectPivot` 只根据摄像机 yaw 旋转，目标水平向量没有参与左右方位角计算：`decompiled/YAPYAP/FullAssembly.cs:176242-176261`。

因此不替换整个 `UICompass` 区域，也不删除其大背景。应保留原 UI 根节点、外框、背景、定位、缩放和设置开关，只隐藏内部原方向元素，再在相同内层区域创建独立的 `Image/RectTransform` 箭头。

水平角计算：

```csharp
Vector3 cameraForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
Vector3 targetDirection = Vector3.ProjectOnPlane(nextWaypoint - camera.transform.position, Vector3.up).normalized;
float signedAngle = Vector3.SignedAngle(cameraForward, targetDirection, Vector3.up);
arrowRect.localEulerAngles = new Vector3(0f, 0f, -signedAngle);
```

不能直接指向最近的密集 corner，否则箭头会在拐角附近抖动。推荐：

- 跳过距离玩家不足约 2 米的点
- 沿当前步行路径选择前方约 4–8 米的 look-ahead 点
- 使用 `Mathf.SmoothDampAngle` 平滑箭头旋转
- 只有新路线预计快至少 `0.25–0.5` 秒时才切换路线

颜色优先级已确认：

```text
当前子目标是传送入口：紫色
否则 look-ahead 点高于玩家：蓝色
否则 look-ahead 点低于玩家：红色
否则：白色
```

建议高度死区：

```text
VerticalDeadZone = 1.0–1.5 米
```

高度颜色应根据下一有效路段的趋势计算，而不是根据最终撤离点总高度计算。否则玩家当前需要先下楼、但撤离点总体在上方时会得到错误颜色。

当当前最快路线要求使用传送点时：

- 当前步行段终点设为传送入口
- 从该段开始，整支导航箭头显示紫色
- 到达入口并完成传送后，由 `Pawn.OnTeleport(Vector3)` 的本地观察型 Postfix 标记重算，并以位置跃迁检测作为回退
- 下一帧从本地玩家实际 `transform.position` 重新规划，箭头恢复为新路段对应颜色

已经到达过的玩家仍继续导航。玩家停在撤离点中心附近时，水平目标向量可能接近零；为避免箭头随机旋转，建议在水平距离不足 `0.5` 米时保持最后一个有效角度，并将箭头设为白色。玩家离开后恢复实时导航。

路径计算失败时不要让箭头直接穿墙指向撤离点。推荐显示灰色或暂时隐藏，并按较短间隔重试。

原版指南针设置开关建议继续控制新的导航箭头：玩家关闭 Compass 时，同时隐藏替换后的箭头，而不是让原设置失效。

## 8.2 大背景保留与内部动画替换边界

已确认不更换指南针的大背景。实现边界如下：

| 保留 | 完整替换 |
|---|---|
| `UICompass` 的 UI 根节点和屏幕位置 | 原 Cardinal 方位刻度动画 |
| `compassActiveFrame` / `compassInactiveFrame` | 原 `TargetObjectPivot` 目标物动画 |
| 原背景、边框、遮罩和整体缩放 | 原 `ElevationIndicatorPivot` 升降动画 |
| `infoRect` 位置切换 | 原距离缩放目标物动画 |
| `enableCompassSetting` 设置链路 | 原方向 alignment 驱动的内部表现 |
| 原显示/隐藏生命周期 | 新的水平导航箭头旋转与颜色动画 |

不能直接禁用整个 `UICompass` 组件，因为它还负责：

- 订阅回合状态和撤离圈生成事件：`:176115-176143`
- 切换 active/inactive 大背景：`:176219-176240`
- 响应原版 Compass 设置：`:180390-180397`
- 管理 `RenderObjectToDiffuseNormal` 和 `UberLitUIGraphic`

推荐采用“保留控制器、替换内层视觉”的方式。

### 初始化流程

```text
UICompass.Awake/OnEnable 完成
→ 等待 _visualRefs 和 compassReferencesPrefab 实例化
→ 记录原内部 Renderer 的 enabled 状态
→ 只隐藏方向相关 Renderer
→ 在原内层显示区域创建 NavigationArrowLayer
→ 绑定原 CompassEnabled 和回合状态
```

内部引用来自 `UICompassReferences`：

- `CardinalDirectionsPivot`：`:176344`
- `ElevationIndicatorPivot`：`:176346`
- `TargetObjectPivot`：`:176348`
- `DistanceScaleTransform`：`:176350`
- `RenderTargetRoot`：`:176352`

首次运行应把 `RenderTargetRoot` 下的 Transform 路径和 Renderer 名称写入 Debug 日志。反编译源码只能证明引用结构，不能确认背景 Mesh 是否嵌套在某个 Pivot 下；因此不能粗暴关闭整个 `RenderTargetRoot`，也不应未经核验就关闭整个 Pivot GameObject。

推荐逐个关闭已经确认属于旧方向动画的 `Renderer.enabled`，而不是销毁对象。这样在 Mod 禁用或卸载时可以恢复原状态。

### 新箭头的承载方式

首版推荐使用独立 UI 层：

```text
UICompass Root
├── 原大背景和外框（保留）
├── 原内部渲染区域（旧方向 Renderer 隐藏）
└── NavigationArrowLayer
    └── Arrow Image + RectTransform
```

优点：

- 水平角度旋转简单
- 白、蓝、红、紫颜色切换稳定
- 不受原 3D Pivot rotation 干扰
- 不需要修改 `RenderObjectToDiffuseNormal` 的 Bounds 和材质缓存
- 可以精确控制箭头在背景内的尺寸、锚点和遮罩

如果后续希望箭头保留原指南针的 3D 光照质感，可以再把箭头改为 `RenderTargetRoot` 下的 MeshRenderer。此时添加或移除 Renderer 后必须调用：

```csharp
renderObjectToDiffuseNormal.RefreshTarget(renderTargetRoot);
```

因为该组件会缓存所有 Renderer、SubMesh 和材质：`decompiled/YAPYAP/FullAssembly.cs:154-209`。

### 更新顺序

不建议覆盖整个 `UICompass.LateUpdate`。更安全的方式是：

1. 保留原 `UICompass` 生命周期和背景状态切换
2. 隐藏旧方向 Renderer，使其旋转不再可见
3. 由唯一 Host 的 `LateUpdate` 更新新箭头
4. 新箭头位于独立层级，角度计算只读取摄像机、玩家和 RoutePlan，不读取原 Pivot 本帧结果
5. 只更新新箭头的角度、颜色、缩放和透明度

两个未声明执行顺序的 `MonoBehaviour.LateUpdate` 之间不能假定固定先后；因此设计必须对先后顺序不敏感。若未来确实需要严格后置，应使用明确且经验证的执行顺序或渲染前回调，不能依靠对象创建顺序。

### 状态表现

```text
Compass 设置关闭：由原 UICompass 维持 active/inactive frame 语义；新箭头隐藏
Compass 设置重新打开但 currentTarget 仍为空：新箭头仍隐藏
非 Dungeon/无本地玩家：沿用原 inactive 状态，新箭头隐藏
路线有效且原 Compass active：显示新箭头
路线计算中：箭头保持上一有效方向，降低透明度
路线无效：箭头灰色或隐藏，但背景不替换
到达撤离点附近：箭头继续显示，保持最后有效角度并变白
```

原 Compass 设置只控制原 Compass 生命周期与新箭头，不控制世界路径光点；世界光点继续由 `shouldShowTrail` 独立门控。Mod 不直接 `SetActive` `compassActiveFrame` 或 `compassInactiveFrame`。

### 清理与恢复

场景卸载、Mod 禁用或对象销毁时：

- 销毁 `NavigationArrowLayer`
- 恢复旧方向 Renderer 的原始 enabled 状态
- 取消事件订阅
- 清空私有字段和 Transform 缓存
- 不修改或销毁原背景、外框和原 Prefab 实例

## 9. 推荐模块划分

```text
HomeGuidancePlugin
├── ArrivalTracker
│   ├── 查找撤离圈
│   ├── 检测所有玩家到达
│   └── 管理 unlocked/reached 状态
│
├── TeleporterGraphProvider
│   ├── 枚举 TeleportDeadEndCircle
│   ├── 读取 NetworkpairedCircle
│   ├── 读取 extraction mode
│   └── 生成有向传送边
│
├── RoutePlanner
│   ├── NavMesh.SamplePosition
│   ├── 计算步行边
│   ├── Dijkstra
│   └── 输出 List<RouteEdge>
│
├── NavigationArrowRenderer
│   ├── 保留指南针大背景并替换内部方向动画
│   ├── 路点 look-ahead
│   ├── 水平 signed angle
│   ├── 白/蓝/红/紫状态色
│   └── 旋转与颜色平滑
│
├── TrailRenderer
│   ├── 光点对象池
│   ├── 分段路径动画
│   ├── 传送入口特殊标记
│   └── Hide/Clear
│
└── GuidanceController
    ├── 本地显示门控
    ├── 事件订阅
    ├── 路线重算节流
    └── 回合清理
```

---

## 10. 推荐开发顺序

### 阶段 1：导航箭头与显示门控原型

- 完整保留指南针区域的大背景、外框、布局和原设置开关
- 仅隐藏原本的方位刻度、目标物、升降指示等内部方向元素
- 在同一区域创建水平导航箭头
- 箭头实时指向缓存路线的下一有效路点
- 同层白色、向上蓝色、向下红色
- 当前子目标为传送入口时整支箭头紫色
- 客户端独立检测到达
- 第一人到达后显示一条直接 NavMesh 调试线
- 到达者隐藏世界路径光点，但导航箭头继续工作

### 阶段 2：普通路径光点

- 对象池生成流动光点
- 检查 `PathComplete`
- 实现偏离重算和回合清理

### 阶段 3：传送图

- 枚举传送点
- 读取有向配对
- 构建步行边和传送边
- 使用 Dijkstra
- 在传送边处断开显示

### 阶段 4：动态状态

- 处理 extraction mode
- 处理传送完成后的实际位置重算
- 添加传送入口特殊提示

### 阶段 5：联机严格模式，可选

- Host 权威到达检测
- 完整状态快照
- 版本和安装协商
- 晚加入恢复

---

## 11. 关键风险

| 风险 | 处理 |
|---|---|
| 原版没有到达事件 | 定时范围检测 |
| `circleRadius` 为 private 且属于原版规则 | 不读取、不修改；始终使用独立 `ArrivalRadius = 2.0m` 配置 |
| 远端位置同步延迟 | 使用略大的 ArrivalRadius，状态只增不减 |
| 晚加入不知道历史状态 | 首版接受限制，严格模式由 Host 发快照 |
| 传送点可能为有向三元环 | 只按每个入口真实目标添加有向边 |
| 提取模式改变传送目标 | 监听/轮询 `NetworkisInExtractionMode` 并重建图 |
| 传送落点随机 | 传送后从实际位置重新规划 |
| NavMesh Partial 路径 | 不添加该步行边，绝不当完整路线使用 |
| 路线跨传送点画直线 | 按 `RouteEdgeType` 分段渲染 |
| 自定义 Mirror 消息兼容 | 首版避免使用；严格模式要求 Host/Client 版本一致 |
| 每客户端路线不同 | 光点完全本地化，这是预期行为 |

---

## 12. 已确认的最终方案

```text
完整保留指南针区域大背景、外框、布局和设置开关
+ 仅替换内部方向显示动画
+ 在原内层区域显示水平导航箭头
+ 箭头指向最快路线的下一有效路点
+ 同层白色
+ 下一路点在上方时蓝色
+ 下一路点在下方时红色
+ 当前子目标为传送入口时紫色覆盖其他颜色
+ 各客户端独立观察玩家到达状态
+ 使用 2 米 Mod 到达半径，到达状态本轮只增不减
+ 导航箭头始终继续工作，包括已经到达过的玩家
+ 任意玩家到达后，未到达玩家额外显示当前连续步行段的世界路径光点
+ 路线要求传送时，光点只显示到当前传送入口
+ 客户端有向传送图
+ Dijkstra 最快到达路线
+ 传送边断开光点
+ 传送后从实际落点重新规划
```

该方案不需要同步光点，也不需要首版引入自定义 Mirror 网络协议。未安装 Mod 的玩家仍可联机，但只有安装者能看到导航箭头和路径光点。
