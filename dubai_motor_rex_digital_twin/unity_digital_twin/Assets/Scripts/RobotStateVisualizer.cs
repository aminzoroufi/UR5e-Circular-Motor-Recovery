using UnityEngine;

public class RobotStateVisualizer : MonoBehaviour
{
    public Transform[] jointTransforms;
    public Vector3[] jointAxes;
    public Transform toolFrameTransform;
    public Transform toolTransform;
    public float worldVerticalGraspOffset = 0.17f;
    public Transform[] gripperFingerTransforms;
    public float[] gripperAngleMultipliers;
    [Range(0.1f, 1.0f)] public float speedScale = 0.65f;

    private static readonly float[] MaxVelocitiesDeg =
    {
        85.94f, 85.94f, 97.40f, 143.24f, 143.24f, 171.89f,
    };

    private static readonly float[] MaxAccelerationsDeg =
    {
        114.59f, 114.59f, 126.05f, 171.89f, 171.89f, 171.89f,
    };

    private readonly float[] startAngles = new float[6];
    private readonly float[] currentAngles = new float[6];
    private readonly float[] targetAngles = new float[6];
    private Quaternion[] baseRotations;
    private Quaternion[] gripperBaseRotations;
    private float trajectoryElapsed;
    private float trajectoryDuration;
    private bool trajectoryActive;
    private float gripperAngle;
    private Vector3 liveRosGraspPosition;
    private bool hasLiveRosToolPosition;

    public bool LiveRosControl { get; private set; }
    public bool HasReachedNamedPose => !trajectoryActive;

    private void Awake()
    {
        CacheBaseRotations();
        UpdateGraspAnchor();
    }

    private void Start()
    {
        SnapToNamedPose("home");
        SetGripperOpen(true);
    }

    public void SetNamedPose(string poseName)
    {
        LiveRosControl = false;
        float[] pose = PoseAngles(poseName);
        for (int i = 0; i < targetAngles.Length; i++)
        {
            startAngles[i] = currentAngles[i];
            targetAngles[i] = ClampJointAngle(i, pose[i]);
        }

        trajectoryElapsed = 0f;
        trajectoryDuration = CalculateTrajectoryDuration();
        trajectoryActive = trajectoryDuration > 0.001f;
        if (!trajectoryActive)
        {
            ApplyJointAngles(targetAngles);
        }
    }

    public void SnapToNamedPose(string poseName)
    {
        float[] pose = PoseAngles(poseName);
        for (int i = 0; i < currentAngles.Length; i++)
        {
            currentAngles[i] = ClampJointAngle(i, pose[i]);
            startAngles[i] = currentAngles[i];
            targetAngles[i] = currentAngles[i];
        }
        trajectoryActive = false;
        ApplyJointAngles(currentAngles);
    }

    public void ApplyRosJointPositions(float[] positionsRadians)
    {
        if (positionsRadians == null || positionsRadians.Length < currentAngles.Length)
        {
            return;
        }

        LiveRosControl = true;
        trajectoryActive = false;
        for (int i = 0; i < currentAngles.Length; i++)
        {
            currentAngles[i] = ClampJointAngle(i, positionsRadians[i] * Mathf.Rad2Deg);
            startAngles[i] = currentAngles[i];
            targetAngles[i] = currentAngles[i];
        }
        ApplyJointAngles(currentAngles);
    }

    public void ApplyRosToolPosition(float[] rosWorldPosition)
    {
        if (rosWorldPosition == null || rosWorldPosition.Length < 3)
        {
            return;
        }
        liveRosGraspPosition = new Vector3(
            rosWorldPosition[0],
            rosWorldPosition[2] - worldVerticalGraspOffset,
            rosWorldPosition[1]
        );
        hasLiveRosToolPosition = true;
        UpdateGraspAnchor();
    }

    public void SetLiveRosControl(bool enabled)
    {
        LiveRosControl = enabled;
        if (!enabled)
        {
            hasLiveRosToolPosition = false;
        }
    }

    public void SetGripperOpen(bool opened)
    {
        gripperAngle = opened ? 0f : 45.4f;
    }

    public float[] GetCurrentJointAngles()
    {
        return (float[])currentAngles.Clone();
    }

