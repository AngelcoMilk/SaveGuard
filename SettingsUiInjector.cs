using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YAPYAP;

namespace SaveGuard;

internal static class SettingsUiInjector
{
    private const string SectionName = "SaveGuard_Section";
    private const string TabName = "SaveGuard_Tab";
    private static readonly FieldInfo SectionsField = AccessTools.Field(typeof(UISettings), "sections");
    private static readonly int[] RecoveryOptions = { 0, 25, 50, 75, 100 };

    internal static void TryInject(UISettings settings)
    {
        if (settings == null || SectionsField?.GetValue(settings) is not UISettings.SettingsSection[] sections || sections.Length == 0)
        {
            return;
        }

        if (sections.Any(section => section?.SectionObj != null && section.SectionObj.name == SectionName))
        {
            return;
        }

        UISettings.SettingsSection template = sections
            .FirstOrDefault(s => s?.SectionObj != null && s.TabButton != null);
        if (template?.SectionObj == null || template.TabButton == null)
        {
            Plugin.Log?.LogWarning("Unable to inject SaveGuard settings: no usable settings section template was found.");
            return;
        }

        GameObject sectionObject = UnityEngine.Object.Instantiate(template.SectionObj, template.SectionObj.transform.parent);
        sectionObject.name = SectionName;
        ClearSection(sectionObject);
        Transform content = BuildScrollContent(sectionObject);

        GameObject tabObject = UnityEngine.Object.Instantiate(template.TabButton.gameObject, template.TabButton.transform.parent);
        tabObject.name = TabName;
        Button tabButton = tabObject.GetComponent<Button>();
        UIFader indicator = tabObject.GetComponentInChildren<UIFader>(true);
        if (tabButton == null || indicator == null)
        {
            UnityEngine.Object.Destroy(sectionObject);
            UnityEngine.Object.Destroy(tabObject);
            Plugin.Log?.LogWarning("Unable to inject SaveGuard settings: cloned tab was incomplete.");
            return;
        }

        SetTabLabel(tabObject, Localized("存档守护", "SaveGuard"));
        BuildControls(content);
        sectionObject.SetActive(false);
        tabObject.SetActive(true);

        UISettings.SettingsSection[] extended = new UISettings.SettingsSection[sections.Length + 1];
        Array.Copy(sections, extended, sections.Length);
        extended[sections.Length] = new UISettings.SettingsSection
        {
            SectionObj = sectionObject,
            TabButton = tabButton,
            Indictor = indicator
        };
        SectionsField.SetValue(settings, extended);
        Plugin.Log?.LogInfo("Injected the SaveGuard tab into the in-game Settings panel.");
    }

    private static void ClearSection(GameObject sectionObject)
    {
        foreach (MonoBehaviour behaviour in sectionObject.GetComponents<MonoBehaviour>())
        {
            if (behaviour != null)
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in sectionObject.transform)
        {
            children.Add(child.gameObject);
        }
        foreach (GameObject child in children)
        {
            UnityEngine.Object.DestroyImmediate(child);
        }
    }

