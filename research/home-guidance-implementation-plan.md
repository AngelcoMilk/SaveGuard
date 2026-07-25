# HomeGuidance 可执行实施计划

> 日期：2026-07-25  
> 适用对象：接手实现、审查、测试与打包的开发 Agent  
> 依据：`DEVELOPMENT.md`、`research/home-guidance-runtime-design.md`、`research/home-guidance-feasibility-validation-report.md` 与本地 YAPYAP 反编译源码  
> 交付目标：建立独立 `HomeGuidance/` 客户端 QoL Mod，并完成可重复构建、纯逻辑测试、Host/Client 运行时验收和 Thunderstore 打包

---

## 0. 执行摘要

首版采用**客户端本地化导航**：每个安装 Mod 的客户端独立观察原版已同步的玩家、撤离圈和传送点状态，独立计算本地玩家的最快撤离路线，并只在本地创建箭头与世界光点。不增加自定义 Mirror 消息，不改变服务端提取、传送、配对或同步逻辑，未安装 Mod 的玩家仍可加入同一房间。

必须同时满足以下不可变约束：

1. 完整保留原指南针区域的大背景、外框、位置、尺寸、遮罩、布局、active/inactive 状态与原 Compass 设置开关
2. 保留 `UICompass` 控制器，只替换其内部原方向动画；不得禁用整个 `UICompass`、`RenderTargetRoot` 或背景容器
3. 导航箭头对存活且未提取的本地玩家始终工作，包括该玩家已经到达过撤离点之后
4. 箭头指向最快路线当前步行段的下一有效路点
5. 箭头颜色：同层白色、向上蓝色、向下红色；当前子目标是传送入口时紫色覆盖其他颜色
6. 任意玩家首次进入独立 `ArrivalRadius = 2.0m` 后，本轮解锁世界光点
7. 光点只向尚未到达过撤离点的本地玩家显示；到达状态本轮只增不减
8. 不读取或修改原版 private `TeleportExtractionCircle.circleRadius` 来改变游戏规则
9. 路线图必须支持有向传送边，包括三个传送点形成的 A→B→C→A 环
10. 传送边不得画跨空间直线；只显示当前连续 Walk 段，传送后从玩家实际落点重算
11. 步行边只接受 `NavMeshPathStatus.PathComplete`
12. 首版不引入自定义 Mirror 消息、网络对象、SyncVar、Command 或 RPC
13. `Pawn.OnTeleportedEvent` 只在服务器侧 `NotifyTeleported()` 触发，纯客户端不得依赖它
14. 游戏构建或关键反射探测不匹配时整项功能 fail closed，不允许半启用

---

## 1. 范围

### 1.1 首版必须交付

- 独立 `HomeGuidance/HomeGuidance.csproj`
- 独立 `HomeGuidance.Tests/HomeGuidance.Tests.csproj`
- BepInEx 配置、日志、Harmony 安装/卸载、构建守卫
- 唯一持久运行时 Host 和明确的回合生命周期
- 本地到达扫描与同轮单调状态
- 普通 NavMesh 最快步行路线
- 有向传送图与 Dijkstra 最快时间路线
- 路线缓存、dirty flags、节流、偏离检测、路线切换滞后
- 保留原指南针容器并替换内部方向视觉
- 世界空间流动光点对象池
- 客户端传送完成信号与位置跃迁回退
- 纯逻辑自动测试
- Host、纯 Client、单人运行时测试矩阵
- Release 构建脚本、Thunderstore 配置、README、CHANGELOG、manifest/icon

### 1.2 首版明确不做

- 不修改原版提取半径、提取条件或 `SvExtractPlayers`
- 不 Patch `DungeonGameplay.PairTeleporters`
- 不修改 `TeleportDeadEndCircle.NetworkpairedCircle` 或 `NetworkisInExtractionMode`
- 不注册自定义 Mirror handler
- 不发送自定义 Command、RPC、TargetRpc 或 NetworkMessage
- 不同步路线、光点、已到达集合或 Mod 配置
- 不为光点添加 `NetworkIdentity`、Collider、Rigidbody 或交互组件
- 不禁用整个 `UICompass`、`RenderTargetRoot`、`compassActiveFrame` 或 `compassInactiveFrame`
- 不覆盖 `UICompass.LateUpdate`、`UpdateCompassRotation`、`SetTarget`
- 不直接移植 ExtractionTrail 源码；只按已核验的行为独立重写
- 不承诺晚加入客户端能恢复其加入前已经发生的“任意玩家到达”历史
- 不实现 Host 权威严格同步模式

### 1.3 首版已知限制

客户端本地观察存在以下可接受差异：

- 远端玩家位置受 Mirror 同步频率影响，不同客户端可能在相邻扫描周期解锁指引
- 晚加入客户端只能从加入后的可见位置开始观察，无法知道加入前谁曾到达又离开
- 随机传送落点在传送前只能使用目标中心附近的估计成本；传送后必须按实际位置纠正
- 当前玩家速度使用配置化估算值，最快时间是稳定近似而非服务端精确运动预测

以上限制必须写入 README，不能隐藏为实现细节。

---

## 2. 已核验的游戏事实与边界

### 2.1 撤离圈

`TeleportExtractionCircle`：`decompiled/YAPYAP/FullAssembly.cs:80315`

公共静态事件：

- `OnSpawned`：`:80388`，客户端 `OnStartClient` 触发于 `:80399-80402`
- `OnExtractionStarted`：`:80390`，正式提取开始时触发于 `:80555-80559`
- `OnExtractionEnded`：`:80392`，提取协程结束时触发于 `:80606-80609`

原版 private 字段：

- `circleRadius = 1f`：`:80323-80324`，仅原版服务端 `SvExtractPlayers` 使用于 `:80618-80627`
- `missingPlayerMinDist = 10f`：`:80355-80358`，只用于交互时“缺少玩家”显示于 `:80489-80499`

实现要求：Mod 到达判定始终使用自身配置 `ArrivalRadius`，默认 2.0m；不得写入上述 private 字段。

### 2.2 玩家与网络 ID

- 玩家集合：`GameManager.Instance.playersByPlayerId`
- 本地玩家：`Pawn.LocalInstance`
- 稳定同轮键：`pawn.netId`，类型 `uint`
- `Pawn.IsDead`、`Pawn.IsExtracted` 只作为显示/扫描门控，不能代替“曾到达”状态

### 2.3 传送点与同步成员

`TeleportDeadEndCircle`：`:78710`

可直接读取的 Weaver 属性：

- `NetworkpairedCircle`：`:78881-78892`
- `NetworkisInExtractionMode`：`:78894-78905`
- `NetworkcountdownSecondsLeft`：`:78868-78879`

`Networkstate` 的 getter 在元数据中为 public（`:78855-78866`），但返回类型是 private 嵌套枚举 `TeleportDeadEndCircle.State`；外部项目不得用普通 C# 属性表达式静态访问它。必须缓存 `AccessTools.PropertyGetter(typeof(TeleportDeadEndCircle), "Networkstate")` 返回的 `MethodInfo`，调用 `getter.Invoke(source, null)` 获得 boxed enum，再以 `Convert.ToInt32(value)` 映射：

```text
0 = Idle
1 = Activating
2 = Finished
```

相关序列化见 `:79587-79644`。

配对规则：

- 服务器生成后调用 `DungeonGameplay.PairTeleporters`：`:52751-52758`、`:52769-52814`
- 当当前剩余数量恰好为 3 时，按当前列表顺序形成 A→B、B→C、C→A
- 其他情况下，只要剩余数量至少为 2（包括恰好 2），就选当前集合欧氏距离最远的一对，设置双方互指并移除，然后继续处理余项
- 总数只有 1 时不进入配对方法，而是直接调用该传送点的 `OnExtractionStarted(extractionCircle)`，见 `:52761-52765`

提取模式：

- `enableExtractionMode` private 配置：`:78746`
- `isInExtractionMode` SyncVar：`:78820-78821`
- 服务器开始提取时把非同步 private 字段 `activeExtractionCircle` 设为撤离圈，并同步 `NetworkisInExtractionMode = true`：`:79282-79291`
- 结束后恢复普通模式：`:79294-79303`
- 实际服务器目标选择：`:79130-79145`
- 纯客户端不得读取或依赖 `activeExtractionCircle`；当 `NetworkisInExtractionMode == true` 时，图构建器把本地当前已观察到的 `TeleportExtractionCircle` 作为该传送边的目标 E

状态时间：

- `countdownDuration = 3f`：`:78730`
- `teleportWaitTime = 0.5f`：`:78733`
- `teleportRadius = 5f`：`:78736`
- 随机落点：`:79192-79199`

首版默认成本可从配置读取，不能依赖反射 private 数值才能工作。高级调试模式可反射读取实际 prefab 值并记录，但任一读取失败都回退到配置默认值。

### 2.4 客户端传送执行边界

- `Pawn.NotifyTeleported()` 带 `[Mirror.Server]`：`:105912-105923`
- `SvTeleport()` 在服务器调用 `NotifyTeleported()`、`OnTeleport()` 和 `RpcTeleport()`：`:105934-105957`
- 客户端 RPC 用户代码在非服务器客户端调用 `OnTeleport(targetPosition)`：`:106755-106761`
- `OnTeleport(Vector3)` 实际设置 transform：`:105971-106017`

因此：

- 不订阅 `Pawn.OnTeleportedEvent` 作为客户端重算来源
- 首选 Harmony Postfix Patch private `Pawn.OnTeleport(Vector3)`，仅当 `__instance.isLocalPlayer` 时标记 `TeleportObserved`
- Host 的本地 Pawn 也会在服务器直接执行 `OnTeleport`，同一 Patch 可覆盖 Host
- 保留位置跃迁检测作为回退，防止方法签名或调用路径变化
- 不 Patch `NotifyTeleported()`，不 Patch Mirror 生成的 `InvokeUserCode_*` 注册路径

### 2.5 指南针边界

`UICompass`：`:176000`

必须保留：

- `compassActiveFrame` / `compassInactiveFrame`：`:176003-176006`
- `infoRect` 布局：`:176059-176067`
- `compassReferencesPrefab` 与 `_visualRefs`：`:176069-176104`
- 原 `OnEnable`/`OnDisable` 事件链：`:176115-176128`
- `SetEnabled(bool)`：`:176209-176217`
- `SetTarget(Transform)` 的 active/inactive 背景与渲染生命周期：`:176219-176240`
- 原设置 `UISettings.CompassEnabled` 链

原 `TargetObjectPivot` 仅跟随摄像机 yaw，未使用目标水平向量计算左右方向：`:176242-176261`。新箭头必须独立计算 signed angle。

---

## 3. 目标项目结构

实现 Agent 必须按以下结构创建文件；若合并小文件，仍需保持同等职责边界和测试可替换性。

```text
HomeGuidance/
├── HomeGuidance.csproj
├── Plugin.cs
├── PluginInfo.cs
├── HomeGuidanceConfig.cs
├── BuildGuard.cs
├── SupportedGameBuilds.cs
├── Logging/
│   ├── GuidanceLog.cs
│   └── OneShotLog.cs
├── Patches/
│   ├── UICompassPatches.cs
│   └── PawnTeleportPatch.cs
├── Runtime/
│   ├── GuidanceRuntimeHost.cs
│   ├── GuidanceController.cs
│   ├── GuidanceLifecycle.cs
│   ├── GuidanceDirtyFlags.cs
│   ├── GuidanceSnapshot.cs
│   └── RoundGuidanceState.cs
├── Arrival/
│   ├── ArrivalTracker.cs
│   └── ArrivalScanResult.cs
├── Routing/
│   ├── RouteModels.cs
│   ├── RouteRequest.cs
│   ├── RoutePlan.cs
│   ├── RoutePlanner.cs
│   ├── DijkstraSolver.cs
│   ├── TeleportAvailabilityPolicy.cs
│   ├── NavMeshPathService.cs
│   ├── NavMeshPathCache.cs
│   ├── TeleporterGraphProvider.cs
│   ├── TeleporterSnapshot.cs
│   ├── TeleporterAccess.cs
│   ├── PathGeometry.cs
│   └── RouteSelectionPolicy.cs
├── Compass/
│   ├── CompassVisualAdapter.cs
│   ├── CompassHierarchyProbe.cs
│   ├── CompassHierarchyProfile.cs
│   ├── CompassOriginalVisualState.cs
│   ├── NavigationArrowLayer.cs
│   ├── ArrowDirectionSolver.cs
│   └── ArrowColorPolicy.cs
├── Trail/
│   ├── GuidanceTrailPresenter.cs
│   ├── TrailDotPool.cs
│   ├── TrailDotView.cs
│   ├── TrailSampling.cs
│   └── TeleportEntranceMarker.cs
├── Assets/
│   └── HomeGuidanceAssets.cs
├── README.md
├── CHANGELOG.md
├── manifest.json
├── icon.png
├── thunderstore.toml
└── Build-Package.ps1

HomeGuidance.Tests/
├── HomeGuidance.Tests.csproj
├── Program.cs
├── AssertEx.cs
├── DijkstraSolverTests.cs
├── TeleportAvailabilityPolicyTests.cs
├── RouteSelectionPolicyTests.cs
├── PathGeometryTests.cs
├── TrailSamplingTests.cs
├── ArrowDirectionSolverTests.cs
├── ArrowColorPolicyTests.cs
├── RoundGuidanceStateTests.cs
├── ArrivalPolicyTests.cs
└── TeleporterGraphPolicyTests.cs
```

