using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TaskProgressEvent
{
    public int step;
    public int total_steps;
    public string description;
    public string status;
}

public class TaskProgressSubscriber : MonoBehaviour
{
    public List<TaskProgressEvent> events = new List<TaskProgressEvent>();
    public DashboardUIManager dashboard;
    public RobotStateVisualizer robotStateVisualizer;
    public RobotStateVisualizer sortingRobotStateVisualizer;
    public MotorAssemblyController assemblyController;

    private int playbackIndex;
    private Coroutine activeEventRoutine;
    public bool IsBusy => activeEventRoutine != null;
    public float LastGraspDistance { get; private set; }

    public void AddEventFromJson(string json)
    {
        TaskProgressEvent progressEvent = UnityEngine.JsonUtility.FromJson<TaskProgressEvent>(json);
        if (progressEvent == null)
        {
            return;
        }
        events.Add(progressEvent);
        if (dashboard != null)
        {
            dashboard.SetTask(progressEvent.description, progressEvent.step, progressEvent.total_steps);
        }
    }

    public void PlayNextMockEvent()
    {
        if (events.Count == 0 || activeEventRoutine != null)
        {
            return;
        }
        TaskProgressEvent progressEvent = events[playbackIndex % events.Count];
        playbackIndex++;
        if (dashboard != null)
        {
            dashboard.SetTask(progressEvent.description, progressEvent.step, progressEvent.total_steps);
        }
        activeEventRoutine = StartCoroutine(RunEvent(progressEvent.description));
    }

    public bool PlayEventDescription(string description)
    {
        if (activeEventRoutine != null || string.IsNullOrEmpty(description))
        {
            return false;
        }

        if (dashboard != null)
        {
            dashboard.SetTask(description, 1, 1);
        }
        activeEventRoutine = StartCoroutine(RunEvent(description));
        return true;
    }

    private IEnumerator RunEvent(string description)
    {
        yield return ApplyEventSequence(description);
        activeEventRoutine = null;
    }

    private IEnumerator ApplyEventSequence(string description)
    {
        if (string.IsNullOrEmpty(description))
        {
            yield break;
        }
        if (description.StartsWith("Pick "))
        {
            if (TryParsePick(description, out string componentId, out string stationId))
            {
                yield return RunPickPlace(
                    sortingRobotStateVisualizer != null ? sortingRobotStateVisualizer : robotStateVisualizer,
                    componentId,
                    stationId
                );
            }
            yield break;
        }
        SetSortingPose(GuessPose(description));
    }

    private IEnumerator RunPickPlace(
        RobotStateVisualizer robot,
        string componentId,
        string stationId)
    {
        if (robot == null || assemblyController == null)
        {
            yield break;
        }
        if (robot.LiveRosControl)
        {
            yield break;
        }

        robot.SetGripperOpen(true);
        robot.SetNamedPose("home");
        yield return WaitForRobot(robot, 5.0f);
        robot.SetNamedPose(PickupPose(componentId));
        yield return WaitForRobot(robot, 5.0f);

        LastGraspDistance = Vector3.Distance(
            assemblyController.GetPartGraspPosition(componentId),
            robot.toolTransform.position
        );
        if (LastGraspDistance > 0.06f)
        {
            Debug.LogWarning(
                "[LOCAL_PREVIEW] " + componentId +
                " is " + LastGraspDistance.ToString("F3") +
                " m from the gripper. Skipping fake attachment; use the live ROS/MoveIt workflow."
            );
            robot.SetNamedPose("home");
            yield return WaitForRobot(robot, 5.0f);
            yield break;
        }

        robot.SetGripperOpen(false);
        yield return new WaitForSeconds(0.55f);
        assemblyController.AttachPartToTool(componentId, robot.toolTransform);
        yield return null;

        robot.SetNamedPose("home");
        yield return WaitForRobot(robot, 5.0f);
        robot.SetNamedPose(StationPose(stationId));
        yield return WaitForRobot(robot, 5.0f);

        assemblyController.ReleasePartAtStation(componentId, stationId);
        robot.SetGripperOpen(true);
        yield return new WaitForSeconds(0.55f);
        robot.SetNamedPose("home");
        yield return WaitForRobot(robot, 5.0f);
    }

    private static IEnumerator WaitForRobot(RobotStateVisualizer robot, float timeout)
    {
        float elapsed = 0f;
        while (!robot.HasReachedNamedPose && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static string PickupPose(string componentId)
    {
        return "pickup_" + componentId + "_pose";
    }

    private static string StationPose(string stationId)
    {
        switch (stationId)
        {
            case "reuse_bin": return "reuse_bin_pose";
            case "repair_bin": return "repair_bin_pose";
            case "replace_bin": return "replace_bin_pose";
            case "recycle_bin": return "recycle_bin_pose";
            default: return "inspection_pose";
        }
    }

    private static string GuessPose(string description)
    {
        if (description.Contains("reuse_bin")) return "reuse_bin_pose";
        if (description.Contains("repair_bin")) return "repair_bin_pose";
        if (description.Contains("replace_bin")) return "replace_bin_pose";
        if (description.Contains("recycle_bin")) return "recycle_bin_pose";
        if (description.Contains("Scan")) return "inspection_pose";
        return "home";
    }

    private static bool TryParsePick(string description, out string componentId, out string stationId)
    {
        componentId = string.Empty;
        stationId = string.Empty;
        int arrowIndex = description.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrowIndex < 0)
        {
            return false;
        }
        componentId = description.Substring(5, arrowIndex - 5).Trim();
        string remainder = description.Substring(arrowIndex + 4);
        int stationEnd = remainder.IndexOf(" ", StringComparison.Ordinal);
        stationId = stationEnd >= 0 ? remainder.Substring(0, stationEnd).Trim() : remainder.Trim();
        return !string.IsNullOrEmpty(componentId) && !string.IsNullOrEmpty(stationId);
    }

    private void SetSortingPose(string poseName)
    {
        RobotStateVisualizer visualizer =
            sortingRobotStateVisualizer != null ? sortingRobotStateVisualizer : robotStateVisualizer;
        if (visualizer != null)
        {
            visualizer.SetNamedPose(poseName);
        }
    }
}