    public float MinimumSelfClearance()
    {
        if (jointTransforms == null || jointTransforms.Length < 6 || toolTransform == null)
        {
            return float.NegativeInfinity;
        }

        Vector3 shoulder = jointTransforms[1].position;
        Vector3 elbow = jointTransforms[2].position;
        Vector3 wrist1 = jointTransforms[3].position;
        Vector3 tool = toolTransform.position;
        float upperToWrist = SegmentDistance(shoulder, elbow, wrist1, tool) - 0.13f;
        float baseToForearm = DistancePointToSegment(
            jointTransforms[0].root.position,
            elbow,
            wrist1
        ) - 0.16f;
        return Mathf.Min(upperToWrist, baseToForearm);
    }

    private void Update()
    {
        CacheBaseRotations();
        if (!LiveRosControl && trajectoryActive)
        {
            trajectoryElapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(trajectoryElapsed / trajectoryDuration);
            float blend = QuinticBlend(normalized);
            for (int i = 0; i < currentAngles.Length; i++)
            {
                currentAngles[i] = Mathf.Lerp(startAngles[i], targetAngles[i], blend);
            }
            ApplyJointAngles(currentAngles);

            if (normalized >= 1f)
            {
                trajectoryActive = false;
                ApplyJointAngles(targetAngles);
            }
        }

        AnimateGripper();
        UpdateGraspAnchor();
    }

    private void CacheBaseRotations()
    {
        if (jointTransforms == null)
        {
            return;
        }
        if (baseRotations == null || baseRotations.Length != jointTransforms.Length)
        {
            baseRotations = new Quaternion[jointTransforms.Length];
            for (int i = 0; i < jointTransforms.Length; i++)
            {
                baseRotations[i] = jointTransforms[i] != null
                    ? jointTransforms[i].localRotation
                    : Quaternion.identity;
            }
        }
        if (gripperBaseRotations == null ||
            gripperFingerTransforms == null ||
            gripperBaseRotations.Length != gripperFingerTransforms.Length)
        {
            int length = gripperFingerTransforms == null ? 0 : gripperFingerTransforms.Length;
            gripperBaseRotations = new Quaternion[length];
            for (int i = 0; i < length; i++)
            {
                gripperBaseRotations[i] = gripperFingerTransforms[i] != null
                    ? gripperFingerTransforms[i].localRotation
                    : Quaternion.identity;
            }
        }
    }

    private void ApplyJointAngles(float[] angles)
    {
        if (jointTransforms == null || baseRotations == null)
        {
            return;
        }
        for (int i = 0; i < jointTransforms.Length && i < angles.Length; i++)
        {
            if (jointTransforms[i] == null)
            {
                continue;
            }
            jointTransforms[i].localRotation =
                baseRotations[i] * Quaternion.AngleAxis(angles[i], JointAxis(i));
        }
        UpdateGraspAnchor();
    }

    private void UpdateGraspAnchor()
    {
        if (toolFrameTransform == null || toolTransform == null)
        {
            return;
        }

        // Gazebo carries the part at tool0 minus a fixed world-Z TCP offset.
        // Unity world Y maps to ROS world Z, so keep this offset vertical.
        Vector3 graspPosition = LiveRosControl && hasLiveRosToolPosition
            ? liveRosGraspPosition
            : toolFrameTransform.position + Vector3.down * worldVerticalGraspOffset;
        toolTransform.SetPositionAndRotation(graspPosition, toolFrameTransform.rotation);
    }

    private float CalculateTrajectoryDuration()
    {
        float duration = 0.65f;
        float scale = Mathf.Max(0.1f, speedScale);
        for (int i = 0; i < currentAngles.Length; i++)
        {
            float distance = Mathf.Abs(targetAngles[i] - startAngles[i]);
            float velocityDuration = 1.875f * distance / (MaxVelocitiesDeg[i] * scale);
            float accelerationDuration = Mathf.Sqrt(
                5.774f * distance / (MaxAccelerationsDeg[i] * scale)
            );
            duration = Mathf.Max(duration, velocityDuration, accelerationDuration);
        }
        return duration;
    }

    private void AnimateGripper()
    {
        if (gripperFingerTransforms == null || gripperBaseRotations == null)
        {
            return;
        }
        for (int i = 0; i < gripperFingerTransforms.Length; i++)
        {
            Transform finger = gripperFingerTransforms[i];
            if (finger == null)
            {
                continue;
            }
            float direction = gripperAngleMultipliers != null &&
                i < gripperAngleMultipliers.Length
                ? gripperAngleMultipliers[i]
                : (i % 2 == 0 ? 1f : -1f);
            Quaternion target = gripperBaseRotations[i] *
                Quaternion.AngleAxis(gripperAngle * direction, Vector3.forward);
            finger.localRotation = Quaternion.RotateTowards(
                finger.localRotation,
                target,
                90f * Time.deltaTime
            );
        }
    }