### 3.1 项目文件要求

`HomeGuidance.csproj`：

- `TargetFramework = netstandard2.1`
- `LangVersion = 10.0`
- `Nullable = disable`
- `GenerateAssemblyInfo = false`
- `AppendTargetFrameworkToOutputPath = false`
- 显式排除工作区其他项目、`decompiled/**`、`research/**`、测试项目的源码
- 引用 `Assembly-CSharp.dll`、`Mirror.dll`、BepInEx、0Harmony、Unity Core、Physics、AI、UI、TextRendering 模块
- 所有游戏和 BepInEx 引用 `Private=false`
- 允许通过 `GameDir`、`ManagedDir`、`ProfileDir` MSBuild 属性覆盖默认路径

`HomeGuidance.Tests.csproj`：

- `TargetFramework = net8.0`
- `OutputType = Exe`
- 不引用 Unity 或 Assembly-CSharp
- Link 引入纯 C# 算法文件，或引用单独的无 Unity 核心项目
- 测试命令退出码非 0 表示失败

### 3.2 文件所有权总表

以下所有权不可重复；实现 Agent 可以合并文件，但不能合并状态所有者：

| 文件 | 输入/输出与依赖 | 唯一所有权 / 禁止事项 |
|---|---|---|
| `Patches/UICompassPatches.cs` | Unity/Harmony；把 Awake、SetEnabled、OnDestroy 转成 Controller 通知 | 不持有状态，不直接改 Renderer，不吞原方法异常 |
| `Patches/PawnTeleportPatch.cs` | Unity/Harmony；把本地 `OnTeleport` 转成观察通知 | 不改参数/返回值，不调用 Server-only API |
| `Runtime/GuidanceLifecycle.cs` | 读取 RoundActive、场景/Pawn 世代；输出 Begin/End/Generation transition | 只产生生命周期 transition，不持有视觉对象或 reached 集合 |
| `Runtime/GuidanceDirtyFlags.cs` | 纯 flags 定义 | 只有 `GuidanceController` 可消费并清零；生产者只能 OR 标记 |
| `Runtime/GuidanceSnapshot.cs` | 当前 Pawn、撤离圈、设置、签名的只读快照 | 不缓存 Unity 对象跨世代；不执行寻路 |
| `Arrival/ArrivalScanResult.cs` | 纯结果 DTO | 不修改 `RoundGuidanceState`；新增状态由 Controller 提交 |
| `Routing/RouteRequest.cs` | 规划输入 DTO；纯逻辑字段加运行时位置快照 | immutable；不得引用 Presenter |
| `Routing/RoutePlan.cs` | 规划结果、`CurrentEdgeIndex`、冻结 `DisplaySegment`、generation | 不持有可销毁 Unity 对象；只有 Controller 提交/推进 active plan |
| `Routing/RoutePlanner.cs` | request + snapshots → candidate plan | 不修改 active plan、dirty flags、箭头或 trail |
| `Routing/TeleporterSnapshot.cs` | 传送点稳定 ID、位置、pair、mode、state、countdown | 快照建立后 immutable；不得持有反射器 |
| `Routing/PathGeometry.cs` | 纯 polyline 清洗、投影、长度、拼接 | 无 Unity/BepInEx；链接进 Tests |
| `Compass/CompassHierarchyProfile.cs` | 目标构建冻结的相对路径、Renderer 类型/名称、箭头父节点与布局常量 | 发布构建只接受单一 profile；不得运行时猜测或二选一 |
| `Compass/ArrowDirectionSolver.cs` | camera/玩家/look-ahead 数值 → signed angle | 纯逻辑；不读取 UI Pivot |
| `Compass/ArrowColorPolicy.cs` | 子目标类型和高度 delta → 状态/颜色键 | 纯逻辑；紫色优先级固定 |
| `Trail/TrailSampling.cs` | corners + spacing → 累计弧长和采样点 | 纯逻辑；无对象池操作 |
| `Trail/TeleportEntranceMarker.cs` | 单一入口位置/显示参数 | 由 `GuidanceTrailPresenter` 创建、隐藏和销毁；不得自行寻路 |
| `Assets/HomeGuidanceAssets.cs` | 内嵌资源或程序化 Sprite/材质工厂 | 不联网下载；资源失败只降级对应视觉 |

唯一状态所有者：

```text
GuidanceLifecycle      → 生命周期 transition
RoundGuidanceState     → 当前 generation 的 reached/unlocked
GuidanceController     → dirty flags 消费、active RoutePlan 提交、传送去重状态
CompassVisualAdapter   → 原 Renderer 修改与恢复、单个箭头层
GuidanceTrailPresenter → trail dots 与入口 marker 的显示生命周期
TrailDotPool           → 点对象创建、复用与最终销毁
```

### 3.3 文件级职责与禁止事项

#### `Plugin.cs`

职责：

- BepInEx 插件入口
- Bind 配置
- 执行 Build Guard 和反射能力探测
- 创建唯一 `GuidanceRuntimeHost`
- 安装 Harmony Patch
- `OnDestroy` 时反向清理

禁止：

- 不承载每帧算法
- 不直接操作指南针 Renderer
- 不访问 Server-only API

#### `HomeGuidanceConfig.cs`

职责：集中声明、归一化和监听配置。

建议默认值：

| 配置 | 默认 | 限制/说明 |
|---|---:|---|
| `Enabled` | true | 总开关 |
| `ArrivalRadius` | 2.0m | 0.5–6.0，不写入原版字段 |
| `ArrivalScanInterval` | 0.15s | 0.10–0.50 |
| `RouteCheckInterval` | 0.50s | 0.20–2.00 |
| `RouteRetryInterval` | 0.75s | 0.25–3.00 |
| `RouteDeviationDistance` | 3.0m | 1.0–8.0 |
| `EstimatedWalkSpeed` | 4.5m/s | 1.0–10.0 |
| `TeleportCountdownSeconds` | 3.0s | 回退值 |
| `TeleportWaitSeconds` | 0.5s | 回退值 |
| `RouteSwitchGainSeconds` | 0.35s | 0–2.0 |
| `LookAheadDistance` | 6.0m | 2.0–12.0 |
| `SkipNearCornerDistance` | 2.0m | 0.25–5.0 |
| `VerticalEnterThreshold` | 1.25m | 0.25–5.0 |
| `VerticalExitThreshold` | 0.75m | 必须 ≤ Enter |
| `ArrowSmoothTime` | 0.12s | 0.02–0.50 |
| `TrailDotSpacing` | 1.5m | 0.5–5.0 |
| `TrailMaxDots` | 96 | 8–256 |
| `TrailGroundOffset` | 0.10m | 0–1.0 |
| `PositionJumpThreshold` | 8.0m | 4–30，传送回退检测 |
| `DebugLogging` | false | 详细图、层级和路径日志 |

配置变化处理：

- 视觉参数：立即更新
- 算法参数：设置 `ConfigChanged | RouteRequested`
- 到达半径变化：不清除已到达集合；后续扫描使用新值
- `HomeGuidanceConfig.Enabled=false`：隐藏 arrow、trail、marker，恢复本 Mod 抑制的旧方向 Renderer，清空临时路线，但不修改原 UI 状态或同轮 reached 集合
- `HomeGuidanceConfig.Enabled=true`：重新执行安全 attach，重新抑制冻结白名单中的旧方向 Renderer，并按当前回合状态请求规划；不得创建重复箭头层
- `UISettings.CompassEnabled` / `UICompass.SetEnabled` 只控制原 Compass 生命周期与新箭头，不参与 `ShouldShowTrail`，世界 trail/marker 不因原 Compass 设置关闭而隐藏

#### `BuildGuard.cs` / `SupportedGameBuilds.cs`

职责：

- 从已加载 `Assembly-CSharp` 程序集的真实 `Location` 读取 SHA-256
- 只接受 `SupportedGameBuilds` 中明确列出的哈希
- 探测关键类型、方法、字段、属性
- 输出一次完整兼容性报告

必须探测：

```text
UICompass.Awake
UICompass.SetEnabled(bool)
UICompass.OnDestroy
UICompass._visualRefs
UICompass.currentTarget
UICompass.compassActiveFrame
UICompass.compassInactiveFrame
UICompassReferences.RenderTargetRoot
UICompassReferences.CardinalDirectionsPivot
UICompassReferences.TargetObjectPivot
UICompassReferences.ElevationIndicatorPivot
Pawn.OnTeleport(Vector3)
TeleportDeadEndCircle.NetworkpairedCircle getter
TeleportDeadEndCircle.NetworkisInExtractionMode getter
TeleportDeadEndCircle.NetworkcountdownSecondsLeft getter
TeleportDeadEndCircle.Networkstate getter
```

首个支持构建：

```text
Assembly-CSharp SHA-256 = 7b6ef048e716ce4cf87bf5c6f190b3c11d39c50aa18a81467770f13ceed3c542
核验日期 = 2026-07-25
核验来源 = 当前本机 YAPYAP_Data/Managed/Assembly-CSharp.dll
```

实现时把该值写入 `SupportedGameBuilds`，但发布前仍须从实际加载程序集重新计算并核对；若成员探测与本计划不一致，不得仅凭哈希继续启用，也不得未经重新核验自行追加新哈希。

失败策略：

- 构建哈希未知：不 Patch、不创建 Host，日志给出实际哈希和支持列表
- 任一必需成员缺失：整项停用
- 可选 private prefab 参数读取失败：继续运行并使用配置回退值

#### `GuidanceRuntimeHost.cs`

职责：唯一 Unity `MonoBehaviour` 驱动入口。

生命周期：

- `Update`：低频到达扫描、状态签名轮询、位置跃迁检测、重算调度
- `LateUpdate`：更新新箭头和光点动画，但不得假定其天然晚于原 `UICompass.LateUpdate`；新箭头必须位于独立层级，且计算不能依赖原指南针在同帧刚写入的 Pivot 状态
- `OnDisable` / `OnDestroy`：调用 Controller 幂等清理

约束：

- 使用 `DontDestroyOnLoad`
- 创建前按固定对象名查重
- 不在每帧调用 `FindObjectsByType`
- 不持有跨场景失效对象而不做 Unity null 检查

#### `GuidanceController.cs`

职责：协调所有服务，不实现几何细节。

持有：

```csharp
RoundGuidanceState roundState;
ArrivalTracker arrivalTracker;
TeleporterGraphProvider teleporterProvider;
RoutePlanner routePlanner;
CompassVisualAdapter compassAdapter;
GuidanceTrailPresenter trailPresenter;
GuidanceDirtyFlags dirtyFlags;
RoutePlan activePlan;
GuidanceSnapshot lastSnapshot;
```

公开入口：

```csharp
void Initialize();
void Tick(float now, float deltaTime);
void LateTick(float now, float deltaTime);
void AttachCompass(UICompass compass);
void DetachCompass(UICompass compass);
void NotifyCompassEnabled(UICompass compass, bool enabled);
void NotifyLocalPawnTeleported(Pawn pawn); // Controller 读取 pawn.transform.position，Patch 参数仅可用于 Debug 对照
void MarkDirty(GuidanceDirtyFlags flags);
void Shutdown();
```

