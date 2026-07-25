using UnityEngine;

namespace HomeGuidance.Compass;

/// <summary>
/// Frozen hierarchy profile for a specific build.
/// Populated manually from CompassHierarchyProbe output.
/// Published builds must have exactly one profile matching the assembly hash.
/// </summary>
public sealed class CompassHierarchyProfile
{
    public string GameBuildHash;

    /// <summary>Relative paths from RenderTargetRoot of Renderers to disable.</summary>
    public string[] DirectionRendererAllowList;

    /// <summary>Relative paths from RenderTargetRoot that must NOT be modified.</summary>
    public string[] PreservedRendererDenyList;

    /// <summary>Relative path from _visualRefs game object to arrow parent RectTransform.</summary>
    public string ArrowParentRelativePath;

    /// <summary>Sibling index to insert arrow at.</summary>
    public int ArrowSiblingIndex;

    public Vector2 AnchorMin;
    public Vector2 AnchorMax;
    public Vector2 Pivot;
    public Vector2 SizeDelta;
    public Vector2 AnchoredPosition;

    /// <summary>Relative path to the Mask (RectMask2D/Mask) component owner.</summary>
    public string MaskOwnerRelativePath;

    /// <summary>
    /// CURRENT PLACEHOLDER. Must be replaced with actual hierarchy probe results
    /// from the first run on the target build before publishing.
    /// </summary>
    public static CompassHierarchyProfile CreatePlaceholder()
    {
        return new CompassHierarchyProfile
        {
            GameBuildHash = SupportedGameBuilds.AllowedHashes[0],
            DirectionRendererAllowList = new string[0],
            PreservedRendererDenyList = new string[0],
            ArrowParentRelativePath = "",
            ArrowSiblingIndex = 0,
            AnchorMin = new Vector2(0.5f, 0.5f),
            AnchorMax = new Vector2(0.5f, 0.5f),
            Pivot = new Vector2(0.5f, 0.5f),
            SizeDelta = new Vector2(64f, 64f),
            AnchoredPosition = Vector2.zero,
            MaskOwnerRelativePath = ""
        };
    }

    public bool ValidatesAgainst(Transform renderTargetRoot)
    {
        if (renderTargetRoot == null) return false;

        // Verify allow list entries
        foreach (var path in DirectionRendererAllowList)
        {
            var t = renderTargetRoot.Find(path);
            if (t == null) return false;
            var r = t.GetComponent<Renderer>();
            if (r == null) return false;
        }

        // Verify deny list entries exist (must be present to ensure we don't miss them)
        foreach (var path in PreservedRendererDenyList)
        {
            var t = renderTargetRoot.Find(path);
            if (t == null) return false;
        }

        return true;
    }
}
