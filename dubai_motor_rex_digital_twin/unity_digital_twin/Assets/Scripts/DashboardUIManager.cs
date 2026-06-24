using UnityEngine;
using UnityEngine.UI;

public class DashboardUIManager : MonoBehaviour
{
    public DigitalPassportLoader passportLoader;
    public MotorAssemblyController assemblyController;
    public ReXDecisionVisualizer decisionVisualizer;
    public TaskProgressSubscriber taskProgress;

    public Text passportText;
    public Text currentTaskText;
    public Slider timelineSlider;

    public void LoadPassport()
    {
        if (passportLoader == null)
        {
            return;
        }
        passportLoader.LoadPassport();
        if (passportText != null && passportLoader.Passport != null)
        {
            passportText.text =
                passportLoader.Passport.product_id + "\n" +
                passportLoader.Passport.source_site + "\n" +
                passportLoader.Passport.asset_type + "\n" +
                passportLoader.Passport.operating_hours + " operating hours";
        }
    }

    public void RunDecisions()
    {
        if (decisionVisualizer != null)
        {
            decisionVisualizer.ApplyDecisionColors();
        }
    }

    public void StartRobotWorkflow()
    {
        if (taskProgress != null) taskProgress.PlayNextMockEvent();
    }

    public void ResetScene()
    {
        if (assemblyController != null) assemblyController.ResetAssembly();
        SetTask("Ready", 0, 1);
    }

    public void SetTask(string description, int step, int total)
    {
        if (currentTaskText != null)
        {
            currentTaskText.text = description;
        }
        if (timelineSlider != null)
        {
            timelineSlider.value = total == 0 ? 0f : (float)step / total;
        }
    }
}