`Initialize()` 必须立即执行一次包含 inactive 对象的 `UICompass` 查找兜底。Compass attach 使用 `Detached → PendingVisualRefs → Attached`，失败回到 `RetryableFailure → Detached` 的状态机：

- 仅在没有 live `Attached` 或 `PendingVisualRefs` 实例时，每 1.0 秒查找一次；成功 attach 后稳态枚举次数必须为 0
- 多个候选时优先 `activeInHierarchy && enabled`；仍冲突则记录一次错误并不修改任何 Renderer
- `AttachCompass` 按 `instanceID` 幂等；同一实例不能创建两个 adapter 或箭头层
- `_visualRefs` 为空时每个 `LateTick` 重试，最多 60 帧；超时后进入 `RetryableFailure`，清除 pending 引用并恢复 1.0 秒查找，不得永久失败
- 当前引用成为 Unity fake-null 或收到 `OnDestroy` Prefix 时立即 `DetachCompass`，然后恢复低频查找

这样即使 Mod 热加载、Compass 初始 inactive、UI 对象早于 Harmony Patch 创建或之后重建，也不会永久错过 attach。

#### `RoundGuidanceState.cs`

纯状态对象：

```csharp
sealed class RoundGuidanceState
{
    public bool RoundActive { get; private set; }
    public int CurrentRoundToken { get; private set; }
    public bool GuidanceUnlocked { get; private set; }
    public IReadOnlyCollection<uint> ReachedPlayerNetIds { get; }

    public bool BeginRound(int roundToken);
    public bool MarkReached(uint netId, int roundToken);
    public bool HasReached(uint netId);
    public bool EndRound();
}
```

不变量与边沿语义：

- `RoundActive && CurrentRoundToken == roundToken` 时，`BeginRound` 严格 no-op，不清集合、不递增 generation
- 新 token 才清空旧集合并开始新 generation；插件首次在回合中途观察到 `RoundActive=true` 时也只创建一个本地 token
- `MarkReached` 仅在 `roundToken == CurrentRoundToken && RoundActive` 时接受；同一 generation 只 Add，旧 token 的迟到结果必须拒绝
- 同一 `roundToken` 内 `GuidanceUnlocked` 只能 false→true
- 玩家离圈、断线、死亡、配置变化均不 Remove
- `EndRound` 幂等；只有第一次 Active→Inactive 清状态，重复 false/null Tick 不继续递增 token

`roundToken` 首选由“RoundActive false→true 的本地世代计数器”生成，不依赖不稳定的场景实例 ID。`GuidanceLifecycle` 必须订阅 `SceneManager.activeSceneChanged`：BeginRound 时记录 `roundSceneHandle`；当旧 active scene handle 等于该值且 active scene 发生切换时，视为 confirmed scene teardown，幂等调用 EndRound。`GameManager.Instance` 短暂为 null 本身只暂停，不结束 generation。`Plugin.Shutdown` 必须取消 `activeSceneChanged` 订阅；新场景再次观察到 `RoundActive=true` 时创建新 token。

#### `ArrivalTracker.cs`

职责：低频扫描所有可见玩家，产生新增到达者。

输入：撤离圈位置、玩家快照、`ArrivalRadius`。

输出：

```csharp
sealed class ArrivalScanResult
{
    public int RoundToken;
    public IReadOnlyList<uint> CandidateNetIds;
    public bool AnyCandidate => CandidateNetIds.Count > 0;
}
```

禁止：

- 不修改 Pawn 或撤离圈
- 不以 `IsExtracted` 作为到达条件
- 不从 reached 集合删除玩家

#### `TeleporterAccess.cs`

职责：集中处理所有反射/Weaver 访问，其他模块不得直接反射。

接口：

```csharp
bool TryReadPaired(TeleportDeadEndCircle source, out TeleportDeadEndCircle target);
bool TryReadExtractionMode(TeleportDeadEndCircle source, out bool value);
bool TryReadCountdown(TeleportDeadEndCircle source, out int seconds);
bool TryReadStateCode(TeleportDeadEndCircle source, out int stateCode);
bool TryReadPrefabTiming(TeleportDeadEndCircle source, out float countdown, out float wait);
```

规则：

- 配对、提取模式、倒计时优先调用公开 getter
- 状态 getter通过 cached `MethodInfo.Invoke`
- private timing 只作可选增强
- 每类失败只记一次警告，避免刷屏

#### `TeleporterGraphProvider.cs`

职责：

- 低频枚举活动 `TeleportDeadEndCircle`
- 构建稳定 `TeleporterSnapshot`
- 计算拓扑/状态签名
- 只在签名变化时标记图 dirty

签名至少包含：

```text
source netId 或 instanceID
source transform position quantized
paired target netId/instanceID
NetworkisInExtractionMode
stateCode
countdownSecondsLeft
目标对象是否有效
撤离圈 instanceID
```

拓扑签名与动态成本签名分开：

- 配对、模式、对象增删、位置变化 → `GraphTopologyChanged`
- state/countdown 改变 → `TeleportCostChanged`

#### `NavMeshPathService.cs`

职责：Unity NavMesh 适配。

接口建议：

```csharp
bool TrySample(Vector3 visualPoint, float radius, out Vector3 groundPoint);
bool TryCalculateCompletePath(Vector3 from, Vector3 to, out Vector3[] corners, out float length);
```

规则：

- 先 `NavMesh.SamplePosition`
- `CalculatePath` 返回 true、`PathComplete`、清洗后至少两个点才成功
- 不接受 Partial
- 不把抬高后的视觉点作为 NavMesh 输入
- 路径 corner 复制出 `NavMeshPath`，避免复用对象被覆盖

#### `NavMeshPathCache.cs`

固定节点缓存键：

```text
roundToken
fromNodeStableId
toNodeStableId
fromSampleQuantized(0.25m)
toSampleQuantized(0.25m)
navAreaMask
```

规则：

- 传送点与撤离圈之间的固定 Walk 边可缓存到本轮结束
- 玩家起点 S 的边不长期缓存；位置移动超过 1m 或偏离时重算
- 拓扑变化清空相关固定边
- 连续失败设置短期 negative cache，默认 0.75s，避免每帧重试

#### `RouteModels.cs`

纯逻辑模型不得依赖 Unity 对象；Unity 引用通过外部字典映射。

```csharp
enum RouteNodeKind { Start, Extraction, TeleporterIn, TeleporterOut }
enum RouteEdgeType { Walk, Teleport, LocalTransition }

readonly struct RouteNode
{
    public int Id;
    public RouteNodeKind Kind;
    public Float3 Position;
    public int StableObjectId;
}

readonly struct TeleportTimingSnapshot
{
    public int StateCode;
    public int CountdownSecondsLeft;
    public float CountdownDuration;
    public float TeleportWait;
}

sealed class RouteEdge
{
    public int FromId;
    public int ToId;
    public RouteEdgeType Type;
    public float WalkCostSeconds; // 仅 Walk 使用；LocalTransition 固定为 0
    public Float3[] WalkCorners;
    public int TeleporterStableId;
    public TeleportTimingSnapshot TeleportTiming; // 仅 Teleport 使用
}
```

`RouteEdge` 不得持有 `TeleportDeadEndCircle`、`TeleporterSnapshot` 或其他 Unity 对象。若运行时模型使用 `Vector3`，必须另外建立纯 C# DTO 供测试，不允许测试项目引用 Unity。

#### `TeleportAvailabilityPolicy.cs` / `DijkstraSolver.cs`

二者均为纯 C#、无 Unity、无 BepInEx、无 Harmony，并链接进入测试项目。

接口：

```csharp
TeleportEvaluation Evaluate(TeleportTimingSnapshot timing, float arrivalAtEntrance);

RouteSolution Solve(
    IReadOnlyList<RouteNode> nodes,
    IReadOnlyList<RouteEdge> edges,
    int startId,
    int goalId);
```

`DijkstraSolver` 对 Teleport edge 调用 `TeleportAvailabilityPolicy.Evaluate(edge.TeleportTiming, dist[u])`；Walk edge 使用 `WalkCostSeconds`；`LocalTransition` 固定成本为 0。输出包含：是否可达、总预计秒数、按顺序排列的 edge 索引。`LocalTransition` 不计入传送次数 tie-break，也不得进入任何视觉分段。

#### `RouteSelectionPolicy.cs`

职责：应用路线滞后。

```csharp
bool ShouldReplace(RoutePlan current, RoutePlan candidate, float requiredGainSeconds)
```

规则：

- 当前无效、目标变化、当前首边失效：立即换
- candidate 与 current 拓扑相同：允许刷新 corners/cost
- 拓扑不同：仅当 `candidate.TotalCost <= current.TotalCost - gain`
- 当前路线偏离或传送完成：忽略滞后，立即换

#### `CompassHierarchyProbe.cs` / `CompassHierarchyProfile.cs`

`CompassHierarchyProbe` 只负责目标构建的发现阶段，不在发布运行时猜测：

- 输出 `_visualRefs.gameObject` 根路径
- 输出 `RenderTargetRoot` 下每个 Transform 相对路径
- 输出 Renderer 类型、名称、enabled、activeInHierarchy
- 标记 Renderer 是否位于 Cardinal/Target/Elevation pivot 下
- 输出所有候选 `RectTransform` 的 anchor、pivot、sizeDelta、anchoredPosition、siblingIndex 和所属 Mask

发现结果必须被人工核对一次并冻结到唯一 `CompassHierarchyProfile`。Profile 至少包含：

```text
GameBuildHash
DirectionRendererAllowList[] = relativePath + rendererType + rendererName
PreservedRendererDenyList[]  = relativePath
ArrowParentRelativePath
ArrowSiblingIndexRule
AnchorMin / AnchorMax / Pivot / SizeDelta / AnchoredPosition
MaskOwnerRelativePath
```

发布运行时只按此 profile 精确匹配。任一路径缺失、重复匹配、类型/名称不符或构建哈希不符时，adapter fail closed：不改任何 Renderer、不创建箭头层、保留原 Compass。不得在“相对路径”与“pivot+名称”间留给实现者二选一。

#### `CompassOriginalVisualState.cs`

记录每个被 Mod 改动的 Renderer：

```csharp
Renderer renderer;
bool originalEnabled;
int instanceId;
```

只记录并修改 `CompassHierarchyProfile.DirectionRendererAllowList` 精确命中的 Renderer。`PreservedRendererDenyList` 优先级更高；任一冲突都使整个 Compass adapter fail closed。禁止通过遍历整个 Pivot 子树扩大匹配，禁止关闭整个 Pivot GameObject，禁止修改背景/外框或共享父级 Renderer。

#### `CompassVisualAdapter.cs`

职责：

- attach/detach `UICompass`
- 等待 `_visualRefs` 已由原 `Awake` 创建
- 探测并保存原 Renderer 状态
- 创建/销毁 `NavigationArrowLayer`
- 同步原 Compass enabled、active/inactive、对象销毁

箭头门控：

```text
HomeGuidanceConfig.Enabled
AND UICompass 已安全 attach
AND UICompass._enabled / 最近一次 SetEnabled 门控为 true
AND UICompass.currentTarget != null
AND compassActiveFrame.activeSelf == true
AND GameManager.RoundActive
AND Pawn.LocalInstance 存在、存活、未提取
AND RoutePlan 有有效当前 DisplaySegment/SubTarget
```

`UICompass.SetEnabled(true)` 只表示允许原 Compass 尝试寻找目标，不等于当前已经 active；Postfix 不得无条件显示新箭头。`SetEnabled(false)` 必须立即隐藏新箭头；再次 true 只允许后续在完整门控满足时显示。

不能自行 `SetActive` `compassActiveFrame` 或 `compassInactiveFrame`。背景、`infoRect`、`_uiGraphic` 与 `RenderObjectToDiffuseNormal` 状态全部由原 `UICompass.SetTarget` 管理。Mod 启用期间，旧方向 Renderer 始终保持被抑制；切换原 Compass 设置不得临时恢复旧方向动画。原 Compass 设置不影响世界 trail/marker。