    private static Transform BuildScrollContent(GameObject sectionObject)
    {
        GameObject scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(sectionObject.transform, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetFullRect(scrollRectTransform, new Vector2(-40f, -40f));

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetFullRect(viewport, Vector2.zero);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 12f;
        layout.padding = new RectOffset(30, 30, 25, 30);
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private static void BuildControls(Transform content)
    {
        TMP_FontAsset font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(text => text != null && text.font != null)?.font
            ?? TMP_Settings.defaultFontAsset;

        CreateHeader(content, font);

        UISettingToggle toggleTemplate = UnityEngine.Object.FindObjectsByType<UISettingToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(control => control != null && !control.gameObject.name.StartsWith("SaveGuard", StringComparison.Ordinal));
        UISettingDropdown dropdownTemplate = UnityEngine.Object.FindObjectsByType<UISettingDropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(control => control != null && !control.gameObject.name.StartsWith("SaveGuard", StringComparison.Ordinal));

        if (toggleTemplate != null)
        {
            CreateToggle(toggleTemplate.gameObject, content, "SaveGuard_ProtectQuotaFailure",
                Localized("任务失败存档", "Keep Save After Mission Failure"),
                Plugin.ProtectQuotaFailure.Value,
                value => Plugin.ProtectQuotaFailure.Value = value,
                font);
        }

        if (dropdownTemplate != null)
        {
            List<string> options = RecoveryOptions.Select(value => value + "%").ToList();
            int initialIndex = FindNearestRecoveryIndex(Plugin.RecoveryPercent.Value);
            CreateDropdown(dropdownTemplate.gameObject, content, "SaveGuard_RecoveryPercent",
                Localized("物品回收率", "Item Recovery Rate"),
                options,
                initialIndex,
                index => Plugin.RecoveryPercent.Value = RecoveryOptions[Mathf.Clamp(index, 0, RecoveryOptions.Length - 1)],
                font);
        }
    }

    private static void CreateHeader(Transform parent, TMP_FontAsset font)
    {
        GameObject titleObject = new GameObject("SaveGuard_Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObject.transform.SetParent(parent, false);
        TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
        title.text = Localized("存档守护 (SaveGuard)", "SaveGuard");
        title.fontSize = 30f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        if (font != null) title.font = font;
        titleObject.GetComponent<LayoutElement>().preferredHeight = 55f;
    }

    private static void CreateToggle(GameObject template, Transform parent, string name, string label, bool initialValue, UnityAction<bool> callback, TMP_FontAsset font)
    {
        GameObject clone = UnityEngine.Object.Instantiate(template, parent);
        clone.name = name;
        clone.SetActive(true);
        UISettingToggle control = clone.GetComponent<UISettingToggle>();
        if (control == null) return;
        control.SetSettingKey(string.Empty);
        control.OnSettingChanged.RemoveAllListeners();
        RemoveLocalisation(clone);

        TMP_Text valueLabel = AccessTools.Field(typeof(UISettingElement<bool>), "valueLabel")?.GetValue(control) as TMP_Text;
        TMP_Text labelTarget = clone.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text != valueLabel);
        if (labelTarget != null)
        {
            labelTarget.text = label;
            if (font != null) labelTarget.font = font;
        }

        control.SetValueNoNotify(initialValue);
        control.DisplayValue(initialValue);
        control.OnSettingChanged.AddListener(callback);
    }

    private static void CreateDropdown(GameObject template, Transform parent, string name, string label, List<string> options, int initialValue, UnityAction<int> callback, TMP_FontAsset font)
    {
        GameObject clone = UnityEngine.Object.Instantiate(template, parent);
        clone.name = name;
        clone.SetActive(true);
        UISettingDropdown control = clone.GetComponent<UISettingDropdown>();
        if (control == null) return;
        control.SetSettingKey(string.Empty);
        control.OnSettingChanged.RemoveAllListeners();
        RemoveLocalisation(clone);

        TMP_Text valueLabel = AccessTools.Field(typeof(UISettingElement<int>), "valueLabel")?.GetValue(control) as TMP_Text;
        TMP_Dropdown tmpDropdown = AccessTools.Field(typeof(UISettingDropdown), "dropdown")?.GetValue(control) as TMP_Dropdown;
        TMP_Text captionText = tmpDropdown?.captionText;

        TMP_Text labelTarget = clone.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text != valueLabel && text != captionText);
        if (labelTarget != null)
        {
            labelTarget.text = label;
            if (font != null) labelTarget.font = font;
        }

        control.PopulateOptions(options);
        control.SetValueNoNotify(initialValue);
        control.DisplayValue(initialValue);
        control.OnSettingChanged.AddListener(callback);
    }

    private static void RemoveLocalisation(GameObject gameObject)
    {
        foreach (LocalisedTMP component in gameObject.GetComponentsInChildren<LocalisedTMP>(true))
        {
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }

    private static void SetTabLabel(GameObject tabObject, string label)
    {
        RemoveLocalisation(tabObject);
        foreach (TMP_Text text in tabObject.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
        }
    }

    private static int FindNearestRecoveryIndex(int value)
    {
        int clamped = SaveGuardPolicy.ClampRecoveryPercent(value);
        int bestIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < RecoveryOptions.Length; i++)
        {
            int distance = Math.Abs(RecoveryOptions[i] - clamped);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static string Localized(string chinese, string english)
    {
        SystemLanguage language = Application.systemLanguage;
        return language == SystemLanguage.ChineseSimplified || language == SystemLanguage.ChineseTraditional || language == SystemLanguage.Chinese
            ? chinese
            : english;
    }

    private static void SetFullRect(RectTransform rect, Vector2 sizeDelta)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = sizeDelta;
    }
}
