using System.Collections;
using UnityEngine;

public class SimulationPlaybackManager : MonoBehaviour
{
    public DashboardUIManager dashboard;
    public MotorAssemblyController assemblyController;
    public ReXDecisionVisualizer decisionVisualizer;
    public TaskProgressSubscriber taskProgress;
    public float stepDelaySeconds = 1.0f;

    private Coroutine playbackRoutine;

    public void StartSimulation()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }
        playbackRoutine = StartCoroutine(RunPlayback());
    }

    private IEnumerator RunPlayback()
    {
        if (dashboard != null) dashboard.LoadPassport();
        yield return new WaitForSeconds(stepDelaySeconds);

        if (decisionVisualizer != null) decisionVisualizer.ApplyDecisionColors();
        yield return new WaitForSeconds(stepDelaySeconds);

        if (taskProgress != null)
        {
            for (int i = 0; i < Mathf.Max(1, taskProgress.events.Count); i++)
            {
                taskProgress.PlayNextMockEvent();
                while (taskProgress.IsBusy)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(stepDelaySeconds);
            }
        }
    }
}