#### `NavigationArrowLayer.cs`

首版使用 UI `Image + RectTransform`。父节点、sibling 规则、anchor、pivot、sizeDelta、anchoredPosition 与 Mask 必须全部来自当前构建冻结的 `CompassHierarchyProfile`；禁止在发布运行时凭名称猜父节点或挂到整个 `RenderTargetRoot`。创建前按固定对象名查重，detach 时只销毁本 Mod 创建的该实例。

箭头纹理优先内嵌资源或运行时程序化 Sprite；不得依赖网络下载。箭头角度计算不读取原 `TargetObjectPivot` 本帧旋转，因此不依赖 Host 与 `UICompass.LateUpdate` 的执行先后。

更新：

- 角度用 `Mathf.SmoothDampAngle`
- 颜色用短时 `Color.Lerp`
- 路线计算中保留上一有效角，降低 alpha
- 无路线时隐藏或灰色，不得直指撤离点穿墙
- 水平距离小于 0.5m 时保持最后角；若当前子目标是传送入口则保持紫色，否则变白

#### `GuidanceTrailPresenter.cs`

职责：

- 只接收当前连续 Walk 段 corners
- 清洗、采样、申请对象池、更新动画
- 传送入口 marker 独立于普通点
- 门控变化时立即隐藏

禁止：

- 不接收整条跨传送路线后自行连线
- 不创建网络对象
- 不每次重算 Destroy/Instantiate 全部点

#### `TrailDotPool.cs` / `TrailDotView.cs`

要求：

- 预热可配置，按需增长但不超过 `TrailMaxDots`
- 使用共享材质或 `MaterialPropertyBlock`
- `Renderer.shadowCastingMode = Off`
- `receiveShadows = false`
- 无 Collider、Rigidbody、NetworkIdentity、AudioSource
- `HideAll` 只 SetActive(false)，回合结束可保留池，场景/Mod 销毁再 Destroy

---

## 4. 核心数据与状态契约

### 4.1 Dirty Flags

```csharp
[Flags]
enum GuidanceDirtyFlags
{
    None                    = 0,
    RoundChanged            = 1 << 0,
    LocalPawnChanged        = 1 << 1,
    ExtractionChanged       = 1 << 2,
    ArrivalStateChanged     = 1 << 3,
    GraphTopologyChanged    = 1 << 4,
    TeleportCostChanged     = 1 << 5,
    PlayerTeleported        = 1 << 6,
    RouteDeviation          = 1 << 7,
    RouteInvalid            = 1 << 8,
    CompassChanged          = 1 << 9,
    ConfigChanged           = 1 << 10,
    RouteRequested          = 1 << 11
}
```

处理优先级：

1. `RoundChanged`：先清理或开始新轮
2. `LocalPawnChanged` / `ExtractionChanged`
3. `PlayerTeleported`：立即隐藏旧 trail，清起点缓存，延迟一帧重算
4. `GraphTopologyChanged`
5. `RouteInvalid` / `RouteDeviation`
6. `TeleportCostChanged`
7. `ArrivalStateChanged`：只改变 trail 门控；箭头仍规划
8. `CompassChanged` / `ConfigChanged`

一次 Tick 可合并多个 flags，只执行一次路线规划。

### 4.2 `GuidanceSnapshot`

每个状态检查周期生成轻量快照：

```csharp
sealed class GuidanceSnapshot
{
    public bool RoundActive;
    public int LocalPawnInstanceId;
    public uint LocalPawnNetId;
    public bool LocalPawnAlive;
    public bool LocalPawnExtracted;
    public int ExtractionInstanceId;
    public Vector3 ExtractionPosition;
    public int CompassInstanceId;
    public bool CompassSettingEnabled;
    public int TeleporterTopologyHash;
    public int TeleporterCostHash;
}
```

比较前后快照生成 flags，避免将大量事件和 Patch 散布到游戏各类。

### 4.3 `RouteRequest`

```csharp
sealed class RouteRequest
{
    public int RoundToken;
    public Vector3 ActualPlayerPosition;
    public Vector3 SampledStart;
    public Vector3 ExtractionVisualPosition;
    public Vector3 SampledExtraction;
    public IReadOnlyList<TeleporterSnapshot> Teleporters;
    public float EstimatedWalkSpeed;
    public float Now;
    public RouteReplanReason Reason;
}
```

### 4.4 `RoutePlan`

```csharp
sealed class RoutePlan
{
    public bool IsValid;
    public float TotalCostSeconds;
    public List<RouteEdgeRuntime> Edges;
    public int CurrentEdgeIndex;
    public DisplayWalkSegment DisplaySegment;
    public int TopologySignature;
    public int RoundToken;
    public int TeleportGeneration;
    public Vector3 PlannedFromPosition;
    public float PlannedAtTime;
    public RouteReplanReason Reason;
}
```

`DisplayWalkSegment` 在 candidate plan 提交前冻结，包含：拼接清洗后的 corners、当前子目标、是否以 Teleport 入口结束、入口稳定 ID。构建规则：

1. 从 `CurrentEdgeIndex` 开始
2. 先跳过不可渲染 `LocalTransition`；若首个可见未完成边是 Teleport，则普通 corners 为空并只显示入口 marker
3. 若首个可见边是 Walk，则拼接所有连续 Walk edge，并删除接缝重复点
4. 遇到第一条 Teleport edge 或路线结束时停止
5. 只有停止边为 Teleport 时，`CurrentSubTargetIsTeleportEntrance=true`
6. 传送后旧 plan 的 `TeleportGeneration` 失效，不能把其远端 Walk edge 推进为当前段；必须从实际落点产生新 plan

必要辅助属性：`CurrentSubTarget`、`CurrentSubTargetIsTeleportEntrance`、`CurrentWalkCorners`、`NextTeleportEdge`。

若起点已经位于传送入口采样半径内，可以创建零长度/极短 Walk 过渡；展示层清洗后直接显示入口 marker，不能把 Teleport edge 当作可画路径。

---

## 5. 生命周期与更新流程

### 5.1 插件加载

```text
Plugin.Awake
→ Bind config
→ 计算 Assembly-CSharp SHA-256
→ 探测必需成员
→ 若失败：记录 DisabledReason 并返回
→ 创建 DontDestroyOnLoad GuidanceRuntimeHost
→ controller.Initialize()
→ Harmony.PatchAll()
→ 记录“client-local mode / no custom Mirror messages”
```

Patch 安装应放在 Host/Controller 已可接收回调之后。若 Patch 安装抛异常：

- `Harmony.UnpatchSelf()`
- `controller.Shutdown()`
- 销毁 Host
- 整项停用

### 5.2 指南针 attach

```text
UICompass.Awake 原方法完成
→ Harmony Postfix
→ GuidanceRuntimeHost.TryGetController
→ controller.AttachCompass(__instance)（按 instanceID 幂等）
→ 进入 PendingVisualRefs
→ 每个 LateTick 检查 _visualRefs，最多 60 帧
→ 成功后用当前构建 CompassHierarchyProfile 做精确匹配
→ 所有 allow/deny/父节点/Mask 断言全部通过
→ 一次性保存并隐藏白名单旧方向 Renderer
→ 在 profile 指定父节点创建唯一 NavigationArrowLayer
→ 状态变为 Attached，停止对象枚举
```

兜底状态机：

- `Initialize` 立即执行一次包含 inactive 对象的查找
- 无 live Attached/Pending 实例时每 1.0 秒查找一次
- `_visualRefs` 60 帧仍为空：清 pending，进入 RetryableFailure，再恢复 1.0 秒查找
- Unity fake-null 或 `OnDestroy` Prefix：只 detach 该实例并恢复查找
- 多个同优先级候选：不修改 Renderer，记录一次错误

如果 profile 无法精确匹配：

- 不隐藏任何 Renderer
- 不创建替代层
- 将该 Compass 适配标记失败
- 路由与 trail 可以继续，但导航箭头功能停用
- 最终包必须附已冻结 profile、层级日志和前后截图；缺任一项均为发布阻断

### 5.3 回合开始

检测确认的 `RoundActive false→true`；或插件首次加载时直接观察到 `RoundActive=true`：

1. 仅为该边沿创建一次 `roundToken++`
2. `RoundGuidanceState.BeginRound(roundToken)`；同 token 重复调用必须 no-op
3. 清固定图、路径、传送签名、negative cache 和上轮传送 generation
4. 建立本地 Pawn/位置跃迁 baseline，不把首个样本报告为传送
5. 查找撤离圈和传送点
6. 箭头允许规划，但 trail 保持隐藏，直到 `GuidanceUnlocked`
7. 输出一次回合摘要日志

`GameManager.Instance` 短暂为 null 只暂停 Tick，不立即 `EndRound`；重复 false/null 观察不得制造多个 token。

### 5.4 回合进行

`Update` 使用独立计时器：

- 每 `ArrivalScanInterval`：到达扫描
- 每 `RouteCheckInterval`：快照/偏离/传送签名检查
- dirty 或重试时间到达：执行最多一次规划
- 每帧：位置跃迁回退只做一次距离平方比较

`LateUpdate`：

- 解析当前 Walk 段 look-ahead
- 更新箭头
- 更新已有 trail 点动画 phase
- 不在 LateUpdate 重新枚举对象或计算完整 NavMesh 图

### 5.5 到达状态变化

任一新增到达者：

- `GuidanceUnlocked = true`
- Add 到 reached 集合
- 标记 `ArrivalStateChanged`
- 本地玩家若刚到达：立即 `trailPresenter.Hide()`
- 箭头不停止、不清路线

### 5.6 传送

首选信号：`Pawn.OnTeleport(Vector3)` Postfix。

Controller 持有去重状态：

```csharp
int lastTeleportObservedFrame;
int lastTeleportPawnInstanceId;
Vector3 lastTeleportActualPosition;
bool previousPositionValid;
int suppressJumpDetectionUntilFrame;
int teleportGeneration;
```

有效通知处理：

```text
若 !__instance.isLocalPlayer、非本轮、Pawn 死亡/已提取：忽略
actual = __instance.transform.position
`duplicate = samePawn && (currentFrame == lastTeleportObservedFrame || (currentFrame <= lastTeleportObservedFrame + 1 && Distance(actual, lastTeleportActualPosition) < 0.25m))`；仅 duplicate 时合并，超过一帧后即使落点相近也视为新的真实传送
否则：
    teleportGeneration++（每次真实传送只增加一次）
    立即隐藏 trail 与入口 marker
    activePlan 标记 stale
    清所有 start-node 动态边缓存
    previousPosition = actual；previousPositionValid = true
    suppressJumpDetectionUntilFrame = currentFrame + 1
    设置 PlayerTeleported | RouteRequested
    notBeforeFrame = currentFrame + 1
```

位置跃迁回退：

```text
roundToken、本地 Pawn instanceID、authority 或场景 generation 变化：
    previousPositionValid = false，首个样本只建立 baseline

仅当 previousPositionValid
AND currentFrame > suppressJumpDetectionUntilFrame
AND RoundActive、本地 Pawn 存活且未提取
AND Distance(previousPosition, actual) > PositionJumpThreshold
才按传送处理
```

当前源码中，`OnTeleport(Vector3)` 参数已包含随机落点和服务器碰撞抬高修正，并直接写入 Pawn transform。仍延迟到下一帧从实际 `transform.position` 建立 `RouteRequest.SampledStart`，理由是防御 NetworkTransform、物理或其他组件的后续调整；最终规划起点始终以实际 transform 为准。

### 5.7 分层清理生命周期

#### `EndRound`（每轮执行，幂等）

1. 停止本轮规划并隐藏 arrow/trail/marker
2. `RoundGuidanceState.EndRound()`；重复调用 no-op
3. 清 `activePlan`、图签名、路径缓存、negative cache 和传送 generation
4. 重置位置跃迁 baseline
5. **保留** Host、Controller、静态事件订阅、Compass attach、Renderer 抑制状态和对象池

#### `DetachCompass` / `UICompass.OnDestroy` Prefix（每个 UI 实例）

1. 只恢复该 `UICompass` 实例中本 Mod 修改过的 Renderer
2. 销毁该实例的 `NavigationArrowLayer`
3. 清 Compass 引用并恢复 1.0 秒低频 attach 查找
4. 不销毁 Host，不取消全局生命周期订阅，不销毁 trail pool

