using UnityEngine;

public class MotorPart : MonoBehaviour
{
    public string componentId;
    public string displayName;
    public string decision = "unknown";
    public MotorPartInfoCanvas infoCanvas;

    private Renderer cachedRenderer;
    private Material originalMaterial;

    private void Awake()
    {
        cachedRenderer = GetComponentInChildren<Renderer>();
        if (cachedRenderer != null)
        {
            originalMaterial = cachedRenderer.material;
        }
    }

    private void OnMouseDown()
    {
        if (infoCanvas != null)
        {
            infoCanvas.Show();
        }
    }

    public Bounds WorldBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one * 0.1f);
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    public void SetDecision(string newDecision, Material decisionMaterial)
    {
        decision = string.IsNullOrEmpty(newDecision) ? "unknown" : newDecision;
        if (cachedRenderer != null && decisionMaterial != null)
        {
            cachedRenderer.material = decisionMaterial;
        }
    }

    public void ResetMaterial()
    {
        if (cachedRenderer != null && originalMaterial != null)
        {
            cachedRenderer.material = originalMaterial;
        }
    }
}
