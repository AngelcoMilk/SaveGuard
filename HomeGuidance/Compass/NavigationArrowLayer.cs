using UnityEngine;
using UnityEngine.UI;

namespace HomeGuidance.Compass;

public sealed class NavigationArrowLayer
{
    private readonly GameObject _gameObject;
    private readonly RectTransform _rectTransform;
    private readonly Image _image;
    private float _currentZ;
    private float _angularVelocity;
    private Color _currentColor = Color.white;

    private NavigationArrowLayer(GameObject go, RectTransform rt, Image img)
    {
        _gameObject = go;
        _rectTransform = rt;
        _image = img;
    }

    public static NavigationArrowLayer Create(Transform parent, CompassHierarchyProfile profile)
    {
        const string arrowName = "HomeGuidanceArrow";

        // Check for existing
        var existing = parent.Find(arrowName);
        if (existing != null)
        {
            GuidanceLog.Warning("NavigationArrowLayer already exists; reusing.");
            var eImg = existing.GetComponent<Image>();
            if (eImg == null) eImg = existing.gameObject.AddComponent<Image>();
            return new NavigationArrowLayer(existing.gameObject, existing as RectTransform, eImg);
        }

        var go = new GameObject(arrowName);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = profile.AnchorMin;
        rt.anchorMax = profile.AnchorMax;
        rt.pivot = profile.Pivot;
        rt.sizeDelta = profile.SizeDelta;
        rt.anchoredPosition = profile.AnchoredPosition;
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;

        if (profile.ArrowSiblingIndex >= 0 && profile.ArrowSiblingIndex < parent.childCount)
            rt.SetSiblingIndex(profile.ArrowSiblingIndex);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.white;

        // Create a simple procedural triangle sprite
        img.sprite = CreateTriangleSprite();

        return new NavigationArrowLayer(go, rt, img);
    }

    public void SetAngle(float signedAngle)
    {
        _currentZ = Mathf.SmoothDampAngle(_currentZ, -signedAngle, ref _angularVelocity,
            Plugin.ModConfig.ArrowSmoothTime.Value);
        _rectTransform.localEulerAngles = new Vector3(0f, 0f, _currentZ);
    }

    public void SetColor(Color color)
    {
        _currentColor = Color.Lerp(_currentColor, color, 10f * Time.deltaTime);
        _image.color = _currentColor;
    }

    public void SetVisible(bool visible)
    {
        _gameObject.SetActive(visible);
    }

    public void SetHidden()
    {
        _gameObject.SetActive(false);
    }

    public void Destroy()
    {
        if (_gameObject != null)
            Object.Destroy(_gameObject);
    }

    private static Sprite CreateTriangleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color32[size * size];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(0, 0, 0, 0);

        // Draw a filled upward-pointing triangle (arrow)
        float cx = size / 2f;
        float cy = size / 2f;
        float r = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                // Point up: tip at (cx, cy+r), base at (cx-r, cy-r/2) and (cx+r, cy-r/2)
                float tipX = 0f, tipY = r;
                float leftX = -r * 0.7f, leftY = -r * 0.5f;
                float rightX = r * 0.7f, rightY = -r * 0.5f;

                if (PointInTriangle(dx, dy, tipX, tipY, leftX, leftY, rightX, rightY))
                {
                    colors[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }
        }

        tex.SetPixels32(colors);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var rect = new Rect(0, 0, size, size);
        return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 64f);
    }

    private static bool PointInTriangle(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        float d1 = Sign(px, py, x1, y1, x2, y2);
        float d2 = Sign(px, py, x2, y2, x3, y3);
        float d3 = Sign(px, py, x3, y3, x1, y1);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;

        return !(hasNeg && hasPos);
    }

    private static float Sign(float px, float py, float x1, float y1, float x2, float y2)
    {
        return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
    }
}