#### `Plugin.Shutdown`（Mod/插件销毁）

1. 停止 Tick 和新规划
2. 取消所有静态事件订阅，包括 `SceneManager.activeSceneChanged`
3. Detach 当前 Compass 并恢复 Renderer
4. 隐藏并销毁 trail pool/marker
5. `Harmony.UnpatchSelf()`
6. 销毁 Host

不得销毁 `_visualRefs.gameObject`，该对象属于原 `UICompass.OnDestroy`。回合结束绝不能调用 `Shutdown`。

---

## 6. 到达扫描算法

### 6.1 伪代码

```text
ArrivalTracker.Scan(now, capturedRoundToken, reachedSnapshot):
    if now < nextArrivalScanTime:
        return EmptyCandidates(capturedRoundToken)
    nextArrivalScanTime = now + ArrivalScanInterval

    gm = GameManager.Instance
    extraction = currentExtraction
    if gm == null or !gm.RoundActive or extraction == null:
        return EmptyCandidates(capturedRoundToken)

    radiusSq = ArrivalRadius * ArrivalRadius
    candidates = []

    for pawn in gm.playersByPlayerId.Values:
        if pawn == null or pawn.netId == 0:
            continue
        if pawn.IsDead or pawn.IsExtracted:
            continue
        if reachedSnapshot.Contains(pawn.netId):
            continue

        delta = pawn.transform.position - extraction.transform.position
        if delta.sqrMagnitude <= radiusSq:
            candidates.Add(pawn.netId)

    return ArrivalScanResult(capturedRoundToken, candidates)

GuidanceController.CommitArrivalScan(scan):
    newlyReached = []
    for netId in scan.CandidateNetIds:
        if roundState.MarkReached(netId, scan.RoundToken):
            newlyReached.Add(netId)

    if newlyReached.Count > 0:
        MarkDirty(ArrivalStateChanged)

    return newlyReached
```

### 6.2 边界规则

- 三维球形距离与原版逻辑一致；首版不改成仅 XZ
- 第一次扫描时玩家已经在半径内，应立即计为到达
- 玩家死亡后不新增，但死亡前已到达记录保留
- 玩家被提取后不新增，但已到达记录保留
- 玩家断线不删除 netId
- 新一轮 netId 复用不影响，因为集合已清空
- 撤离圈暂时缺失时不结束回合状态，只暂停扫描

### 6.3 trail 门控

```text
ShouldShowTrail(local):
    return ModEnabled
       && RoundActive
       && GuidanceUnlocked
       && local != null
       && !local.IsDead
       && !local.IsExtracted
       && !Reached.Contains(local.netId)
       && (ActivePlan.DisplaySegment has drawable Walk corners
           OR ActivePlan.DisplaySegment is MarkerOnly teleport entrance)
```

箭头门控不得包含 `GuidanceUnlocked` 或 `Reached.Contains`。

---

## 7. 有向传送图与最快路线

### 7.1 节点模型

首版固定采用 `.in/.out` 双逻辑节点，不允许等价替代：

```text
S         本次规划的玩家起点
E         撤离圈地面采样点
T_i.in    第 i 个传送入口地面采样点
T_i.out   从其他传送点抵达第 i 个传送点后的逻辑出口点
```

对每个有效 teleporter `t`，分别创建 `T_t.in` 和 `T_t.out`。二者使用不同 node ID / `RouteNodeKind`，物理位置都来自 `t.sourcePosition` 的同一 NavMesh 采样结果。

固定边语义：

- S、E、所有 `T.in` 参与 Walk 路径计算；`T.out` 不直接参与 NavMesh Walk
- 普通模式 Teleport edge：`T_source.in → T_paired.out`
- 提取模式 Teleport edge：`T_source.in → E`
- 每个有效传送点固定添加不可渲染、零成本的单向 `LocalTransition(T_i.out → T_i.in)`，表示抵达该物理传送点后进入其本地步行/再次传送状态
- 不添加 `T_i.in → T_i.out`，不自动添加反向 Teleport edge

`LocalTransition` 不是 Walk，不要求 `NavMeshPathStatus.PathComplete`，不携带 corners，也不参与箭头或 trail。它只解决 `A.in→B.out→B.in→C.out` 链式有向传送；实际传送发生后仍必须让旧 plan 作废并从玩家实际落点重算。

### 7.2 地面采样

```text
SampleRoutePoint(visualPosition):
    try NavMesh.SamplePosition(visualPosition, radius=2m)
    if fail:
        try offsets: +up*0.5, +up*1.0, horizontal small ring
    if all fail:
        node invalid
```

采样输出用于寻路；视觉 marker 可以再加 `TrailGroundOffset`。不得反过来用抬高视觉点计算 NavMesh。

### 7.3 图构建伪代码

```text
BuildGraph(request):
    nodes = [S, E]
    nodeMap = {}
    resolvedTargets = {}

    // Pass 1：先为全部有效传送点创建节点，禁止受枚举顺序影响
    for each valid teleporter snapshot t:
        if Sample(t.sourcePosition) fails:
            log once and skip t
        inNode = add T_t.in with unique node ID
        outNode = add T_t.out with different unique node ID at same sample
        nodeMap[t.stableId] = (inNode, outNode)
        add LocalTransition(outNode, inNode, cost=0, noCorners)

    // Pass 2：全部 out 节点存在后再解析传送目标
    for each t where nodeMap contains t.stableId:
        if t.extractionMode:
            resolvedTargets[t.stableId] = E
        else if t.pairedStableId is valid and nodeMap contains pairedStableId:
            resolvedTargets[t.stableId] = nodeMap[t.pairedStableId].out
        else:
            no teleport target for t

    // Pass 3a：Walk 只在 S、E、所有 T.in 之间计算
    walkNodes = [S, E] + all nodeMap[*].in
    for each ordered pair (a, b) of walkNodes:
        if a == b:
            continue
        if TryCalculateCompletePath(a, b):
            length = CleanAndMeasure(corners)
            cost = length / EstimatedWalkSpeed
            add directed Walk(a, b, cost, corners)

    // Pass 3b：只复制纯逻辑 timing DTO；可用性在 Dijkstra 到达入口时评估
    for each t with resolved target:
        timing = TeleportTimingSnapshot(
            stateCode=t.stateCode,
            countdownSecondsLeft=t.countdownSecondsLeft,
            countdownDuration=t.configuredOrReflectedCountdown,
            teleportWait=t.configuredOrReflectedWait)
        add directed Teleport(nodeMap[t.stableId].in, resolvedTargets[t.stableId],
                              teleportTiming=timing, noCorners,
                              teleporterId=t.stableId)

    return graph
```

优化要求：

- 固定节点间 Walk 边使用缓存
- S→所有候选节点与必要的候选→S 每次规划动态计算
- 若节点数量较多，可先用欧氏下界剪枝，但首版优先正确性
- 两方向 Walk 边分别计算，不假设 NavMesh 对称

### 7.4 传送成本与可用性

首版采用保守的 sweep 可用性模型。`TeleportAvailabilityPolicy.Evaluate` 可以返回 `Unavailable`；不可用边在本次 Dijkstra 松弛中直接跳过：

```text
TeleportAvailabilityPolicy.Evaluate(timing, arrivalAtEntrance):
    countdown = timing.CountdownDuration
    wait = timing.TeleportWait

    switch timing.StateCode:
        Idle:
            // 下一轮完整倒计时，从预计抵达入口后再激活
            return Available(countdown + wait)

        Activating:
            currentSweepAt = max(0, timing.CountdownSecondsLeft) + wait
            if arrivalAtEntrance <= currentSweepAt:
                return Available(currentSweepAt - arrivalAtEntrance)
            return Unavailable

        Finished:
            // 同步字段无法区分 0.5s sweep 前窗口和 sweep 后约 2s 保持期
            return Unavailable

        Unknown:
            return Unavailable
```

不得把“预计抵达晚于当前 sweep”错误压成固定 `wait`；不得把 `Finished` 固定计为 0.5s 可用。状态回到 Idle 或签名变化后重新规划。若未来要利用 Finished 的 sweep 前窗口，必须另外实现“观察到 Finished 边沿的本地时间戳、晚加入未知相位处理和误差上限”，并新增专项测试；首版不做。普通 Walk 路线必须在传送边不可用时继续参与求解。

### 7.5 Dijkstra 伪代码

```text
Solve(graph, S, E):
    for node: dist[node] = INF; previousEdge[node] = NONE
    dist[S] = 0
    queue.Push(S, 0)

    while queue not empty:
        u = queue.PopMin()
        if poppedDistance != dist[u]:
            continue
        if u == E:
            break

        for edge in outgoing[u]:
            if edge.Type == Walk:
                edgeCost = edge.WalkCostSeconds
            else if edge.Type == LocalTransition:
                edgeCost = 0
            else:
                evaluation = TeleportAvailabilityPolicy.Evaluate(edge.TeleportTiming, dist[u])
                if !evaluation.Available:
                    continue
                edgeCost = evaluation.IncrementalCost

            alt = dist[u] + edgeCost
            if alt < dist[edge.To] - epsilon:
                dist[edge.To] = alt
                previousEdge[edge.To] = edge
                queue.Push(edge.To, alt)

    if dist[E] == INF:
        return Unreachable

    edges = backtrack previousEdge from E to S
    reverse(edges)
    return RouteSolution(dist[E], edges)
```

稳定性：

- 相同成本在 epsilon 内时优先更少 Teleport edge
- 仍相同时优先更少总 edge 数
- 再相同时按稳定节点 ID，保证不同帧不随机切换

### 7.6 路线替换滞后

```text
SelectPlan(current, candidate, reason):
    if candidate invalid:
        keep current only if current still geometrically valid and retry window active
    if current invalid:
        choose candidate
    if reason in [PlayerTeleported, RouteDeviation, TargetChanged, CurrentEdgeInvalid]:
        choose candidate
    if same edge topology:
        refresh candidate
    if candidate.cost <= current.cost - RouteSwitchGainSeconds:
        choose candidate
    else:
        keep current
```

不能让滞后阻止传送后从实际落点重算。

### 7.7 偏离检测

将玩家位置投影到当前 Walk polyline：

```text
distance = minimum point-to-segment distance over current corners
if distance > RouteDeviationDistance:
    MarkDirty(RouteDeviation | RouteRequested)
```

附加条件：

- 玩家沿路径进度倒退不自动重算，只看距离
- 当前 Walk 段终点已到达且下一边是 Teleport：保持入口指示，等待实际传送
- 当前 Walk 段终点已到达但下一边仍是 Walk：推进到下一 edge 或重算

---

## 8. 箭头算法与 Compass 适配

### 8.1 Harmony Patch 清单

首版只允许以下 Patch：

| 目标 | 类型 | 用途 |
|---|---|---|
| `UICompass.Awake` | Postfix | 原 `_visualRefs` 创建后 attach |
| `UICompass.SetEnabled(bool)` | Postfix | 同步原 Compass 设置语义 |
| `UICompass.OnDestroy` | Prefix | 原对象销毁前恢复 Renderer、解绑 |
| `Pawn.OnTeleport(Vector3)` | Postfix | 本地 Pawn 传送后标记立即重算 |

不得 Patch：

- `UICompass.LateUpdate`
- `UICompass.UpdateCompassRotation`
- `UICompass.SetTarget`
- `TeleportExtractionCircle.TryExtract/SvExtractPlayers`
- `TeleportDeadEndCircle.TeleportRadiusTargets/SetPairedCircle`
- `DungeonGameplay.PairTeleporters`
- Mirror 生成的 serializer、SyncVar setter、`InvokeUserCode_*`
- `Pawn.NotifyTeleported`

所有 Patch 回调必须 try/catch，并只把异常交给 Mod 日志；不能阻止原方法执行。

### 8.2 原视觉筛选

发布运行时只允许固定算法：