    private Vector3 JointAxis(int index)
    {
        if (jointAxes == null || index >= jointAxes.Length || jointAxes[index] == Vector3.zero)
        {
            return Vector3.up;
        }
        return jointAxes[index].normalized;
    }

    private static float ClampJointAngle(int index, float angle)
    {
        if (index == 2)
        {
            return Mathf.Clamp(angle, -180f, 180f);
        }
        return Mathf.Clamp(angle, -360f, 360f);
    }

    private static float QuinticBlend(float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return t3 * (10f - 15f * t + 6f * t2);
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 segment = b - a;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.000001f)
        {
            return Vector3.Distance(point, a);
        }
        float t = Mathf.Clamp01(Vector3.Dot(point - a, segment) / lengthSquared);
        return Vector3.Distance(point, a + segment * t);
    }

    private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        float s;
        float t;

        if (a <= 0.000001f && e <= 0.000001f)
        {
            return Vector3.Distance(p1, p2);
        }
        if (a <= 0.000001f)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 0.000001f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denominator = a * e - b * b;
                s = denominator == 0f ? 0f : Mathf.Clamp01((b * f - c * e) / denominator);
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }
        return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
    }

    private static float[] PoseAngles(string poseName)
    {
        switch (poseName)
        {
            case "inspection_pose": return Degrees(-0.20f, -0.95f, 1.35f, -0.25f, 0.15f, 0f);
            case "pickup_pose": return Degrees(0.15f, -1.05f, 1.55f, -0.35f, 0f, 0f);
            case "pickup_terminal_box_pose": return Degrees(0.34f, -1.08f, 1.48f, -0.42f, 0.18f, 0f);
            case "pickup_bolts_pose": return Degrees(0.20f, -1.06f, 1.50f, -0.40f, 0.08f, 0.12f);
            case "pickup_bearing_front_pose": return Degrees(0.06f, -1.08f, 1.54f, -0.45f, 0.14f, -0.18f);
            case "pickup_bearing_rear_pose": return Degrees(-0.08f, -1.05f, 1.52f, -0.46f, 0.10f, 0.20f);
            case "pickup_front_cover_pose": return Degrees(0.18f, -1.02f, 1.58f, -0.52f, -0.05f, 0f);
            case "pickup_housing_pose": return Degrees(0.05f, -1.08f, 1.60f, -0.50f, 0.05f, 0f);
            case "pickup_stator_pose": return Degrees(-0.08f, -1.10f, 1.56f, -0.46f, 0.12f, -0.15f);
            case "pickup_rear_cover_pose": return Degrees(-0.18f, -1.04f, 1.55f, -0.48f, 0.03f, 0.14f);
            case "pickup_fan_pose": return Degrees(0.42f, -1.00f, 1.52f, -0.56f, -0.10f, 0.20f);
            case "pickup_rotor_pose": return Degrees(0.22f, -1.12f, 1.60f, -0.48f, -0.04f, -0.10f);
            case "pickup_shaft_pose": return Degrees(-0.02f, -1.16f, 1.62f, -0.44f, 0f, 0.24f);
            case "reuse_bin_pose": return Degrees(-1.45f, -0.75f, 1.10f, -0.30f, 0f, 0f);
            case "repair_bin_pose": return Degrees(-1.75f, -0.75f, 1.15f, -0.30f, 0f, 0f);
            case "replace_bin_pose": return Degrees(-2.05f, -0.75f, 1.15f, -0.30f, 0f, 0f);
            case "recycle_bin_pose": return Degrees(-2.35f, -0.75f, 1.15f, -0.30f, 0f, 0f);
            default: return Degrees(0f, -0.80f, 1.25f, -0.45f, 0f, 0f);
        }
    }

    private static float[] Degrees(
        float j0,
        float j1,
        float j2,
        float j3,
        float j4,
        float j5)
    {
        return new[]
        {
            j0 * Mathf.Rad2Deg,
            j1 * Mathf.Rad2Deg,
            j2 * Mathf.Rad2Deg,
            j3 * Mathf.Rad2Deg,
            j4 * Mathf.Rad2Deg,
            j5 * Mathf.Rad2Deg,
        };
    }
}
