using UnityEngine;

public class ReXDecisionVisualizer : MonoBehaviour
{
    public DigitalPassportLoader passportLoader;
    public MotorPart[] parts;
    public Material reuseMaterial;
    public Material repairMaterial;
    public Material replaceMaterial;
    public Material recycleMaterial;
    public Material unknownMaterial;

    public void ApplyDecisionColors()
    {
        if (passportLoader == null)
        {
            return;
        }

        foreach (MotorPart part in parts)
        {
            if (part == null)
            {
                continue;
            }
            string decision = passportLoader.GetDecision(part.componentId);
            part.SetDecision(decision, MaterialForDecision(decision));
        }
    }

    private Material MaterialForDecision(string decision)
    {
        switch (decision)
        {
            case "reuse":
                return reuseMaterial;
            case "repair/remanufacture":
                return repairMaterial;
            case "replace":
                return replaceMaterial;
            case "recycle":
                return recycleMaterial;
            default:
                return unknownMaterial;
        }
    }
}