```text
profile = CompassHierarchyProfile.ForCurrentBuild
refs = UICompass._visualRefs

assert profile.GameBuildHash == loaded Assembly-CSharp hash
assert every DirectionRendererAllowList entry matches exactly one Renderer
assert every PreservedRendererDenyList entry matches exactly one object
assert no allow entry is under/equal to a deny entry
assert ArrowParent and MaskOwner each match exactly one RectTransform

if any assertion fails:
    fail closed without modifying Renderer or creating arrow
else:
    for each exact allow entry:
        record renderer instanceID + original enabled
    after every entry has validated:
        set only those renderer.enabled = false
        create arrow using profile layout
```

Profile 生成是阶段 D0 的发布门禁：首次目标构建运行输出层级日志，人工核对并冻结明确常量表；D1 编码只消费该表。不得采用“关闭 `RenderTargetRoot` 下所有 Renderer”、遍历整个 pivot 子树、模糊名称匹配或运行时二选一规则。

### 8.3 look-ahead 点

先把玩家投影到当前 Walk polyline，再向前取弧长：

```text
ResolveLookAhead(player, corners, lookAheadDistance):
    clean corners
    projection = FindClosestProjection(player, polyline)
    startDistance = cumulative distance at projection
    desired = startDistance + lookAheadDistance
    point = SamplePolylineAtDistance(desired)

    while horizontalDistance(player, point) < SkipNearCornerDistance
          and desired < totalLength:
        desired += smallStep
        point = SamplePolylineAtDistance(desired)

    return point
```

默认 look-ahead 6m，可配置 4–8m 范围内调试。

### 8.4 角度

```csharp
Vector3 cameraForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
Vector3 targetDirection = Vector3.ProjectOnPlane(lookAhead - camera.transform.position, Vector3.up);

if (cameraForward.sqrMagnitude >= epsilon && targetDirection.sqrMagnitude >= epsilon)
{
    float signed = Vector3.SignedAngle(cameraForward.normalized, targetDirection.normalized, Vector3.up);
    targetZ = -signed;
}
```

用 `Mathf.SmoothDampAngle(currentZ, targetZ, ref angularVelocity, ArrowSmoothTime)`。

### 8.5 高度颜色状态机

优先级：

```text
CurrentSubTargetIsTeleportEntrance → Purple
否则 VerticalState=Up              → Blue
否则 VerticalState=Down            → Red
否则                               → White
```

双阈值：

```text
Level → Up    when deltaY >= EnterThreshold
Level → Down  when deltaY <= -EnterThreshold
Up → Level    when deltaY <= ExitThreshold
Down → Level  when deltaY >= -ExitThreshold
```

颜色基于 look-ahead/下一有效路段趋势，不基于最终撤离点总高度。

建议色值集中在 `ArrowColorPolicy`，不要散落硬编码。颜色需在目标 UI 材质上验证可读性，并保留色盲兼容的形状/轻微脉冲差异作为后续可选增强。

### 8.6 到达点附近

若玩家与当前子目标水平距离 < 0.5m：

- 保持最后有效角度
- 若不是传送入口，颜色白色
- 若是传送入口，保持紫色
- 不隐藏箭头，仅可降低旋转响应

---

## 9. 世界光点算法

### 9.1 当前连续 Walk 段选择

```text
BuildDisplayWalkSegment(plan):
    i = plan.CurrentEdgeIndex
    corners = []

    while i < plan.Edges.Count and plan.Edges[i].Type == LocalTransition:
        i++ // 逻辑边永不渲染

    if i >= plan.Edges.Count:
        return None

    if plan.Edges[i].Type == Teleport:
        return MarkerOnly(plan.Edges[i].EntrancePosition)
    if plan.Edges[i].Type != Walk:
        return None

    while i < plan.Edges.Count and plan.Edges[i].Type == Walk:
        AppendCornersRemovingDuplicateJoin(corners, plan.Edges[i].WalkCorners)
        i++

    // 连续 Walk 后的第一个可见边只可能是 Teleport；LocalTransition 只能在 Teleport 之后
    if i < plan.Edges.Count and plan.Edges[i].Type == Teleport:
        return DisplaySegment(corners, endsAtTeleport=true,
                              entrance=plan.Edges[i].EntrancePosition)

    return DisplaySegment(corners, endsAtTeleport=false)
```

对 `Walk1→Walk2→Teleport→Walk3`，传送前只提交拼接清洗后的 `Walk1+Walk2` 和入口 marker；不得创建 `Walk3` 的任何对象。传送后旧 plan 整体 stale，只能从实际落点新规划。若路线无传送，则显示从当前进度到撤离圈的连续 Walk 段。

### 9.2 路径清洗

```text
CleanCorners(input):
    output = []
    for p in input:
        if output empty or Distance(output.last, p) >= 0.05m:
            output.Add(p)
    remove zero/near-zero segments
    return output
```

### 9.3 累计弧长与采样

```text
BuildCumulative(points):
    cumulative[0] = 0
    for i = 1..n-1:
        cumulative[i] = cumulative[i-1] + Distance(points[i-1], points[i])

SampleAtDistance(d):
    d = Clamp(d, 0, total)
    i = binary search first cumulative[i] >= d
    segment = i-1 → i
    t = (d - cumulative[i-1]) / segmentLength
    return Lerp(points[i-1], points[i], t)
```

点数量：

```text
count = min(TrailMaxDots, floor(totalLength / TrailDotSpacing) + 1)
```

从玩家投影进度之后开始采样，避免玩家身后继续显示大量点。

### 9.4 流动动画

每个点保存归一化路径位置 `u` 和 phase：

```text
phase = frac(globalTime * speed + u * frequency)
alpha = EvaluatePulse(phase)
scale = Lerp(minScale, maxScale, alpha)
```

首版不需要 AnimationClip。用共享材质 + `MaterialPropertyBlock` 更新颜色/alpha；若 UI/粒子 Shader 不支持 MPB，再选择单一共享发光材质并只改变 Transform。

### 9.5 地面与遮挡

- NavMesh corner 已在地面附近，视觉点加 `TrailGroundOffset`
- 可选向下 Raycast 只作视觉贴地，不改变路线
- Raycast 失败保留 NavMesh 点，不删除整条路径
- 不做昂贵的每点每帧 Raycast；只在路线提交时执行

### 9.6 隐藏与清理

立即隐藏条件：

- 本地玩家到达
- 本地玩家死亡或已提取
- 原 Compass 设置关闭只隐藏原指南针区域与新箭头；世界 trail 继续按 `ShouldShowTrail` 门控显示，不擅自扩大原设置语义
- 回合结束
- 传送信号
- 路线失效
- Mod 禁用

对象池可跨本轮复用，但不能跨已销毁场景材质/父节点失效而不重建。

---

## 10. 分阶段实施任务

每一阶段完成后立即构建和执行相关测试。不得把所有文件一次性写完后再统一调试。

### 阶段 A：脚手架、配置与 fail-closed

前置：工作区可读取目标游戏和 BepInEx 程序集。

文件：

- `HomeGuidance.csproj`
- `PluginInfo.cs`
- `Plugin.cs`
- `HomeGuidanceConfig.cs`
- `BuildGuard.cs`
- `SupportedGameBuilds.cs`
- `Logging/*`

步骤：

1. 复制 `MoreSlots` 的路径属性和 Release 输出模式，但改为独立根命名空间
2. 显式排除 `decompiled/**`、其他 Mod 和测试源码
3. 实现配置绑定与归一化
4. 从加载程序集位置计算实际 SHA-256
5. 写关键成员探测
6. 未通过时不 Patch、不创建 Host
7. 通过时创建空 Host 并 PatchAll
8. `OnDestroy` 实现 Unpatch + Host 销毁

必须日志：

```text
[HomeGuidance] Version ...
[HomeGuidance] Assembly-CSharp SHA256 ...
[HomeGuidance] Build guard: supported/unsupported
[HomeGuidance] Reflection probe: n/n required members
[HomeGuidance] Network mode: client-local; no custom Mirror messages
```

验收：

- Release 构建成功
- 已知哈希加载 Host；未知哈希安全停用
- 重复加载不会创建两个 Host
- 卸载时无残留 Harmony Patch

失败回滚：只保留插件日志入口，不继续实现运行时功能。

### 阶段 B：生命周期与到达状态

文件：

- `Runtime/GuidanceRuntimeHost.cs`
- `Runtime/GuidanceController.cs`
- `Runtime/GuidanceLifecycle.cs`
- `Runtime/RoundGuidanceState.cs`
- `Arrival/*`

步骤：

1. 实现 RoundActive 边沿检测和本地 roundToken；同 token BeginRound no-op
2. 订阅 `TeleportExtractionCircle.OnSpawned`，并保留轮询查找回退
3. 每 0.15s 扫描玩家
4. 使用 `HashSet<uint>` 单调记录，并拒绝旧 token 迟到结果
5. 打印新增到达和本地 trail 门控状态
6. EndRound 只清本轮状态/缓存，不销毁 Host、事件订阅、Compass adapter 或对象池

验收：

- 第一名玩家进入 2m 后 `GuidanceUnlocked=true`
- 离开、死亡、断线后状态不回退
- 同 token 重复 BeginRound 不清 reached；重复 EndRound 幂等
- 本地玩家到达只关闭 trail 门控，不关闭箭头门控
- 连续两轮使用同一 Host，第二轮状态为空且功能仍工作

### 阶段 C：普通 NavMesh 路线

文件：

- `Routing/RouteModels.cs`
- `Routing/RouteRequest.cs`
- `Routing/RoutePlan.cs`
- `Routing/NavMeshPathService.cs`
- `Routing/NavMeshPathCache.cs`
- `Routing/PathGeometry.cs`
- `Routing/RoutePlanner.cs`

步骤：

1. 采样玩家与撤离圈地面点
2. 计算 S→E 完整路径
3. 清洗 corners、计算长度和预计秒数
4. 实现偏离检测与重试节流
5. 路线失败时不创建直接线
6. 在 Debug 日志输出 status、corner 数、长度、耗时

验收：

- 普通可达场景得到 PathComplete
- Partial/Invalid 时 plan 无效
- 玩家移动不每帧重算
- 偏离 >3m 时重算
- 路线失败时箭头/光点不会穿墙直指 E

### 阶段 D0：指南针层级发现与 Profile 冻结（D1 前置阻断）

文件：

- `Compass/CompassHierarchyProbe.cs`
- `Compass/CompassHierarchyProfile.cs`
- 运行证据：`research/evidence/home-guidance-compass-hierarchy-<buildHash>.log`
- 运行证据：attach 前/后 active 与 inactive 固定分辨率截图

步骤：

1. 在目标支持构建上运行只读 Probe，不关闭任何 Renderer
2. 导出完整相对路径、Renderer 类型/名称、RectTransform 布局、Mask 与 sibling 信息
3. 人工确认旧方向元素、必须保留元素和新箭头承载节点
4. 冻结唯一常量表：allow list、deny list、父节点、sibling 规则、anchor/pivot/size/position/Mask
5. 将常量表与 `Assembly-CSharp` 哈希绑定
6. 二次运行验证每项精确匹配一次；缺失或重复均失败

验收：

- 被关闭 Renderer 的预期路径集合明确且无模糊匹配
- 背景、外框、共享父级全部进入 deny/preserved 证据
- `NavigationArrowLayer` 的父节点和完整 RectTransform 常量已冻结
- 层级日志与截图已保存；没有这些证据不得开始 D1 或判定可发布

### 阶段 D1：指南针视觉适配

文件：

- `Patches/UICompassPatches.cs`
- `Compass/CompassOriginalVisualState.cs`
- `Compass/CompassVisualAdapter.cs`
- `Compass/NavigationArrowLayer.cs`
- `Compass/ArrowDirectionSolver.cs`
- `Compass/ArrowColorPolicy.cs`
- `Assets/HomeGuidanceAssets.cs`

步骤：

1. `UICompass.Awake` Postfix attach，并实现 1.0 秒查找/60 帧 pending 状态机
2. 严格消费 D0 冻结 profile；全部断言通过后才批量抑制白名单 Renderer
3. 在 profile 指定节点创建唯一新箭头
4. 实现 look-ahead、signed angle、平滑、颜色；不读取原 Pivot 本帧旋转
5. `SetEnabled` Postfix 只同步设置门控；true 不无条件显示
6. 结合 `currentTarget != null` 和 active frame 状态决定箭头显示
7. `OnDestroy` Prefix 只恢复/解绑该 Compass 实例

