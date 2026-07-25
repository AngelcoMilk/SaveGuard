[assembly: System.Runtime.CompilerServices.CompilationRelaxations(8)]
[assembly: System.Runtime.CompilerServices.RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: System.Diagnostics.Debuggable(System.Diagnostics.DebuggableAttribute.DebuggingModes.Default | System.Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations | System.Diagnostics.DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | System.Diagnostics.DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETStandard,Version=v2.1", FrameworkDisplayName = ".NET Standard 2.1")]
[assembly: System.Reflection.AssemblyCompany("ExtractionTrail")]
[assembly: System.Reflection.AssemblyConfiguration("Debug")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.3.0")]
[assembly: System.Reflection.AssemblyProduct("ExtractionTrail")]
[assembly: System.Reflection.AssemblyTitle("ExtractionTrail")]
[assembly: System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[module: System.Security.UnverifiableCode]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[System.Runtime.CompilerServices.CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : System.Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[System.Runtime.CompilerServices.CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[System.AttributeUsage(System.AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : System.Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace ExtractionTrail
{
	public enum TargetMode
	{
		ExtractionPoint,
		Truck,
		Cart
	}
	[BepInPlugin("com.repo.extractiontrail", "Extraction Trail", "1.5.1")]
	public class Plugin : BaseUnityPlugin
	{
		public static ManualLogSource Log;

		public static ConfigEntry<string> ColorExtraction;

		public static ConfigEntry<string> ColorTruck;

		public static ConfigEntry<string> ColorCart;

		public static ConfigEntry<float> DotSize;

		public static ConfigEntry<float> DotSpeed;

		public static ConfigEntry<int> DotCount;

		public static ConfigEntry<string> ToggleKeyStr;

		public static ConfigEntry<string> NavNextKeyStr;

		public static ConfigEntry<string> NavPrevKeyStr;

		public static ConfigEntry<float> ToastPosX;

		public static ConfigEntry<float> ToastPosY;

		public static KeyCode ToggleKey { get; private set; } = (KeyCode)285;


		public static KeyCode NavNextKey { get; private set; } = (KeyCode)286;


		public static KeyCode NavPrevKey { get; private set; } = (KeyCode)287;


		private void Awake()
		{
			Log = ((BaseUnityPlugin)this).Logger;
			ColorExtraction = ((BaseUnityPlugin)this).Config.Bind<string>("1. Visuals", "ExtractionColor", "#00FFFF", "Hex color.");
			ColorTruck = ((BaseUnityPlugin)this).Config.Bind<string>("1. Visuals", "TruckColor", "#00FF00", "Hex color.");
			ColorCart = ((BaseUnityPlugin)this).Config.Bind<string>("1. Visuals", "CartColor", "#FFFF00", "Hex color.");
			DotSize = ((BaseUnityPlugin)this).Config.Bind<float>("1. Visuals", "DotSize", 0.25f, "Size.");
			DotSpeed = ((BaseUnityPlugin)this).Config.Bind<float>("1. Visuals", "DotSpeed", 4f, "Speed.");
			DotCount = ((BaseUnityPlugin)this).Config.Bind<int>("1. Visuals", "DotCount", 30, "Count.");
			ToggleKeyStr = ((BaseUnityPlugin)this).Config.Bind<string>("2. Controls", "Toggle Key", "F4", "Wł/Wył ścieżkę.");
			NavNextKeyStr = ((BaseUnityPlugin)this).Config.Bind<string>("2. Controls", "Next Target", "F5", "Następny cel (Extraction → Truck → Cart 1 → Cart 2 → ...).");
			NavPrevKeyStr = ((BaseUnityPlugin)this).Config.Bind<string>("2. Controls", "Prev Target", "F6", "Poprzedni cel.");
			ToggleKeyStr.SettingChanged += delegate
			{
				ParseKeys();
			};
			NavNextKeyStr.SettingChanged += delegate
			{
				ParseKeys();
			};
			NavPrevKeyStr.SettingChanged += delegate
			{
				ParseKeys();
			};
			ParseKeys();
			ToastPosX = ((BaseUnityPlugin)this).Config.Bind<float>("3. UI", "ToastPosX", 25f, "X.");
			ToastPosY = ((BaseUnityPlugin)this).Config.Bind<float>("3. UI", "ToastPosY", -243f, "Y.");
			((Component)this).gameObject.AddComponent<ExtractionTrail.TrailManager>();
		}

		private void ParseKeys()
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			if (System.Enum.TryParse<KeyCode>(ToggleKeyStr.Value, ignoreCase: true, out KeyCode result))
			{
				ToggleKey = result;
			}
			if (System.Enum.TryParse<KeyCode>(NavNextKeyStr.Value, ignoreCase: true, out KeyCode result2))
			{
				NavNextKey = result2;
			}
			if (System.Enum.TryParse<KeyCode>(NavPrevKeyStr.Value, ignoreCase: true, out KeyCode result3))
			{
				NavPrevKey = result3;
			}
		}
	}
	public class TrailManager : MonoBehaviour
	{
		private bool isEnabled = false;

		private GameObject[] dots;

		private Material dotMaterial;

		private NavMeshPath currentPath;

		private float pathLength;

		private float[] pathDistances;

		private float offset = 0f;

		private float updateTimer = 0f;

		private int cachedDotCount = 0;

		private bool isDraggingToast = false;

		private Text toastText;

		private RectTransform toastRect;

		private CanvasGroup toastCanvasGroup;

		private float toastTimer = 0f;

		private Vector3[] _smoothedCorners = (Vector3[])(object)new Vector3[0];

		private ExtractionTrail.TargetMode _currentMode = ExtractionTrail.TargetMode.ExtractionPoint;

		private int _cartIndex = 0;

		private System.Collections.Generic.List<MonoBehaviour> _mainCarts = new System.Collections.Generic.List<MonoBehaviour>();

		private void Start()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Expected O, but got Unknown
			currentPath = new NavMeshPath();
			InitializeDots();
			BuildToastUI();
		}

		private void InitializeDots()
		{
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Expected O, but got Unknown
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			if (dots != null)
			{
				GameObject[] array = dots;
				foreach (GameObject val in array)
				{
					if ((Object)(object)val != (Object)null)
					{
						Object.Destroy((Object)(object)val);
					}
				}
			}
			int num = (cachedDotCount = ExtractionTrail.Plugin.DotCount.Value);
			dots = (GameObject[])(object)new GameObject[num];
			Shader val2 = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
			dotMaterial = new Material(val2);
			for (int j = 0; j < num; j++)
			{
				GameObject val3 = GameObject.CreatePrimitive((PrimitiveType)0);
				Object.Destroy((Object)(object)val3.GetComponent<Collider>());
				((Renderer)val3.GetComponent<MeshRenderer>()).material = dotMaterial;
				((Renderer)val3.GetComponent<MeshRenderer>()).shadowCastingMode = (ShadowCastingMode)0;
				val3.transform.localScale = Vector3.one * ExtractionTrail.Plugin.DotSize.Value;
				val3.SetActive(false);
				val3.transform.SetParent(((Component)this).transform);
				dots[j] = val3;
			}
		}

		private void BuildToastUI()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Expected O, but got Unknown
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Expected O, but got Unknown
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			GameObject val = new GameObject("ExtractionTrailToastCanvas");
			Object.DontDestroyOnLoad((Object)(object)val);
			Canvas val2 = val.AddComponent<Canvas>();
			val2.renderMode = (RenderMode)0;
			val2.sortingOrder = 200;
			GameObject val3 = new GameObject("ToastText");
			val3.transform.SetParent(val.transform, false);
			toastRect = val3.AddComponent<RectTransform>();
			toastCanvasGroup = val3.AddComponent<CanvasGroup>();
			toastCanvasGroup.alpha = 0f;
			toastRect.anchorMin = new Vector2(0f, 1f);
			toastRect.anchorMax = new Vector2(0f, 1f);
			toastRect.pivot = new Vector2(0f, 1f);
			toastRect.sizeDelta = new Vector2(400f, 80f);
			toastRect.anchoredPosition = new Vector2(ExtractionTrail.Plugin.ToastPosX.Value, ExtractionTrail.Plugin.ToastPosY.Value);
			toastText = val3.AddComponent<Text>();
			toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			toastText.fontSize = 16;
			toastText.fontStyle = (FontStyle)1;
			((Graphic)toastText).color = Color.white;
			((Shadow)val3.AddComponent<Outline>()).effectColor = Color.black;
		}

		private void ShowToast()
		{
			string text = (isEnabled ? "<color=#00FF00>ON</color>" : "<color=#FF0000>OFF</color>");
			string text2;
			if (_currentMode == ExtractionTrail.TargetMode.Cart)
			{
				int count = _mainCarts.Count;
				text2 = ((count > 0) ? $"CART {_cartIndex + 1}/{count}" : "CART (brak)");
			}
			else
			{
				text2 = _currentMode.ToString().ToUpper();
			}
			toastText.text = "TRAIL: " + text + "\nTARGET: <color=#FFFF00>" + text2 + "</color>";
			toastTimer = 3f;
		}

		private void Update()
		{
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_01da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0266: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)toastRect != (Object)null)
			{
				if (Input.GetKey((KeyCode)308))
				{
					if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(toastRect, Vector2.op_Implicit(Input.mousePosition)))
					{
						isDraggingToast = true;
					}
				}
				else
				{
					isDraggingToast = false;
				}
				if (Input.GetMouseButtonUp(0) && isDraggingToast)
				{
					((ConfigEntryBase)ExtractionTrail.Plugin.ToastPosX).ConfigFile.Save();
					isDraggingToast = false;
				}
				if (isDraggingToast)
				{
					ExtractionTrail.Plugin.ToastPosX.Value = Input.mousePosition.x;
					ExtractionTrail.Plugin.ToastPosY.Value = Input.mousePosition.y - (float)Screen.height;
				}
				toastRect.anchoredPosition = new Vector2(ExtractionTrail.Plugin.ToastPosX.Value, ExtractionTrail.Plugin.ToastPosY.Value);
				toastCanvasGroup.alpha = Mathf.MoveTowards(toastCanvasGroup.alpha, (toastTimer > 0f) ? 1f : 0f, Time.deltaTime * 3f);
				if (toastTimer > 0f)
				{
					toastTimer -= Time.deltaTime;
				}
			}
			if (ExtractionTrail.Plugin.DotCount.Value != cachedDotCount)
			{
				InitializeDots();
			}
			if (Input.GetKeyDown(ExtractionTrail.Plugin.ToggleKey))
			{
				isEnabled = !isEnabled;
				ShowToast();
				if (!isEnabled)
				{
					HideDots();
				}
			}
			if (Input.GetKeyDown(ExtractionTrail.Plugin.NavNextKey))
			{
				AdvanceTarget(1);
				updateTimer = 10f;
			}
			if (Input.GetKeyDown(ExtractionTrail.Plugin.NavPrevKey))
			{
				AdvanceTarget(-1);
				updateTimer = 10f;
			}
			if (!isEnabled)
			{
				return;
			}
			string value = ExtractionTrail.Plugin.ColorExtraction.Value;
			if (_currentMode == ExtractionTrail.TargetMode.Truck)
			{
				value = ExtractionTrail.Plugin.ColorTruck.Value;
			}
			if (_currentMode == ExtractionTrail.TargetMode.Cart)
			{
				value = ExtractionTrail.Plugin.ColorCart.Value;
			}
			Color color = default(Color);
			if (ColorUtility.TryParseHtmlString(value, ref color))
			{
				dotMaterial.color = color;
			}
			updateTimer += Time.deltaTime;
			if (updateTimer > 0.5f)
			{
				updateTimer = 0f;
				if (!IsLevelActive())
				{
					currentPath.ClearCorners();
					HideDots();
				}
				else
				{
					UpdatePath();
				}
			}
			AnimateDots();
		}

		private bool IsLevelActive()
		{
			LevelGenerator @static = ExtractionTrail.Access.GetStatic<LevelGenerator>(typeof(LevelGenerator), "Instance");
			return (Object)(object)@static != (Object)null && ExtractionTrail.Access.Get<bool>(@static, "Generated") && (Object)(object)ExtractionTrail.Access.GetStatic<RoundDirector>(typeof(RoundDirector), "instance") != (Object)null;
		}

		private void AdvanceTarget(int dir)
		{
			RefreshCartList();
			int count = _mainCarts.Count;
			int num = 2 + count;
			int num2 = ((_currentMode != 0) ? ((_currentMode == ExtractionTrail.TargetMode.Truck) ? 1 : (2 + _cartIndex)) : 0);
			num2 = (num2 + dir + num) % num;
			switch (num2)
			{
			case 0:
				_currentMode = ExtractionTrail.TargetMode.ExtractionPoint;
				_cartIndex = 0;
				break;
			case 1:
				_currentMode = ExtractionTrail.TargetMode.Truck;
				_cartIndex = 0;
				break;
			default:
				_currentMode = ExtractionTrail.TargetMode.Cart;
				_cartIndex = num2 - 2;
				break;
			}
			ShowToast();
		}

		private void RefreshCartList()
		{
			_mainCarts.Clear();
			System.Type type = System.Type.GetType("PhysGrabCart, Assembly-CSharp");
			if (type == null || !(Object.FindObjectsOfType(type) is MonoBehaviour[] array))
			{
				return;
			}
			MonoBehaviour[] array2 = array;
			foreach (MonoBehaviour val in array2)
			{
				if (!((Object)(object)((Component)val).gameObject.GetComponent("Item") != (Object)null) && !((Object)(object)((Component)((Component)val).transform.root).GetComponentInChildren<PlayerAvatar>() != (Object)null))
				{
					_mainCarts.Add(val);
				}
			}
			if (_cartIndex >= _mainCarts.Count)
			{
				_cartIndex = Mathf.Max(0, _mainCarts.Count - 1);
			}
		}

		private void UpdatePath()
		{
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0214: Unknown result type (might be due to invalid IL or missing references)
			//IL_021b: Expected O, but got Unknown
			//IL_021e: Unknown result type (might be due to invalid IL or missing references)
			//IL_022b: Unknown result type (might be due to invalid IL or missing references)
			PlayerController @static = ExtractionTrail.Access.GetStatic<PlayerController>(typeof(PlayerController), "instance");
			if ((Object)(object)@static == (Object)null)
			{
				return;
			}
			PlayerAvatar val = ExtractionTrail.Access.Get<PlayerAvatar>(@static, "playerAvatarScript");
			if ((Object)(object)val == (Object)null)
			{
				return;
			}
			Vector3 val2 = ExtractionTrail.Access.Get<Vector3>(val, "LastNavmeshPosition");
			if (val2 == Vector3.zero)
			{
				return;
			}
			RefreshCartList();
			Vector3 pos = Vector3.zero;
			bool flag = false;
			RoundDirector static2 = ExtractionTrail.Access.GetStatic<RoundDirector>(typeof(RoundDirector), "instance");
			switch (_currentMode)
			{
			case ExtractionTrail.TargetMode.ExtractionPoint:
			{
				if ((Object)(object)static2 != (Object)null && ExtractionTrail.Access.Get<bool>(static2, "allExtractionPointsCompleted"))
				{
					flag = TryGetTruckPosition(out pos);
					break;
				}
				ExtractionPoint val4 = (((Object)(object)static2 != (Object)null) ? ExtractionTrail.Access.Get<ExtractionPoint>(static2, "extractionPointCurrent") : null);
				if ((Object)(object)val4 != (Object)null)
				{
					pos = ((Component)val4).transform.position;
					flag = true;
				}
				break;
			}
			case ExtractionTrail.TargetMode.Truck:
				flag = TryGetTruckPosition(out pos);
				break;
			case ExtractionTrail.TargetMode.Cart:
				if (_mainCarts.Count > 0)
				{
					MonoBehaviour val3 = _mainCarts[_cartIndex];
					if ((Object)(object)val3 != (Object)null)
					{
						pos = ((Component)val3).transform.position;
						flag = true;
					}
				}
				break;
			}
			if (flag)
			{
				if (NavMesh.CalculatePath(val2, pos, -1, currentPath) && currentPath.corners.Length >= 2)
				{
					FilterPitCorners();
					currentPath.corners.CopyTo(currentPath.corners, 0);
					if (currentPath.corners.Length >= 2)
					{
						Vector3[] array = SmoothPath(currentPath.corners);
						if (array.Length >= 2)
						{
							NavMeshPath val5 = new NavMeshPath();
							NavMesh.CalculatePath(array[0], array[array.Length - 1], -1, val5);
							_smoothedCorners = array;
							CalculatePathDistancesFromCorners(array);
						}
						else
						{
							currentPath.ClearCorners();
							pathLength = 0f;
						}
					}
					else
					{
						currentPath.ClearCorners();
						pathLength = 0f;
					}
				}
				else
				{
					currentPath.ClearCorners();
					pathLength = 0f;
				}
			}
			else
			{
				currentPath.ClearCorners();
				pathLength = 0f;
			}
		}

		private bool TryGetTruckPosition(out Vector3 pos)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			pos = Vector3.zero;
			LevelGenerator @static = ExtractionTrail.Access.GetStatic<LevelGenerator>(typeof(LevelGenerator), "Instance");
			LevelPoint val = (((Object)(object)@static != (Object)null) ? ExtractionTrail.Access.Get<LevelPoint>(@static, "LevelPathTruck") : null);
			if ((Object)(object)val != (Object)null)
			{
				pos = ((Component)val).transform.position;
				return true;
			}
			return false;
		}

		private void FilterPitCorners()
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Expected O, but got Unknown
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			Vector3[] corners = currentPath.corners;
			System.Collections.Generic.List<Vector3> list = new System.Collections.Generic.List<Vector3>();
			for (int i = 0; i < corners.Length; i++)
			{
				Vector3 val = corners[i] + Vector3.up * 0.3f;
				if (Physics.Raycast(val, Vector3.down, 5f, LayerMask.GetMask(new string[1] { "Default" })) || i == 0)
				{
					list.Add(corners[i]);
					continue;
				}
				break;
			}
			NavMeshPath val2 = new NavMeshPath();
			if (list.Count >= 2)
			{
				NavMesh.CalculatePath(list[0], list[list.Count - 1], -1, val2);
				if (val2.corners.Length >= 2)
				{
					currentPath = val2;
				}
			}
		}

		private void CalculatePathDistances()
		{
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			pathDistances = new float[currentPath.corners.Length];
			pathDistances[0] = 0f;
			pathLength = 0f;
			for (int i = 1; i < currentPath.corners.Length; i++)
			{
				pathLength += Vector3.Distance(currentPath.corners[i - 1], currentPath.corners[i]);
				pathDistances[i] = pathLength;
			}
			_smoothedCorners = currentPath.corners;
		}

		private void CalculatePathDistancesFromCorners(Vector3[] corners)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			pathDistances = new float[corners.Length];
			pathDistances[0] = 0f;
			pathLength = 0f;
			for (int i = 1; i < corners.Length; i++)
			{
				pathLength += Vector3.Distance(corners[i - 1], corners[i]);
				pathDistances[i] = pathLength;
			}
			_smoothedCorners = corners;
		}

		private static Vector3[] SmoothPath(Vector3[] corners)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			if (corners == null || corners.Length <= 2)
			{
				return corners;
			}
			System.Collections.Generic.List<Vector3> list = new System.Collections.Generic.List<Vector3> { corners[0] };
			int num = 0;
			NavMeshHit val = default(NavMeshHit);
			while (num < corners.Length - 1)
			{
				int num2 = num + 1;
				for (int num3 = corners.Length - 1; num3 > num + 1; num3--)
				{
					if (!NavMesh.Raycast(corners[num], corners[num3], ref val, -1))
					{
						num2 = num3;
						break;
					}
				}
				list.Add(corners[num2]);
				num = num2;
			}
			return list.ToArray();
		}

		private void AnimateDots()
		{
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			Vector3[] smoothedCorners = _smoothedCorners;
			if (smoothedCorners == null || smoothedCorners.Length < 2 || pathLength <= 0.5f)
			{
				HideDots();
				return;
			}
			offset += Time.deltaTime * ExtractionTrail.Plugin.DotSpeed.Value;
			float num = pathLength / (float)dots.Length;
			for (int i = 0; i < dots.Length; i++)
			{
				float num2 = ((float)i * num + offset) % pathLength;
				if (num2 < 0f)
				{
					num2 += pathLength;
				}
				dots[i].transform.position = GetPositionOnPath(num2, smoothedCorners);
				dots[i].SetActive(true);
				float num3 = ExtractionTrail.Plugin.DotSize.Value;
				if (num2 < 2f)
				{
					num3 *= num2 / 2f;
				}
				else if (num2 > pathLength - 2f)
				{
					num3 *= (pathLength - num2) / 2f;
				}
				dots[i].transform.localScale = Vector3.one * num3;
			}
		}

		private Vector3 GetPositionOnPath(float dist, Vector3[] pts)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			for (int i = 1; i < pts.Length; i++)
			{
				if (dist <= pathDistances[i])
				{
					float num = pathDistances[i] - pathDistances[i - 1];
					return Vector3.Lerp(pts[i - 1], pts[i], (dist - pathDistances[i - 1]) / num);
				}
			}
			return pts[pts.Length - 1];
		}

		private void HideDots()
		{
			if (dots == null)
			{
				return;
			}
			GameObject[] array = dots;
			foreach (GameObject val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					val.SetActive(false);
				}
			}
		}
	}
	public static class Access
	{
		public static T Get<T>(object inst, string n)
		{
			System.Reflection.FieldInfo fieldInfo = inst?.GetType().GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			return (fieldInfo != null) ? ((T)fieldInfo.GetValue(inst)) : default(T);
		}

		public static T GetStatic<T>(System.Type t, string n)
		{
			System.Reflection.FieldInfo field = t.GetField(n, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			return (field != null) ? ((T)field.GetValue(null)) : default(T);
		}
	}
}
