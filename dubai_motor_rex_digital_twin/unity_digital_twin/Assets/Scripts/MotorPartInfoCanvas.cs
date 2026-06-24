using UnityEngine;
using UnityEngine.UI;

public class MotorPartInfoCanvas : MonoBehaviour
{
    public MotorPart targetPart;
    public DigitalPassportLoader passportLoader;
    public GameObject panel;
    public Text titleText;
    public Text decisionText;
    public Text metricsText;
    public Text reasonText;
    public float verticalPadding = 0.16f;

    private static MotorPartInfoCanvas activeCanvas;
    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = Camera.main;
        Hide();
    }

    private void LateUpdate()
    {
        if (panel == null || !panel.activeSelf || targetPart == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Bounds bounds = targetPart.WorldBounds();
        transform.position = bounds.center + Vector3.up * (bounds.extents.y + verticalPadding);
        if (targetCamera != null)
        {
            Vector3 forward = transform.position - targetCamera.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }
    }

    public void Show()
    {
        if (activeCanvas != null && activeCanvas != this)
        {
            activeCanvas.Hide();
        }
        activeCanvas = this;
        Refresh();
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
        if (activeCanvas == this)
        {
            activeCanvas = null;
        }
    }

    private void Refresh()
    {
        if (targetPart == null)
        {
            return;
        }

        PassportComponent data = passportLoader == null
            ? null
            : passportLoader.GetComponentData(targetPart.componentId);
        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(targetPart.displayName)
                ? targetPart.componentId
                : targetPart.displayName;
        }
        if (data == null)
        {
            if (decisionText != null) decisionText.text = "NO PASSPORT DATA";
            if (metricsText != null) metricsText.text = string.Empty;
            if (reasonText != null) reasonText.text = "Load the Digital Product Passport first.";
            return;
        }

        int confidence = Mathf.RoundToInt(data.confidence * 100f);
        if (decisionText != null)
        {
            decisionText.text = data.decision.ToUpperInvariant() + "  |  " + confidence + "% CONFIDENCE";
            decisionText.color = DecisionColor(data.decision);
        }
        if (metricsText != null)
        {
            metricsText.text =
                "Health " + Mathf.RoundToInt(data.health_score) + "%     " +
                "Risk " + Mathf.RoundToInt(data.risk_score) + "%\n" +
                "Damage: " + Friendly(data.damage_type) + "\n" +
                "Required test: " + Friendly(data.test_required) + "\n" +
                "Value: AED " + data.recovered_value_aed.ToString("0") +
                "     CO2: " + data.co2_saving_kg.ToString("0.0") + " kg";
        }
        if (reasonText != null)
        {
            reasonText.text = DecisionReason(data);
        }
    }

    private static string DecisionReason(PassportComponent data)
    {
        string evidence =
            "Evidence: health " + Mathf.RoundToInt(data.health_score) + "%, risk " +
            Mathf.RoundToInt(data.risk_score) + "%, confidence " +
            Mathf.RoundToInt(data.confidence * 100f) + "%.";
        string decision = (data.decision ?? string.Empty).ToLowerInvariant();
        if (decision.Contains("reuse"))
        {
            return "Reusable condition: high remaining health and controlled risk. " + evidence;
        }
        if (decision.Contains("repair") || decision.Contains("remanufacture"))
        {
            return "Recoverable damage: repair and verification can restore function. " + evidence;
        }
        if (decision.Contains("replace"))
        {
            return "Replacement required: failure risk is too high for continued service. " + evidence;
        }
        if (decision.Contains("recycle"))
        {
            return "End-of-service condition: damage exceeds safe recovery value. " + evidence;
        }
        return evidence;
    }

    private static Color DecisionColor(string decision)
    {
        string value = (decision ?? string.Empty).ToLowerInvariant();
        if (value.Contains("reuse")) return new Color(0.12f, 0.82f, 0.36f);
        if (value.Contains("repair")) return new Color(1.0f, 0.72f, 0.12f);
        if (value.Contains("replace")) return new Color(1.0f, 0.26f, 0.22f);
        if (value.Contains("recycle")) return new Color(0.20f, 0.52f, 1.0f);
        return Color.white;
    }

    private static string Friendly(string value)
    {
        return string.IsNullOrEmpty(value) ? "not specified" : value.Replace("_", " ");
    }
}