验收：

- 固定分辨率和 UI scale 下，背景/外框 Rect 每轴差值 ≤1px
- attach 前后 active/inactive frame 层级状态一致，Mod 不直接 SetActive 两个 frame
- 原 Compass 设置关闭后仅新箭头隐藏；world trail 保持由 `ShouldShowTrail` 控制
- 再次打开设置时仅在原 Compass 有有效 target 时恢复箭头
- 同层/上/下/传送入口颜色正确；已到达玩家箭头继续工作
- Mod 总开关关闭时旧 Renderer 恢复；再次打开不创建重复箭头
- Mod 热加载、Compass 初始 inactive、销毁重建三种场景均在 1 秒查找窗口内恢复
- 不 Patch `UICompass.LateUpdate`

失败回滚：恢复该实例已修改的 Renderer，关闭箭头子功能；不得以禁用整个 UICompass 代替修复。

### 阶段 E：世界光点

文件：

- `Trail/GuidanceTrailPresenter.cs`
- `Trail/TrailDotPool.cs`
- `Trail/TrailDotView.cs`
- `Trail/TrailSampling.cs`
- `Trail/TeleportEntranceMarker.cs`

步骤：

1. 实现 clean/cumulative/binary-search sample
2. 实现对象池和最大点数
3. 提交当前 Walk corners
4. 实现流动 phase
5. 应用本地显示门控
6. 到达、传送、回合结束立即 Hide

验收：

- 点间距按弧长均匀，不按 corner 索引
- 频繁重算无持续 Instantiate/Destroy 峰值
- 第一人到达前无 trail
- 未到达本地玩家显示
- 本地玩家首次到达立即隐藏且本轮不恢复

### 阶段 F：有向传送图和 Dijkstra

文件：

- `Routing/TeleporterAccess.cs`
- `Routing/TeleporterSnapshot.cs`
- `Routing/TeleporterGraphProvider.cs`
- `Routing/DijkstraSolver.cs`
- `Routing/TeleportAvailabilityPolicy.cs`
- `Routing/RouteSelectionPolicy.cs`
- 扩展 `RoutePlanner.cs`

步骤：

1. 缓存所有 getter/MethodInfo
2. 枚举传送点并建立拓扑/成本签名
3. Pass 1 为全部有效传送点建立不同 node ID 的 `.in/.out`；固定添加 `T.out→T.in` 零成本不可渲染 `LocalTransition`
4. Pass 2 在全部节点创建后解析 pair/extraction 目标，禁止受枚举顺序影响
5. Pass 3 只在 S/E/T.in 间计算有向 Walk 边，再按真实 `NetworkpairedCircle` 添加单向 Teleport edge
6. `NetworkisInExtractionMode=true` 时，把本地已观察到的当前 `TeleportExtractionCircle` 作为目标 E；不得读取 `activeExtractionCircle`
7. Dijkstra 使用预计抵达入口时刻评估 sweep：错过 Activating 当前 sweep、Finished、Unknown 均不添加该次可用边
8. 加 0.35s 路线切换滞后
9. plan 输出 ordered edges、`CurrentEdgeIndex` 和冻结 `DisplaySegment`

验收：

- A→B→C→A 测试不自动生成反向边；恰好两个传送点时双方 getter 互指
- `A.in→B.out→LocalTransition→B.in→C.out` 链式传送可达，LocalTransition 不渲染
- 双向 pair 只有双方各自 getter 指回时才有两条边
- 单一传送点 extraction mode 可直达 E，纯客户端不依赖 `activeExtractionCircle`
- Partial Walk edge 不进入图
- 最短几何路线与最快时间路线不同时选择可用且更快者
- Activating 且预计抵达早于当前 sweep 时允许；预计抵达晚于 sweep 时该边不可用
- Finished/Unknown 不生成传送边，普通 Walk 路线仍可工作

### 阶段 G：传送后重算和分段显示

文件：

- `Patches/PawnTeleportPatch.cs`
- 扩展 Controller、Trail、RoutePlanner

步骤：

1. Patch private `Pawn.OnTeleport(Vector3)` Postfix
2. 只处理本轮存活且未提取的 `isLocalPlayer`
3. 实现同 pawn 的同帧或相邻一帧且落点差 <0.25m 的传送通知去重；超过一帧不合并
4. 实现 position jump baseline、Patch 后一帧抑制和误报门控
5. 首次有效通知立即 Hide trail/marker，并只增加一次 `teleportGeneration`
6. 下一帧从实际 transform `SamplePosition`
7. 清动态起点边，重跑 Dijkstra；拒绝旧 round/teleport generation 的迟到 plan
8. 只显示新位置开始的当前连续 Walk 段

验收：

- 传送前 trail 止于入口；`Walk1→Walk2→Teleport→Walk3` 只显示 Walk1+Walk2 和入口 marker
- 不出现入口到远端出口的直线点列，也不创建 Walk3 对象
- Host 本地玩家和纯 Client 本地玩家均触发相同重算契约
- 不依赖 `Pawn.OnTeleportedEvent`
- Postfix 与 jump fallback 同帧命中时 generation 和规划请求各只有一次
- spawn、回合开始、authority/Pawn 变化的首个位置样本不误报
- 无论 RPC 参数与下一帧 transform 是否一致，`RouteRequest.SampledStart` 都来自实际 transform
- 新计划失败时旧 trail 持续隐藏，不回显旧段

### 阶段 H：兼容、打包与文档

文件：

- `README.md`
- `CHANGELOG.md`
- `manifest.json`
- `thunderstore.toml`
- `Build-Package.ps1`
- `icon.png`

步骤：

1. 补齐中英文功能、安装、配置、联机和限制
2. 明确“客户端可单独安装；未安装者可联机但看不到功能”
3. 构建脚本依次 restore、build、tests、copy dist、tcli build
4. 包内只包含运行需要的 DLL 与文档/manifest/icon
5. 检查程序集版本、插件版本、manifest、thunderstore 版本一致

验收：

- 一条命令完成 Release + tests + package
- 解压包目录符合 BepInEx 插件布局
- 干净 Profile 安装成功
- 未安装 Mod 的 Client 可加入安装 Mod 的 Host，反向亦可

---

## 11. 自动测试计划

### 11.1 Dijkstra

至少覆盖：

1. 单一路径
2. 不可达目标
3. 有向边不能反走
4. A→B→C→A 环无死循环
5. `A.in→B.out→LocalTransition→B.in→C.out` 链式传送可达，LocalTransition 成本为 0
6. 步行短但慢于可用传送的路线选择
7. Activating 且预计抵达早于当前 sweep 时允许
8. Activating 且预计抵达晚于当前 sweep 时返回 Unavailable
9. Finished/Unknown 不松弛传送边，普通 Walk 仍可求解
10. 相同成本优先更少传送
11. 稳定 ID tie-break 保持确定性

### 11.2 路线切换策略

1. 当前无效立即替换
2. 新路线只快 0.1s，不超过 0.35s 阈值时保持
3. 新路线快 0.5s 时替换
4. 传送后忽略滞后
5. 当前首边失效时忽略滞后
6. 同拓扑允许刷新 corners

### 11.3 几何与 trail

1. 删除重复点
2. 总长度正确
3. 首尾采样正确
4. 二分插值在中间段正确
5. 非均匀 corner 仍产生均匀点距
6. 长路径受 `TrailMaxDots` 限制
7. 玩家投影后不显示身后点
8. 点到 polyline 最短距离正确
9. `Walk1→Walk2→Teleport→Walk3` 拼接 Walk1+Walk2、删除接缝重复 corner
10. 传送前不返回 Walk3，且 marker 位于 Teleport 入口
11. 传送后旧 plan generation 不可推进到远端 Walk edge
12. 零长度 Walk 被清洗后，MarkerOnly 仍显示入口 marker 且不创建普通 dots

### 11.4 箭头

1. 正前方 0°
2. 右侧和左侧符号正确
3. 摄像机有 pitch 时投影后角度稳定
4. 目标水平向量近零时保持最后角
5. teleport purple 优先于 up/down
6. 高度双阈值无边界闪烁

### 11.5 回合与到达状态

1. 第一次 `MarkReached(netId, currentToken)` 解锁
2. 重复 MarkReached 返回 false
3. 同 token 重复 `BeginRound` 严格 no-op 且不清 reached
4. 玩家离开、死亡、断线均不删除
5. `ArrivalRadius` 运行时变化不清 reached
6. `EndRound` 第一次清空，重复调用幂等
7. 旧 token 的迟到 MarkReached 被拒绝
8. 新 roundToken 才清空并重新开始
9. 插件中途加载到 active round 只创建一个 generation
10. `ArrivalRadius` 边界内/外判定

### 11.6 传送图策略

使用不依赖 Unity 的 snapshot：

1. paired null 不生成 Teleport edge
2. source→target 只生成单向边
3. 恰好两个传送点且 getter 互指时生成两条方向明确的边
4. extraction mode 以当前观察到的撤离圈 E 为目标，不读取 `activeExtractionCircle`
5. 普通模式指 paired target
6. topology hash 与 cost hash 分离
7. countdown 变化不强制清固定 Walk cache
8. Finished/Unknown snapshot 不生成当前可用 Teleport edge

### 11.7 传送通知去重

1. Postfix 与 jump fallback 同帧命中只增加一次 `teleportGeneration`
2. 同一 pawn 同帧重复 Postfix 只产生一个 `RouteRequested`
3. 同一 pawn 在同帧/相邻一帧内且落点差 <0.25m 的重复通知被合并
4. 同一 pawn 数秒后再次传送到相近落点时 generation 必须再次增加
5. round/Pawn/authority/场景 generation 改变后的首样本只建立 baseline
6. Patch 当帧和下一帧 jump fallback 被抑制
7. 普通移动低于阈值不触发；真实位置跃迁在 Patch 缺失时能触发
8. 旧 roundToken 或旧 teleportGeneration 的 candidate plan 被 Controller 拒绝提交

---

## 12. 运行时测试矩阵

每项记录：游戏构建哈希、Mod 版本、Host/Client 安装组合、地图/回合、日志片段、截图或视频、结果。

### 12.1 基础加载

- [ ] 支持构建正常加载，无 Harmony 错误
- [ ] 未支持哈希整项停用，原游戏 UI 不受影响
- [ ] 反射探测报告完整
- [ ] 场景切换不生成重复 Host
- [ ] 连续完成两轮时 Host GameObject instanceID 保持相同，第二轮仍可扫描、规划和解锁 trail
- [ ] 两轮间静态事件回调不重复；第一轮 reached/RoutePlan/图缓存不泄漏
- [ ] 退出游戏无清理异常

### 12.2 指南针视觉

- [ ] 当前构建冻结 profile、层级日志和前后截图齐全
- [ ] 实际被关闭 Renderer 路径集合与 allow list 完全一致；任一路径缺失/多匹配时 fail closed
- [ ] 固定分辨率/UI scale 下，大背景与外框 Rect 每轴差值 ≤1px
- [ ] active/inactive frame 仍只由原版切换，Mod 从不直接 SetActive
- [ ] `infoRect` 位置切换不变
- [ ] `CompassEnabled=false` 时新箭头隐藏，但满足 `ShouldShowTrail` 的 world trail 继续显示
- [ ] `CompassEnabled=true` 但 `currentTarget==null` 时新箭头仍隐藏
- [ ] `HomeGuidance Enabled=false` 时 arrow/trail/marker 隐藏且旧 Renderer 恢复
- [ ] 再次 `HomeGuidance Enabled=true` 时仅一个箭头层，当前回合 reached 不被清空
- [ ] 背景、外框、共享父级 Renderer 的 enabled 值从未改变
- [ ] Mod 停用/对象销毁后旧 Renderer 状态恢复
- [ ] 其他修改 UICompass 的 Mod 共存时无整块消失

### 12.3 单人/Host

- [ ] 回合开始立即有箭头
- [ ] 第一人到达前无 trail
- [ ] 进入 2m 后状态解锁且箭头继续
- [ ] 离开撤离点后到达状态不回退
- [ ] 死亡/提取后视觉正确隐藏
- [ ] 下一轮重置

### 12.4 多人 Host + Client

组合：

