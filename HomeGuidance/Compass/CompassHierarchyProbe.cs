using UnityEngine;

namespace HomeGuidance.Compass;

/// <summary>
/// Runtime probe for hierarchy discovery. Runs on first attach to dump the
/// full RenderTargetRoot hierarchy so a CompassHierarchyProfile can be frozen.
/// </summary>
public static class CompassHierarchyProbe
{
    public static void DumpHierarchy(YAPYAP.UICompassReferences visualRefs)
    {
        if (visualRefs == null) return;

        var root = visualRefs.RenderTargetRoot;
        GuidanceLog.Info("=== Compass Hierarchy Probe ===");
        GuidanceLog.Info($"RenderTargetRoot: {GetFullPath(root.transform)}");

        DumpRecursive(root.transform, "  ");

        // Also dump key pivots
        DumpPivot("CardinalDirectionsPivot", visualRefs.CardinalDirectionsPivot);
        DumpPivot("TargetObjectPivot", visualRefs.TargetObjectPivot);
        DumpPivot("ElevationIndicatorPivot", visualRefs.ElevationIndicatorPivot);

        GuidanceLog.Info("=== End Compass Hierarchy Probe ===");
    }

    private static void DumpPivot(string name, Transform pivot)
    {
        if (pivot == null) return;
        GuidanceLog.Info($"Pivot {name}: {GetFullPath(pivot)}");
        foreach (var r in pivot.GetComponentsInChildren<Renderer>(true))
        {
            GuidanceLog.Info($"  Renderer: {GetRelativePath(pivot, r.transform)} type={r.GetType().Name} name={r.name} enabled={r.enabled} active={r.gameObject.activeInHierarchy}");
        }
        foreach (var rt in pivot.GetComponentsInChildren<RectTransform>(true))
        {
            GuidanceLog.Info($"  RectTransform: {GetRelativePath(pivot, rt)} anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} sizeDelta={rt.sizeDelta} anchoredPos={rt.anchoredPosition} siblingIdx={rt.GetSiblingIndex()}");
        }
    }

    private static void DumpRecursive(Transform t, string indent)
    {
        if (t == null) return;

        var renderer = t.GetComponent<Renderer>();
        var rt = t.GetComponent<RectTransform>();

        string info = "";
        if (renderer != null) info = $" [Renderer:{renderer.GetType().Name} enabled={renderer.enabled}]";
        if (rt != null) info += $" [RectTransform anchorMin={rt.anchorMin} anchorMax={rt.anchorMax}]";

        GuidanceLog.Debug($"{indent}{t.name}{info}");

        for (int i = 0; i < t.childCount; i++)
            DumpRecursive(t.GetChild(i), indent + "  ");
    }

    private static string GetFullPath(Transform t)
    {
        if (t == null) return "null";
        var path = t.name;
        var parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (root == null || child == null) return "?";
        if (root == child) return ".";
        var path = child.name;
        var cur = child.parent;
        while (cur != null && cur != root)
        {
            path = cur.name + "/" + path;
            cur = cur.parent;
        }
        if (cur == root) return path;
        return GetFullPath(child);
    }
}
