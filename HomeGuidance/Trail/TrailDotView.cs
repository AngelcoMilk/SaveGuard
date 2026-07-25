using UnityEngine;

namespace HomeGuidance.Trail;

public sealed class TrailDotView
{
    private readonly GameObject _gameObject;
    private readonly Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private float _phase;

    private TrailDotView(GameObject go, Renderer renderer)
    {
        _gameObject = go;
        _renderer = renderer;
        _propertyBlock = new MaterialPropertyBlock();
    }

    public static TrailDotView Create()
    {
        // Use a simple sphere primitive for trail dots
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "HomeGuidanceTrailDot";
        go.transform.localScale = Vector3.one * 0.15f;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Remove collider
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Create a simple bright material
            var mat = new Material(Shader.Find("Sprites/Default"));
            if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(0.3f, 0.8f, 1f, 1f);
            renderer.sharedMaterial = mat;
        }

        return new TrailDotView(go, renderer);
    }

    public void SetPosition(Vector3 position)
    {
        _gameObject.transform.position = position;
    }

    public void SetPhase(float phase)
    {
        _phase = phase;
    }

    public void Animate(float now, float speed, float frequency)
    {
        float phase = (now * speed + _phase * frequency) % 1f;
        float alpha = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f));

        if (_renderer != null && _propertyBlock != null)
        {
            _renderer.GetPropertyBlock(_propertyBlock);
            var c = _renderer.sharedMaterial.color;
            c.a = 0.3f + alpha * 0.7f;
            _propertyBlock.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        // Scale pulse
        float scale = 0.12f + alpha * 0.06f;
        _gameObject.transform.localScale = Vector3.one * scale;
    }

    public void SetVisible(bool visible)
    {
        _gameObject.SetActive(visible);
    }

    public void Destroy()
    {
        if (_gameObject != null)
            Object.Destroy(_gameObject);
    }
}