1. Host 安装、Client 安装
2. Host 安装、Client 未安装
3. Host 未安装、Client 安装
4. 两者均安装但配置不同

验证：

- [ ] 均能连接，不出现未知 Mirror 消息
- [ ] 安装者只看到自己的本地箭头/光点
- [ ] 未安装者无异常
- [ ] 任一可见玩家到达后，安装客户端在扫描延迟内解锁
- [ ] 到达状态只增不减
- [ ] 远端玩家死亡/断线不清历史

### 12.5 路线与 NavMesh

- [ ] 同层路线白色
- [ ] 当前需要上楼为蓝色
- [ ] 当前需要下楼为红色
- [ ] 最终目标高度与当前路段相反时仍按当前路段着色
- [ ] 拐角附近箭头不高频抖动
- [ ] Partial/Invalid 路径不显示穿墙直线
- [ ] 玩家离开路线 >3m 后重算
- [ ] 路线相近时不频繁切换

### 12.6 传送点

- [ ] 恰好两个传送点的双向 pair 路线正确
- [ ] 三点有向环路线正确且不自动生成反向边
- [ ] extraction mode 以客户端当前观察到的撤离圈 E 为目标，不依赖非同步字段
- [ ] Idle 使用完整下一轮成本
- [ ] Activating 且预计抵达早于当前 sweep 时允许；晚于 sweep 时不得选择
- [ ] Finished sweep 后和 Unknown 状态不得按 0.5 秒可用计算
- [ ] 当前子目标是入口时箭头紫色
- [ ] `Walk1→Walk2→Teleport→Walk3` 传送前只显示拼接 Walk1+Walk2 和入口 marker
- [ ] 入口与远端出口之间无光点，且未创建 Walk3 对象
- [ ] 传送后下一帧从实际 transform 重算，旧 plan generation 不得提交
- [ ] Postfix 与 jump fallback 同帧命中时 generation/规划请求各一次
- [ ] spawn、回合开始、authority/Pawn 切换首样本不误报传送
- [ ] 新计划无效时旧 trail 持续隐藏
- [ ] Host 与纯 Client 都收到本地 `OnTeleport` Patch 回调

### 12.7 晚加入与异常场景

- [ ] 晚加入时当前在圈内玩家可被首次扫描识别
- [ ] 晚加入无法恢复历史时行为与 README 限制一致
- [ ] 撤离圈晚生成后 attach
- [ ] 传送 pair getter 暂时 null 时延迟重试
- [ ] 本地 Pawn 暂时 null 时不报错
- [ ] Mod 在 `UICompass.Awake` 已执行后加载，1 秒查找窗口内 attach
- [ ] UICompass 初始 inactive 时仍能被发现，启用后无需重载 Mod
- [ ] `_visualRefs` 暂为空后在 60 帧内可用时成功恢复；超时后回到低频查找而非永久失败
- [ ] UICompass 重建后旧 adapter 解绑、新对象 attach；Attached 稳态不继续枚举
- [ ] 地牢销毁时所有 Unity fake-null 引用安全清理
- [ ] 配置运行时变化不清除同轮 reached 集合
- [ ] 同 token 重复 BeginRound、重复 EndRound 和旧 token 迟到更新符合幂等/拒绝契约

### 12.8 性能

在包含多个传送点的回合，以 Development Build + Profiler 连续采样 60 秒：

- [ ] Detached 时 UICompass 枚举频率 ≤1 次/秒；Attached 稳态枚举次数为 0
- [ ] 无 dirty reason 的连续 10 秒内 `NavMesh.CalculatePath` 调用为 0
- [ ] trail 重算后对象池复用，无持续 Instantiate/Destroy
- [ ] Mod 的 Update/LateUpdate 稳态不产生持续逐帧 managed allocation
- [ ] DebugLogging=false 时逐帧日志数为 0
- [ ] 候选收益小于 `RouteSwitchGainSeconds` 时不切换拓扑
- [ ] 固定高度阈值附近噪声不造成逐帧切色
- [ ] 路径缓存命中率和重算原因可在 Debug 模式查看

---

## 13. 日志规范

日志前缀统一：`[HomeGuidance]`。

级别：

- Info：加载、构建守卫、回合开始/结束、首次 attach、首次到达解锁
- Debug：路线节点/边、成本、缓存命中、dirty reasons、层级探测
- Warning：可选字段读取失败、路径暂不可达、pair 暂未同步
- Error：必需成员缺失、Compass 安全适配失败、Patch 安装失败

高频事件必须节流或 one-shot。

推荐路线摘要：

```text
Route planned reason=TeleportObserved nodes=8 edges=22 total=14.27s
segments=Walk(7.8s)->Teleport#123(0.5s)->Walk(5.97s)
selected=replaced gain=2.14s cache=fixed:12/14 dynamic:4
```

禁止记录玩家隐私信息或语音内容；玩家使用 netId/instanceID 即可。

---

## 14. 兼容性和安全要求

### 14.1 Client / Server / Mirror 边界表

| 行为 | Client | Host/Server | Mod 是否介入 |
|---|---|---|---|
| 玩家位置同步 | 读取 | 原版权威 | 只读 |
| 到达判定 | 各安装客户端本地观察 | Host 也按本地客户端观察 | 是，本地状态 |
| 原版提取判定 | 不改 | `SvExtractPlayers` 权威 | 否 |
| 传送配对 | 读取 SyncVar | 原版 `PairTeleporters` 设置 | 否 |
| extraction mode | 读取 SyncVar | 原版设置 | 否 |
| 传送执行 | 观察本地 `OnTeleport` | 原版 `SvTeleport` | 只做 Postfix 通知 |
| 路线 | 本地计算 | Host 的本地玩家也本地计算 | 是 |
| 箭头/光点 | 本地 GameObject | 不同步 | 是 |
| 自定义消息 | 无 | 无 | 禁止 |

### 14.2 与其他 Mod 共存

- Harmony 使用唯一 GUID，Patch 尽量 Postfix/观察型
- 不返回 false 跳过原方法
- 不改原参数和返回值
- 不覆盖 UICompass per-frame 原逻辑
- 恢复 Renderer 时只恢复本 Mod 曾记录和修改的实例
- 若 Renderer 在 attach 后被其他 Mod 改变，detach 默认恢复 attach 前状态；Debug 日志记录冲突可能性
- 新箭头对象名带固定前缀并先查重
- 不依赖加载顺序查找其他 Mod

### 14.3 故障降级

- 路由失败：箭头灰/隐藏，trail 隐藏，原背景保留
- 传送 getter 暂无数据：仅计算普通步行路线并短期重试
- Compass adapter 失败：路由/trail 可运行，箭头停用；发布前必须修复
- trail 材质失败：关闭 trail，不影响箭头
- Build Guard/必需反射失败：整项停用

---

## 15. 构建、安装与打包

### 15.1 本地命令

```powershell
dotnet build .\HomeGuidance\HomeGuidance.csproj -c Release
dotnet run --project .\HomeGuidance.Tests\HomeGuidance.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\HomeGuidance\Build-Package.ps1
```

默认参考路径：

```text
GameDir    = C:\Program Files (x86)\Steam\steamapps\common\YAPYAP
ProfileDir = C:\Users\Home\AppData\Roaming\Thunderstore Mod Manager\DataFolder\Yapyap\profiles\Default
```

项目必须允许命令行覆盖，不能把本机绝对路径写死为唯一来源。

### 15.2 `Build-Package.ps1` 顺序

```text
$ErrorActionPreference = Stop
→ dotnet tool restore
→ dotnet build Release
→ dotnet run tests Release
→ 清理并创建 dist
→ 复制 DLL、README、CHANGELOG、manifest、icon
→ dotnet tool run tcli build
→ 输出包路径与 SHA-256
```

测试失败不得继续打包。

### 15.3 发布文档必须说明

- 客户端可单独安装
- Host 不要求安装
- 未安装客户端仍可加入
- 只有安装者看得到导航
- 首版晚加入历史状态限制
- Compass 原设置只控制原 Compass 生命周期和新箭头，不控制世界 trail/marker
- 游戏更新导致哈希未知时 Mod 会安全停用

---

## 16. Definition of Done

只有全部满足才能视为首版完成：

### 16.1 代码

- [ ] 所有目标文件存在且职责清晰
- [ ] Release 零编译错误
- [ ] 无未处理的关键编译警告
- [ ] 无未完成占位标记、临时硬编码路径或调试占位逻辑
- [ ] 无自定义 Mirror 消息或网络对象
- [ ] 无对 Server-only `NotifyTeleported` 的客户端依赖
- [ ] 无对原版提取/传送/配对行为的修改
- [ ] EndRound、DetachCompass、Plugin.Shutdown 三层清理不混用
- [ ] 当前支持构建哈希与关键成员探测已冻结

### 16.2 算法

- [ ] ArrivalRadius 独立为 2.0m 默认
- [ ] 同轮状态只增不减
- [ ] Walk edge 仅 PathComplete
- [ ] 有向传送图正确
- [ ] Dijkstra 按预计时间
- [ ] sweep 可用性模型不会选择已错过的 Activating 或相位不明的 Finished 批次
- [ ] 路线滞后生效且不阻止传送后强制换代
- [ ] 传送通知去重，每次真实传送只增加一次 generation
- [ ] 传送后实际落点重算，旧 generation plan 不可提交
- [ ] 连续 Walk 边正确拼接且不跨传送边渲染

### 16.3 视觉

- [ ] 原指南针背景、外框、位置、尺寸、遮罩和布局保留
- [ ] 原 active/inactive 和设置开关保留；Mod 从不直接 SetActive 两个 frame
- [ ] Compass 设置只影响新箭头，不影响 world trail/marker
- [ ] 冻结 hierarchy profile、日志和前后截图齐全
- [ ] 只隐藏 profile 精确确认属于旧方向动画的 Renderer，失配时 fail closed
- [ ] 箭头角度、白/蓝/红/紫优先级正确
- [ ] 已到达玩家箭头继续工作
- [ ] 只有未到达本地玩家在解锁后看到 trail
- [ ] 清理时原 Renderer 完整恢复

### 16.4 测试与发布

- [ ] 全部纯逻辑测试通过
- [ ] 单人、Host、纯 Client 测试通过
- [ ] 安装组合兼容测试通过
- [ ] 性能验收通过
- [ ] 干净 Profile 安装通过
- [ ] 一键构建打包通过
- [ ] README、CHANGELOG、manifest、插件版本一致

---

## 17. 实现 Agent 的最终交付报告格式

实现完成后必须报告：

1. 创建/修改文件清单
2. 关键架构与偏离本计划之处
3. 使用的 Assembly-CSharp SHA-256 与探测结果
4. Harmony Patch 实际清单
5. 自动测试命令和结果
6. Release 构建命令和结果
7. 运行时矩阵已完成项及证据路径
8. 尚存限制和发布阻断项
9. Thunderstore 包路径和 SHA-256

若某项未完成，必须明确标记为发布阻断，不得用“基本完成”代替。

---

## 18. 关键源码索引

| 事实 | 位置 |
|---|---|
| `UICompass` | `decompiled/YAPYAP/FullAssembly.cs:176000-176289` |
| `_visualRefs` 创建 | `:176095-176104` |
| Compass 原事件/设置 | `:176115-176143`、`:176209-176240` |
| 原方向旋转不足 | `:176242-176261` |
| `UICompassReferences` | 约 `:176340-176353` |
| 撤离圈事件 | `:80388-80392` |
| 撤离圈客户端 Spawn | `:80399-80402` |
| 原提取半径与判定 | `:80323-80324`、`:80618-80627` |
| 传送点字段/SyncVar | `:78728-78823` |
| 传送 getter | `:78855-78905` |
| 传送实际目标 | `:79122-79189` |
| 随机落点 | `:79192-79199` |
| extraction mode | `:79282-79303` |
| 传送配对 | `:52751-52814` |
| Server-only 传送事件 | `:105912-105923` |
| `SvTeleport` | `:105934-105957` |
| 客户端 `OnTeleport` | `:105971-106017`、`:106755-106761` |

本计划中的“必须”条目是首版验收边界；实现过程中如源码探测与此索引不一致，应先更新核验文档和 Build Guard，再继续编码，不得静默猜测。
