# YAPYAP Mod 开发规范

基于 SaveGuard 开发过程总结，适用于所有 YAPYAP BepInEx 模组。

---

## 1. 环境搭建

### 1.1 必备工具

| 工具 | 用途 |
|---|---|
| [BepInEx 5](https://github.com/BepInEx/BepInEx) | 模组加载框架 |
| [Harmony](https://github.com/pardeike/Harmony) | 运行时 IL 补丁 |
| [ilspycmd](https://github.com/icsharpcode/ILSpy) | 反编译游戏程序集 |
| [.NET SDK](https://dotnet.microsoft.com/) | 编译模组 DLL |
| [Native Settings UI Lib](https://thunderstore.io/c/yapyap/p/XiaohaiMod/Native_Settings_UI_Lib/) | 设置页注入库（推荐，可替代手写注入） |
| [TCLI](https://github.com/thunderstore-io/thunderstore-cli) | Thunderstore 打包 |
| [Thunderstore Mod Manager](https://www.overwolf.com/app/thunderstore-thunderstore_mod_manager) | 模组管理和测试 |

### 1.2 游戏程序集

游戏目录：`steamapps/common/YAPYAP/yapyap_Data/Managed/`

核心依赖：

```
Assembly-CSharp.dll      # 游戏主逻辑
Mirror.dll               # 网络框架
UnityEngine.dll / UnityEngine.CoreModule.dll / UnityEngine.UI.dll
Unity.TextMeshPro.dll
BepInEx/core/BepInEx.dll
BepInEx/core/0Harmony.dll
```

### 1.3 项目模板

```xml
<!-- .csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>10.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>ModName</AssemblyName>
    <RootNamespace>ModName</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <!-- 排除工作区中其他项目的源码 -->
    <Compile Remove="other-project\**" />
    <None Remove="other-project\**" />

    <!-- 游戏引用 -->
    <Reference Include="Assembly-CSharp" HintPath="$(ManagedDir)\Assembly-CSharp.dll" Private="false" />
    <Reference Include="Mirror" HintPath="$(ManagedDir)\Mirror.dll" Private="false" />
    <Reference Include="BepInEx" HintPath="$(BepInExCore)\BepInEx.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExCore)\0Harmony.dll" Private="false" />
    <Reference Include="UnityEngine" HintPath="$(ManagedDir)\UnityEngine.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$(ManagedDir)\UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="UnityEngine.UI" HintPath="$(ManagedDir)\UnityEngine.UI.dll" Private="false" />
    <Reference Include="Unity.TextMeshPro" HintPath="$(ManagedDir)\Unity.TextMeshPro.dll" Private="false" />
  </ItemGroup>
</Project>
```

> 如果用到 UI 布局（`TextAnchor` 等），需要额外引用 `UnityEngine.TextRenderingModule.dll`。

---

## 2. 分析游戏代码

### 2.1 反编译

```bash
ilspycmd --disable-updatecheck -p -o ./decompiled \
  "C:/Program Files (x86)/Steam/steamapps/common/YAPYAP/yapyap_Data/Managed/Assembly-CSharp.dll"
```

### 2.2 关键类速查

| 类 | 文件 | 职责 |
|---|---|---|
| `GameManager` | `YAPYAP/GameManager.cs` | 回合流程、游戏状态、存档 |
| `SaveManager` | `YAPYAP/SaveManager.cs` | 存档槽读写 |
| `LostItemsTracker` | `YAPYAP/LostItemsTracker.cs` | 撤离失败物品追踪与回收 |
| `AstralPlaneManager` | `AstralPlaneManager.cs` | Quota 结算动画与流程 |
| `UISettings` | `YAPYAP/UISettings.cs` | 设置面板 |
| `UISettingToggle` | `YAPYAP/UISettingToggle.cs` | 开关控件 |
| `UISettingDropdown` | `YAPYAP/UISettingDropdown.cs` | 下拉控件 |
| `UISettingElement<T>` | `YAPYAP/UISettingElement.cs` | 设置控件基类 |
| `Pawn` | `YAPYAP/Pawn.cs` | 玩家角色 |
| `PawnInventory` | `YAPYAP/PawnInventory.cs` | 玩家背包 |
| `DungeonTasks` | `YAPYAP/DungeonTasks.cs` | 任务系统 |
| `GrimoireController` | `YAPYAP/GrimoireController.cs` | 魔法书 |

### 2.3 调用链追踪

YAPYAP 的核心流程通常是 `[Server]` 方法通过 Mirror RPC 串联。追踪时注意：

- `SvMethodName` → 服务端逻辑
- `RpcMethodName` → 客户端通知
- `CmdMethodName` → 客户端到服务端命令

**Quota 失败标准流程：**

```
SvRoundFinish → 掉落物品 → 回收物品 → 进入 AstralPlane
  → SvAstralPlaneSequence(isQuotaMet)
  → RestartGame(false)
  → SvResetGameState(false) → 清空进度
  → SaveGameData
```

**Game Over 流程（Demo 模式/特殊情况）：**

```
SvExecuteGameOver
  → SaveManager.DeleteSlot(CurrentSlot)
  → SaveManager.LoadSlot(CurrentSlot)
  → DelayedGameOverAction → FSM 状态切换
```

---

## 3. Harmony 补丁

### 3.1 基本原则

- **只 Patch 必要的方法**，尽量用 Prefix 修改参数/返回值，少用 Postfix 和 Transpiler
- **永远提供 Finalizer** 清理你自己设置的状态
- **不要全局拦截**关键方法（如 `SaveManager.DeleteSlot`），改用调用点级拦截
- 注意 Harmony 补丁顺序：Prefix → 原方法 → Postfix，任何一步抛异常都会触发 Finalizer

### 3.2 常用模式

**Prefix 修改行为：**

```csharp
[HarmonyPatch(typeof(GameManager), "SomeMethod")]
[HarmonyPrefix]
private static bool SomeMethodPrefix(GameManager __instance, bool someArg)
{
    if (shouldOverride)
    {
        // 自定义逻辑
        return false; // 跳过原方法
    }
    return true; // 执行原方法
}
```

**Transpiler 替换调用点：**

当需要跳过方法内某一个特定调用，但不影响其他调用时使用。例如：只跳过 `SvExecuteGameOver` 中的 `DeleteSlot`，但保留手动删档功能。

```csharp
[HarmonyPatch(typeof(GameManager), "SvExecuteGameOver")]
[HarmonyTranspiler]
private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    foreach (var instruction in instructions)
    {
        if (instruction.Calls(deleteSlotMethod))
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = scopedWrapperMethod; // 替换为条件包装
        }
        yield return instruction;
    }
}
```

> **为什么不用 Prefix 拦截 `DeleteSlot`？**  
> FrogDataLib 等模组在 `DeleteSlot` 上有 Postfix。Prefix 返回 false 只跳过原方法，Postfix 仍然执行。用 Transpiler 替换调用点可以彻底阻止进入该方法。

### 3.3 反射访问私有成员

```csharp
using HarmonyLib;

// 字段
var field = AccessTools.Field(typeof(SomeType), "_privateField");
field.SetValue(instance, newValue);

// 方法
var method = AccessTools.Method(typeof(SomeType), "PrivateMethod");
method.Invoke(instance, new object[] { arg1, arg2 });
```

---

## 4. 存档交互

### 4.1 存档路径

```
Application.persistentDataPath/saves/save_slot_{n}.json
```

YAPYAP 使用 JSON 格式，`SaveManager` 封装读写。

### 4.2 关键键名

| 键 | 含义 |
|---|---|
| `GAME.GOLD` | 金币 |
| `GAME.ROUND_CURRENT` | 当前夜数 |
| `GAME.ROUND_TOTAL` | 总回合数 |
| `GAME.CURRENT_SCORE` | Session 分数 |
| `GAME.QUOTA_CURRENT` | 当前 Quota 目标 |
| `GAME.QUOTA_SESSIONS_COMPLETED` | 已完成 Quota 层数 |
| `GAME.POTION_ROOM` | 地下室解锁状态 |
| `GAME.GRIMOIRE.*` | 魔法书进度 |
| `HUB.PROPS.COUNT` | Hub 物品数量 |
| `HUB.PROPS.{n}.ASSET` | Hub 物品资产 ID |
| `HUB.BASEMENT_DOOR_OPENED` | 地下室门状态 |

### 4.3 存档操作安全规范

- **备份优先**：任何可能导致进度丢失的操作前，先调用 `SaveGameData()` 写入磁盘再复制备份
- **只改必要字段**：不要直接操作 JSON，通过 `SaveManager.SetInt/SetString` 等方法
- **注意 Mirror 同步**：`Network*` 属性写入会自动同步到客户端

---

## 5. 设置页注入

### 5.0 推荐：使用 Native Settings UI Lib

手写设置页注入涉及模板克隆、标签定位、双语刷新、PlayerPrefs 隔离、游戏内外区分等多个易错点。**推荐直接使用 [Native Settings UI Lib](https://thunderstore.io/c/yapyap/p/XiaohaiMod/Native_Settings_UI_Lib/)**（XiaohaiMod），它把这些全部封装成了简洁 API：

```csharp
var tab = NativeSettingsUI.RegisterTab(
    guid: "com.yourmod.settings",
    title: new LocalText("TAB", "我的设置", "My Settings"),
    showInGame: true
);

tab.CreateToggle(
    id: "Toggle_Enabled",
    settingKey: "com.yourmod.Enabled",
    title: new LocalText("TOGGLE", "启用", "Enabled"),
    initialValue: true,
    onChanged: v => enabled = v,
    showInGame: true
);

tab.CreateDropdownString(
    id: "Dropdown_Quality",
    settingKey: "com.yourmod.Quality",
    title: new LocalText("QUALITY", "品质", "Quality"),
    options: new[] { "Low", "Medium", "High" },
    initialValue: "Medium",
    onChanged: v => quality = v,
    showInGame: true
);
```

**支持的控件：** `CreateButton`, `CreateLabel`, `CreateToggle`, `CreateDropdownString`, `CreateSliderInt`, `CreateInputField`

**核心优势：**
- `LocalText` 提供中/英双语，语言切换时自动刷新所有绑定文字
- `UiRef<T>` 延迟引用，处理 UI 异步创建
- `settingKey` 参数内置 PlayerPrefs 持久化，无需手动管理
- `showInGame` 区分游戏内和主菜单设置界面
- 解决了克隆控件的标签定位、残留文字等问题

> 以下 5.1–5.6 为手写注入原理，使用 Native Settings UI Lib 时无需关心。但理解原理有助于调试。

### 5.1 注入时机

在 `UISettings.Initialise` 的 Prefix 中注入。此时 `sections` 数组已初始化、原生控件模板已加载。

```csharp
[HarmonyPatch(typeof(UISettings), nameof(UISettings.Initialise))]
[HarmonyPrefix]
private static void InitialisePrefix(UISettings __instance)
{
    SettingsUiInjector.TryInject(__instance);
}
```

### 5.2 克隆原生控件

1. 选择一个已存在的 `SettingsSection` 作为模板
2. `Instantiate` 其 `SectionObj` 和 `TabButton`
3. 清除克隆体的子对象和 MonoBehaviour
4. 扩展 `sections` 数组

### 5.3 控件隔离（重要）

克隆的原生控件继承了模板的 `settingKey`，必须：

```csharp
control.SetSettingKey(string.Empty);           // 清空原版键
control.OnSettingChanged.RemoveAllListeners(); // 移除原版回调
RemoveLocalisation(clone);                     // 移除本地化组件
```

然后绑定到 BepInEx 的 `ConfigEntry`，而不是 PlayerPrefs。

### 5.4 标签文字设置（关键坑）

**不要用名字匹配找标签组件**——不同 Mod 的控件模板命名不同。正确做法：

```csharp
// 排除法：反射拿到已知的值显示组件，其余 TMP_Text 就是标签
TMP_Text valueLabel = AccessTools.Field(typeof(UISettingElement<T>), "valueLabel")
    ?.GetValue(control) as TMP_Text;
TMP_Dropdown tmpDropdown = AccessTools.Field(typeof(UISettingDropdown), "dropdown")
    ?.GetValue(control) as TMP_Dropdown;
TMP_Text captionText = tmpDropdown?.captionText;

TMP_Text labelTarget = clone.GetComponentsInChildren<TMP_Text>(true)
    .FirstOrDefault(text => text != valueLabel && text != captionText);
```

### 5.5 防止重复注入

```csharp
if (sections.Any(s => s.SectionObj?.name == SectionName))
    return; // 已注入
```

### 5.6 多语言

```csharp
private static string Localized(string chinese, string english)
{
    return Application.systemLanguage == SystemLanguage.ChineseSimplified
        ? chinese : english;
}
```

---

## 6. 游戏设置系统解析

理解原版设置系统有助于正确注入 Mod 设置，避免破坏原版行为。

### 6.1 UISettings 架构

```
UISettings (MonoBehaviour)
├── SettingsSection[] sections    ← 页签数组，每个 Section 包含：
│   ├── GameObject SectionObj     ← 页签对应内容面板
│   ├── Button TabButton          ← 页签按钮
│   └── UIFader Indictor          ← 页签选中指示器
├── Initialise()                  ← 外部调用入口
│   ├── ApplyAllSettings()        ← 把所有设置值生效
│   └── InitSections()            ← 绑定页签点击 → ChangeSection
├── Awake()                       ← 注册输入/状态监听，绑定各控件回调
└── ChangeSection(SettingsSection) ← 切换显示，控制 Indicator 动画
```

**生命周期：**

```
场景加载 → Awake() → Initialise() → InitSections() → ChangeSection(sections[0])
```

- `Awake` 绑定所有原生控件的 `OnSettingChanged` 回调到 `SetXxx` 方法
- `Initialise` 被外部调用（如 `YapFsm`），执行 `ApplyAllSettings` + `InitSections`
- `InitSections` 为每个 Section 的 TabButton 绑定 `ChangeSection`，并默认打开第一个

### 6.2 SettingsSection 结构

```csharp
[Serializable]
public class SettingsSection
{
    public GameObject SectionObj;   // 内容面板（包含所有设置控件）
    public Button TabButton;       // 页签按钮
    public UIFader Indictor;       // 选中高亮指示器
}
```

每个 Section 对应设置面板的一个页签。控件放在 `SectionObj` 的子孙节点中。`sections` 是 `[SerializeField] private` 数组，需要在 Editor 中预设。

### 6.3 UISettingElement<T> 基类

所有设置控件继承自：

```csharp
public abstract class UISettingElement<T> : MonoBehaviour
{
    protected string settingKey;            // PlayerPrefs 键名
    protected TMP_Text valueLabel;          // 值显示文本
    public UnityEvent<T> OnSettingChanged;  // 值变更事件

    // 核心方法
    public void SetSettingKey(string key);  // 设置 PlayerPrefs 键
    public void SetValue(T newValue);       // 设置值 → ApplyValue + Save + 触发事件
    public void SetValueNoNotify(T newValue); // 设置值 → ApplyValue，不触发事件
    public void DisplayValue(T value);      // 仅刷新显示
    public abstract void Load();            // 从 PlayerPrefs 读取
    public virtual void Save();             // 写入 PlayerPrefs
    protected virtual void Initialize();    // Awake 时调用，执行 Load
}
```

**数据流：**

```
用户操作 → OnSettingChanged 触发
  → 原生 Awake 中绑定的 SetXxx 回调（如 SetMasterVolume）
  → 立即生效 + PlayerPrefs.Save()

启动时：
  → Awake → Initialize → Load → PlayerPrefs.GetXxx(settingKey)
  → ApplyValue → 更新显示
  → Initialise → ApplyAllSettings → 所有设置生效
```

### 6.4 UISettingToggle（开关控件）

```csharp
public class UISettingToggle : UISettingElement<bool>
{
    private MultiGraphicToggle toggle;     // Unity UI Toggle 封装
    private LocalisedTMP localisedLabelText; // 标签本地化组件
    private bool defaultValue;

    protected override void Awake()
    {
        toggle.onValueChanged.AddListener(base.SetValue);
        base.Awake();  // → Initialize → Load
    }

    public override void Load()
    {
        currentValue = PlayerPrefs.GetInt(settingKey, defaultValue ? 1 : 0) == 1;
    }

    public override void Save()
    {
        PlayerPrefs.SetInt(settingKey, currentValue ? 1 : 0);
    }
}
```

**关键点：**
- `Awake` 中绑定 `toggle.onValueChanged → base.SetValue`，用户点击触发完整流程
- `SetValue` → `SetValueInternal` → `ApplyValue` → `UpdateValueLabel` + `Save` + `OnSettingChanged`
- `localisedLabelText` 负责标签多语言，克隆后需销毁该组件并手动设文字

### 6.5 UISettingDropdown（下拉控件）

```csharp
public class UISettingDropdown : UISettingElement<int>
{
    private TMP_Dropdown dropdown;  // Unity TMP_Dropdown
    private int defaultValue;

    protected override void Awake()
    {
        dropdown.onValueChanged.AddListener(base.SetValue);
        base.Awake();
        // 注册 EventTrigger 处理手柄导航
    }

    public void PopulateOptions(List<string> options)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
    }

    public override void Load()
    {
        currentValue = PlayerPrefs.GetInt(settingKey, GetDefaultValue());
    }

    protected override void UpdateValueLabel(int value)
    {
        if (value < dropdown.options.Count && valueLabel != null)
            valueLabel.text = dropdown.options[value].text;
    }
}
```

**关键点：**
- `dropdown.captionText` 是下拉按钮上显示的当前选项文字
- `valueLabel`（继承自基类）是额外的一个值显示 TMP_Text
- `PopulateOptions` 需要在初始化时调用（原生在 `Awake` 中），克隆后必须手动调用
- 下拉的 `UpdateValueLabel` 依赖 `dropdown.options` 已填充

### 6.6 PlayerPrefs 与 Mod 配置的隔离

**原版控件数据流：**

```
控件操作 → SetValue → Save → PlayerPrefs.SetXxx(settingKey, value)
启动加载 → Initialize → Load → PlayerPrefs.GetXxx(settingKey, defaultValue)
```

**Mod 注入控件必须切断这个链路：**

```csharp
// 1. 清空键名，阻止 Save/Load 访问 PlayerPrefs
control.SetSettingKey(string.Empty);

// 2. 移除原版回调
control.OnSettingChanged.RemoveAllListeners();

// 3. 绑定到 BepInEx ConfigEntry
control.OnSettingChanged.AddListener(value => {
    Plugin.MyConfig.Value = value;
    // ConfigEntry 自动持久化到 .cfg 文件
});

// 4. 初始化显示
control.SetValueNoNotify(Plugin.MyConfig.Value);
control.DisplayValue(Plugin.MyConfig.Value);
```

> **不这样做会怎样？** 克隆控件的 `settingKey` 继承自模板，会把 Mod 的值写入原版 PlayerPrefs，覆盖玩家的游戏设置。多个 Mod 克隆同一模板还会互相覆盖。

### 6.7 控件模板选择

注入时搜索场景中已有的控件作为模板：

```csharp
// 找一个非本 Mod 创建的控件当模板
UISettingToggle toggleTemplate = FindObjectsByType<UISettingToggle>(
    FindObjectsInactive.Include, FindObjectsSortMode.None)
    .FirstOrDefault(c => c != null && !c.name.StartsWith("SaveGuard"));

UISettingDropdown dropdownTemplate = FindObjectsByType<UISettingDropdown>(
    FindObjectsInactive.Include, FindObjectsSortMode.None)
    .FirstOrDefault(c => c != null && !c.name.StartsWith("SaveGuard"));
```

- `FindObjectsInactive.Include` 确保非激活页签里的控件也能找到
- 排除自己创建的控件，防止取到上次注入的残留
- 如果有多个 Mod 注入，模板可能是其他 Mod 的控件

### 6.8 InitSections 注入时机

```csharp
[HarmonyPatch(typeof(UISettings), nameof(UISettings.Initialise))]
[HarmonyPrefix]
private static void InitialisePrefix(UISettings __instance)
{
    // Initialise 在 Awake 之后调用
    // 此时 sections 已初始化，原生控件 Awake 已跑完
    // InitSections 还未调用，可以安全扩展 sections 数组
    TryInject(__instance);
}
```

**为什么不在 Awake 里注入？** `Awake` 先于 `Initialise`，此时 `sections` 已填充但 `InitSections` 还没跑。在 `Initialise` 的 Prefix 注入可以确保 `InitSections` 执行时包含新 Section。

### 6.9 控件值变更的生效机制

原生控件通过 `OnSettingChanged` 驱动设置生效：

```csharp
// UISettings.Awake() 中的绑定示例
masterVolumeSetting.OnSettingChanged.AddListener(SetMasterVolume);

// 回调方法
private void SetMasterVolume(float value) { /* 设置 AudioMixer */ }
private void SetFov(float value)          { /* 调整摄像机 FOV */ }
private void SetScreenMode(int mode)      { /* 切换全屏/窗口 */ }
```

Mod 注入时可同样利用此机制：绑定自己的回调到 `OnSettingChanged`，在回调中同时更新 BepInEx 配置和应用设置。

---

## 7. 配置管理

### 6.1 BepInEx 配置

```csharp
ConfigEntry<bool> MyToggle = Config.Bind(
    "Category",           // Section 名
    "KeyName",            // 键名
    true,                 // 默认值
    "Description text"    // 描述
);

// 限定可选值
Config.Bind("Category", "Key", 100,
    new ConfigDescription("描述", new AcceptableValueList<int>(0, 25, 50, 75, 100)));

// 数值范围
Config.Bind("Category", "Key", 5,
    new ConfigDescription("描述", new AcceptableValueRange<int>(1, 20)));
```

### 6.2 配置文件路径

```
BepInEx/config/{GUID}.cfg
```

GUID 格式：`com.author.modname`

### 6.3 游戏内只暴露必要配置

复杂/高级选项放在配置文件里，游戏内只留最常用的 2-3 个开关/下拉。避免界面臃肿。

---

## 8. 兼容性

### 7.1 版本守卫

对 `Assembly-CSharp.dll` 做 SHA-256 校验，游戏更新后自动停止补丁：

```csharp
private static bool Validate(string expectedHash)
{
    string path = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        "Assembly-CSharp.dll");
    // 实际应通过反射获取加载的程序集路径
    using var sha = SHA256.Create();
    var hash = Convert.ToHexStringLower(
        sha.ComputeHash(File.ReadAllBytes(path)));
    return hash == expectedHash;
}
```

### 7.2 与其他 Mod 共存

- **FrogDataLib**：在 `SaveManager.DeleteSlot`、`LoadSlot`、`WriteSlot` 上有 Postfix。如果不想触发其逻辑，用 Transpiler 替换调用点，而不是 Prefix 返回 false。
- **多个 Mod 注入设置页**：检查 `SectionObj.name` 避免重复注入。
- **More Inventory Slots**：修改了 `PawnInventory` 的槽位数量，相关补丁注意不冲突。

---

## 9. 测试

### 8.1 策略层单元测试

独立于 Unity 的纯逻辑测试，测试配置归一化、条件判断等：

```xml
<!-- Test.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../SaveGuardPolicy.cs" Link="SaveGuardPolicy.cs" />
  </ItemGroup>
</Project>
```

### 8.2 运行时测试清单

- [ ] 模组正常加载，日志无 Harmony 错误
- [ ] 设置页正确注入，无重复
- [ ] 控件标签文字正确，无其他 Mod 残留
- [ ] 配置修改后重启游戏保持
- [ ] 核心功能触发（Quota 失败 / 撤离失败）
- [ ] 多人 Host + Client 联机
- [ ] 与其他常用 Mod 共存无报错
- [ ] 存档文件对比（操作前后）

---

## 10. 构建与打包

### 9.1 构建脚本

```powershell
# Build-Package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet tool restore
    dotnet build .\ModName.csproj -c Release
    dotnet run --project .\ModName.Tests\ModName.Tests.csproj -c Release
    New-Item -ItemType Directory -Force .\dist | Out-Null
    Copy-Item .\bin\Release\ModName.dll .\dist\ModName.dll -Force
    Copy-Item .\CHANGELOG.md .\dist\CHANGELOG.md -Force
    dotnet tool run tcli build
} finally {
    Pop-Location
}
```

### 9.2 Thunderstore 配置

```toml
# thunderstore.toml
[config]
schemaVersion = "0.0.1"

[package]
namespace = "AuthorName"
name = "ModName"
versionNumber = "0.1.0"
description = "Brief description"
websiteUrl = "https://github.com/AuthorName/ModName"
containsNsfwContent = false

[package.dependencies]
BepInEx-BepInExPack = "5.4.2304"
# 可选：如果使用 Native Settings UI Lib 做设置页
# XiaohaiMod-Native_Settings_UI_Lib = "1.0.1"

[build]
icon = "./icon.png"
readme = "./README.md"
outdir = "./build"

[[build.copy]]
source = "./dist"
target = ""

[publish]
repository = "https://thunderstore.io"
communities = ["yapyap"]
[publish.categories]
yapyap = ["mods", "host", "qol"]
```

### 9.3 图标规格

- 尺寸：256 × 256
- 格式：PNG

### 9.4 本地测试安装

将 `ModName.dll` 复制到：
```
%AppData%/Thunderstore Mod Manager/DataFolder/Yapyap/profiles/Default/BepInEx/plugins/ModName/
```

启动游戏，带控制台查看 BepInEx 日志。

---

## 11. README 规范

### 10.1 必要内容

- 一句话说明模组做什么
- 功能列表
- 游戏内设置说明（如有）
- 安装方式
- 联机说明（Host / Client 要求）
- 配置文件说明
- 已知限制

### 10.2 风格建议

- 双语（中文 + English），文件顶部锚点跳转
- 用 blockquote 和表格增强可读性
- 不用 emoji 图标（跨平台渲染不一致）
- 不用句号结尾（中文列表项）
- 不用作者署名放在 README 正文（Thunderstore 已有 `namespace` 标识）

---

## 12. 常见问题

| 问题 | 原因 | 解决 |
|---|---|---|
| 克隆控件显示其他 Mod 文字 | 模板残留，名字匹配失败 | 排除法定位标签组件 |
| `DeleteSlot` 拦截不完整 | 第三方 Postfix 仍执行 | Transpiler 替换调用点 |
| `CS1525: 表达式项"ref"无效` | SDK 捡到分析目录的反编译文件 | csproj 加 `Compile Remove` |
| 构建时 `AssemblyInfo` 重复 | 工作区多个项目的 `obj/` 冲突 | 清理 `obj/bin`，排除其他项目 |
| 设置页重复注入 | 未检查已存在 | 检查 `SectionObj.name` |
| 游戏更新后静默破坏存档 | 补丁假设未更新 | Build Guard 哈希校验 |

---

*最后更新：2026-07-25 · 基于 SaveGuard v0.1.1 开发过程编写*
