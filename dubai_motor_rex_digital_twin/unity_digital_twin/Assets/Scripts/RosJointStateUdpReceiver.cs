using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class RosUnityBridgePacket
{
    public string type;
    public string[] names;
    public float[] positions;
    public float[] tool_position;
    public float gripper_position;
    public string action;
    public string component_id;
    public string target_station;
    public float[] target_part_position;
    public string status;
    public string gripper;
}

public class RosJointStateUdpReceiver : MonoBehaviour
{
    public int listenPort = 15000;
    public RobotStateVisualizer robot;
    public MotorAssemblyController assemblyController;
    public float liveTimeoutSeconds = 1.0f;

    private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
    private UdpClient client;
    private Thread receiverThread;
    private volatile bool running;
    private float lastJointPacketTime = -100f;
    private string lastActionKey = string.Empty;
    private readonly Dictionary<string, Vector3> sourceTargets =
        new Dictionary<string, Vector3>();
    private Coroutine pendingGrasp;

    public float LastLiveGraspDistance { get; private set; } = -1f;
    public string LastAction { get; private set; } = string.Empty;
    public string LastComponentId { get; private set; } = string.Empty;
    public string LastReleasedComponentId { get; private set; } = string.Empty;
    public bool IsLiveConnected =>
        robot != null &&
        robot.LiveRosControl &&
        Time.unscaledTime - lastJointPacketTime <= liveTimeoutSeconds;

    private void Start()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-rexVerify") >= 0)
        {
            enabled = false;
            return;
        }

        try
        {
            client = new UdpClient(listenPort);
            client.Client.ReceiveTimeout = 500;
            running = true;
            receiverThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "MotorReX ROS UDP Receiver",
            };
            receiverThread.Start();
            Debug.Log("[ROS_UNITY] Listening for Gazebo/MoveIt joint states on UDP " + listenPort);
        }
        catch (Exception exception)
        {
            Debug.LogError("[ROS_UNITY] Could not open UDP port " + listenPort + ": " + exception.Message);
        }
    }

    private void Update()
    {
        string newestJointPacket = null;
        while (messages.TryDequeue(out string json))
        {
            RosUnityBridgePacket packet = JsonUtility.FromJson<RosUnityBridgePacket>(json);
            if (packet == null)
            {
                continue;
            }
            if (packet.type == "joint_state")
            {
                newestJointPacket = json;
            }
            else if (packet.type == "action")
            {
                if (!string.IsNullOrEmpty(newestJointPacket))
                {
                    ApplyJointPacket(newestJointPacket);
                    newestJointPacket = null;
                }
                ApplyAction(packet);
            }
        }

        if (!string.IsNullOrEmpty(newestJointPacket))
        {
            ApplyJointPacket(newestJointPacket);
        }
        else if (robot != null &&
                 robot.LiveRosControl &&
                 Time.unscaledTime - lastJointPacketTime > liveTimeoutSeconds)
        {
            robot.SetLiveRosControl(false);
            Debug.LogWarning("[ROS_UNITY] Joint-state stream timed out; local safe trajectory mode restored.");
        }
    }

    private void ApplyJointPacket(string json)
    {
        RosUnityBridgePacket packet = JsonUtility.FromJson<RosUnityBridgePacket>(json);
        if (packet == null || robot == null)
        {
            return;
        }
        robot.ApplyRosJointPositions(packet.positions);
        robot.ApplyRosToolPosition(packet.tool_position);
        robot.SetGripperOpen(packet.gripper_position < 0.20f);
        lastJointPacketTime = Time.unscaledTime;
    }

    private void ApplyAction(RosUnityBridgePacket packet)
    {
        if (assemblyController == null ||
            packet.status != "executing" ||
            string.IsNullOrEmpty(packet.component_id))
        {
            return;
        }

        string actionKey = packet.action + "|" + packet.component_id + "|" + packet.target_station;
        if (actionKey == lastActionKey)
        {
            return;
        }
        lastActionKey = actionKey;
        LastAction = packet.action ?? string.Empty;
        LastComponentId = packet.component_id ?? string.Empty;

        if ((packet.action == "approach_component" ||
             packet.action == "contact_component") &&
            packet.target_part_position != null &&
            packet.target_part_position.Length >= 3)
        {
            assemblyController.SynchronizePartCenter(
                packet.component_id,
                new Vector3(
                    packet.target_part_position[0],
                    packet.target_part_position[2],
                    packet.target_part_position[1]
                )
            );
            sourceTargets[packet.component_id] = new Vector3(
                packet.target_part_position[0],
                packet.target_part_position[2],
                packet.target_part_position[1]
            );
        }

        if (packet.action == "grasp_component" && robot != null)
        {
            if (pendingGrasp != null)
            {
                StopCoroutine(pendingGrasp);
            }
            pendingGrasp = StartCoroutine(AttachAtContact(packet.component_id));
        }
        else if (packet.action == "release_component")
        {
            assemblyController.ReleasePartAtStation(packet.component_id, packet.target_station);
            LastReleasedComponentId = packet.component_id;
        }
    }

    private IEnumerator AttachAtContact(string componentId)
    {
        MotorPart part = assemblyController.GetPart(componentId);
        float timeout = 1.5f;
        float distance = float.PositiveInfinity;
        while (part != null && timeout > 0f)
        {
            if (sourceTargets.TryGetValue(componentId, out Vector3 target))
            {
                assemblyController.SynchronizePartCenter(componentId, target);
            }
            distance = Vector3.Distance(part.WorldBounds().center, robot.toolTransform.position);
            if (distance <= 0.06f)
            {
                break;
            }
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        LastLiveGraspDistance = part == null ? -1f : distance;
        Vector3 sourceTarget = sourceTargets.TryGetValue(componentId, out Vector3 cachedTarget)
            ? cachedTarget
            : Vector3.zero;
        Vector3 partError = part == null
            ? Vector3.zero
            : part.WorldBounds().center - sourceTarget;
        Vector3 toolError = robot.toolTransform.position - sourceTarget;
        Debug.Log(
            "[ROS_UNITY] Live grasp " + componentId +
            " pre_attach_distance_m=" + LastLiveGraspDistance.ToString("F4") +
            " part_target_error=" + partError.ToString("F4") +
            " tool_target_error=" + toolError.ToString("F4")
        );
        if (part != null && LastLiveGraspDistance <= 0.06f)
        {
            assemblyController.AttachPartToTool(componentId, robot.toolTransform);
        }
        else
        {
            Debug.LogError(
                "[ROS_UNITY] Refused non-contact attachment for " + componentId
            );
        }
        pendingGrasp = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref sender);
                messages.Enqueue(Encoding.UTF8.GetString(data));
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private void OnDestroy()
    {
        running = false;
        if (client != null)
        {
            client.Close();
            client = null;
        }
        if (receiverThread != null && receiverThread.IsAlive)
        {
            receiverThread.Join(700);
        }
    }
}
