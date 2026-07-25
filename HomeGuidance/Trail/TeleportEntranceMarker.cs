using UnityEngine;

namespace HomeGuidance.Trail;

public sealed class TeleportEntranceMarker
{
    private readonly GameObject _gameObject;
    private readonly Renderer _renderer;

    private TeleportEntranceMarker(GameObject go, Renderer renderer)
    {
        _gameObject = go;
        _renderer = renderer;
    }

    public static TeleportEntranceMarker Create()
    {
        // Create a small purple pillar/cylinder to mark teleporter entrance
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "HomeGuidanceTeleportMarker";
        go.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var mat = new Material(Shader.Find("Sprites/Default"));
            if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(0.7f, 0.3f, 1f, 1f);
            renderer.sharedMaterial = mat;
        }

        go.SetActive(false);
        return new TeleportEntranceMarker(go, renderer);
    }

    public void ShowAt(Vector3 position)
    {
        _gameObject.transform.position = position;
        _gameObject.SetActive(true);

        // Pulsing animation via scale
        float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 3f);
        _gameObject.transform.localScale = new Vector3(0.3f * pulse, 1.5f, 0.3f * pulse);
    }

    public void Hide()
    {
        _gameObject.SetActive(false);
    }

    public void Destroy()
    {
        if (_gameObject != null)
            Object.Destroy(_gameObject);
    }
}
