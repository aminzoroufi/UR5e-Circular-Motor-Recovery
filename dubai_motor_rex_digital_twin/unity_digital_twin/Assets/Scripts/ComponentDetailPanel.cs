using UnityEngine;
using UnityEngine.UI;

public class ComponentDetailPanel : MonoBehaviour
{
    public static ComponentDetailPanel Instance { get; private set; }

    public DigitalPassportLoader passportLoader;
    public Text titleText;
    public Text bodyText;

    private void Awake()
    {
        Instance = this;
    }

    public void Show(MotorPart part)
    {
        if (part == null)
        {
            return;
        }

        PassportComponent data = passportLoader == null ? null : passportLoader.GetComponentData(part.componentId);
        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(part.displayName) ? part.componentId : part.displayName;
        }
        if (bodyText != null)
        {
            if (data == null)
            {
                bodyText.text = "No passport data loaded.";
            }
            else
            {
                bodyText.text =
                    "Part ID: " + data.part_id + "\n" +
                    "Material: " + data.material + "\n" +
                    "Health: " + data.health_score + "\n" +
                    "Risk: " + data.risk_score + "\n" +
                    "Decision: " + data.decision + "\n" +
                    "Test: " + data.test_required + "\n" +
                    "Robot action: " + data.robot_action;
            }
        }
    }
}

