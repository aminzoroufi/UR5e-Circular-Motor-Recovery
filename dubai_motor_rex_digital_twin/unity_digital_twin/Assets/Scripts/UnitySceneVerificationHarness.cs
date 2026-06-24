using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnitySceneVerificationHarness : MonoBehaviour
{
    public Camera sceneCamera;
    public RobotStateVisualizer robot;
    public MotorAssemblyController assemblyController;
    public TaskProgressSubscriber taskProgress;
    public RosJointStateUdpReceiver rosBridge;
    public MotorPartInfoCanvas[] partCanvases;

    private const string OutputFolder = "/tmp/motor_rex_unity_verification";
    private const string LiveOutputFolder = "/tmp/motor_rex_unity_live_verification";

    private void Start()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (Array.IndexOf(arguments, "-rexVerifyLive") >= 0)
        {
            StartCoroutine(RunLiveVerification());
        }
        else if (Array.IndexOf(arguments, "-rexVerify") >= 0)
        {
            StartCoroutine(RunVerification());
        }
    }

    private IEnumerator RunVerification()
    {
        Directory.CreateDirectory(OutputFolder);
        File.Delete(Path.Combine(OutputFolder, "complete.txt"));
        yield return new WaitForSeconds(1.0f);

        bool canvasesHidden = true;
        foreach (MotorPartInfoCanvas info in partCanvases)
        {
            canvasesHidden &= info == null || info.panel == null || !info.panel.activeSelf;
        }
        Debug.Log("[REX_VERIFY] Canvases hidden before selection: " + canvasesHidden);

        LogOversizedRenderers();
        bool robotRigReady = ValidateRobotRig();
        yield return CaptureAngle(
            "01_front.bmp",
            new Vector3(2.20f, 1.75f, -2.35f),
            new Vector3(0.38f, 0.52f, -0.40f)
        );
        yield return CaptureAngle(
            "02_side.bmp",
            new Vector3(-1.85f, 1.55f, -1.65f),
            new Vector3(0.35f, 0.55f, -0.45f)
        );
        yield return CaptureAngle(
            "03_rear.bmp",
            new Vector3(0.50f, 1.65f, 2.15f),
            new Vector3(0.38f, 0.55f, -0.38f)
        );

        MotorPart housing = assemblyController == null ? null : assemblyController.GetPart("housing");
        bool interactionReady = ValidateInteractionWiring();
        if (housing != null && housing.infoCanvas != null)
        {
            housing.gameObject.SendMessage("OnMouseDown", SendMessageOptions.RequireReceiver);
            yield return null;
            bool openedFromPartClick = housing.infoCanvas.panel.activeSelf;
            yield return CaptureAngle(
                "04_housing_evidence.bmp",
                new Vector3(1.70f, 1.32f, -1.32f),
                housing.WorldBounds().center
            );
            Button closeButton = housing.infoCanvas.panel.GetComponentInChildren<Button>(true);
            if (closeButton != null)
            {
                closeButton.onClick.Invoke();
            }
            yield return null;
            bool closedFromX = !housing.infoCanvas.panel.activeSelf;
            interactionReady &= openedFromPartClick && closedFromX;
            Debug.Log(
                "[REX_VERIFY] Housing click_open=" + openedFromPartClick +
                " x_close=" + closedFromX
            );
        }
        else
        {
            Debug.LogError("[REX_VERIFY] Housing evidence canvas is missing.");
            interactionReady = false;
        }

        float minimumClearance = float.PositiveInfinity;
        string[] previewPoses = { "inspection_pose", "home", "reuse_bin_pose", "home" };
        foreach (string pose in previewPoses)
        {
            robot.SetNamedPose(pose);
            float timeout = 8f;
            while (!robot.HasReachedNamedPose && timeout > 0f)
            {
                minimumClearance = Mathf.Min(minimumClearance, robot.MinimumSelfClearance());
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
        Debug.Log(
            "[REX_VERIFY] Safe pose preview minimum_self_clearance_m=" +
            minimumClearance.ToString("F4")
        );
        yield return CaptureAngle(
            "05_safe_pose_preview.bmp",
            new Vector3(1.70f, 1.30f, -1.55f),
            robot.toolTransform.position
        );

        yield return CaptureAngle(
            "06_safe_pose_complete.bmp",
            new Vector3(2.20f, 1.75f, -2.35f),
            new Vector3(0.38f, 0.52f, -0.40f)
        );

        Complete(
            OutputFolder,
            robotRigReady &&
            minimumClearance > -0.03f &&
            canvasesHidden &&
            interactionReady
        );
    }

    private IEnumerator RunLiveVerification()
    {
        Directory.CreateDirectory(LiveOutputFolder);
        File.Delete(Path.Combine(LiveOutputFolder, "complete.txt"));

        float timeout = 50f;
        while ((rosBridge == null || !rosBridge.IsLiveConnected) && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        bool connected = rosBridge != null && rosBridge.IsLiveConnected;
        Debug.Log("[REX_LIVE_VERIFY] ROS joint-state connected=" + connected);
        if (!connected)
        {
            Complete(LiveOutputFolder, false);
            yield break;
        }

        bool robotRigReady = ValidateRobotRig();
        yield return CaptureAngleInFolder(
            LiveOutputFolder,
            "01_live_connected.bmp",
            new Vector3(2.20f, 1.75f, -2.35f),
            new Vector3(0.38f, 0.52f, -0.40f)
        );
        yield return CaptureAngleInFolder(
            LiveOutputFolder,
            "02_live_shoulder_side.bmp",
            new Vector3(-1.85f, 1.55f, -1.65f),
            new Vector3(0.35f, 0.55f, -0.45f)
        );

        float minimumClearance = float.PositiveInfinity;
        timeout = 180f;
        while (rosBridge.LastLiveGraspDistance < 0f && timeout > 0f)
        {
            minimumClearance = Mathf.Min(minimumClearance, robot.MinimumSelfClearance());
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        bool graspObserved = rosBridge.LastLiveGraspDistance >= 0f;
        string componentId = rosBridge.LastComponentId;
        Debug.Log(
            "[REX_LIVE_VERIFY] grasp_observed=" + graspObserved +
            " component=" + componentId +
            " pre_attach_distance_m=" + rosBridge.LastLiveGraspDistance.ToString("F4")
        );
        yield return CaptureAngleInFolder(
            LiveOutputFolder,
            "03_live_grasp.bmp",
            new Vector3(1.70f, 1.30f, -1.55f),
            robot.toolTransform.position
        );

        timeout = 120f;
        while (rosBridge.LastReleasedComponentId != componentId && timeout > 0f)
        {
            minimumClearance = Mathf.Min(minimumClearance, robot.MinimumSelfClearance());
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        bool releaseObserved =
            !string.IsNullOrEmpty(componentId) &&
            rosBridge.LastReleasedComponentId == componentId;
        MotorPart part = assemblyController == null ? null : assemblyController.GetPart(componentId);
        Vector3 expectedPlacement = assemblyController == null
            ? Vector3.zero
            : assemblyController.GetStationPlacementPosition("reuse_bin", componentId);
        timeout = 3f;
        while (part != null &&
               Vector3.Distance(part.WorldBounds().center, expectedPlacement) > 0.12f &&
               timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        bool detached = part != null && part.transform.parent != robot.toolTransform;
        bool placed = part != null &&
            Vector3.Distance(part.WorldBounds().center, expectedPlacement) <= 0.12f;
        Debug.Log(
            "[REX_LIVE_VERIFY] release_observed=" + releaseObserved +
            " detached=" + detached +
            " placed=" + placed +
            " minimum_self_clearance_m=" + minimumClearance.ToString("F4")
        );
        yield return CaptureAngleInFolder(
            LiveOutputFolder,
            "04_live_release.bmp",
            new Vector3(2.20f, 1.75f, -2.35f),
            new Vector3(0.38f, 0.52f, -0.40f)
        );

        Complete(
            LiveOutputFolder,
            connected &&
            robotRigReady &&
            graspObserved &&
            rosBridge.LastLiveGraspDistance < 0.06f &&
            releaseObserved &&
            detached &&
            placed &&
            minimumClearance > -0.03f
        );
    }

    private bool ValidateRobotRig()
    {
        if (robot == null || robot.jointTransforms == null || robot.jointTransforms.Length != 6)
        {
            Debug.LogError("[REX_VERIFY] UR5e joint chain is incomplete.");
            return false;
        }

        float[] expectedDistances = { 0f, 0.425f, 0.4142f, 0.0997f, 0.0996f };
        float maxError = 0f;
        for (int i = 1; i < robot.jointTransforms.Length; i++)
        {
            float actual = Vector3.Distance(
                robot.jointTransforms[i - 1].position,
                robot.jointTransforms[i].position
            );
            maxError = Mathf.Max(maxError, Mathf.Abs(actual - expectedDistances[i - 1]));
        }

        Transform wristMesh = FindDeepChild(robot.jointTransforms[0].root, "ur5e_wrist_3_mesh");
        float wristMeshDistance = wristMesh == null
            ? float.PositiveInfinity
            : Vector3.Distance(wristMesh.position, robot.jointTransforms[5].position);
        Vector3[] expectedGazeboHomeFrames =
        {
            new Vector3(0f, 0.1625f, 0f),
            new Vector3(0f, 0.1625f, 0f),
            new Vector3(0.2961f, 0.467376f, 0f),
            new Vector3(0.649256f, 0.296783f, 0.1333f),
            new Vector3(0.649256f, 0.197083f, 0.1333f),
            new Vector3(0.649256f, 0.197083f, 0.2329f),
        };
        float gazeboFrameError = 0f;
        Transform robotRoot = robot.jointTransforms[0].root;
        for (int i = 0; i < robot.jointTransforms.Length; i++)
        {
            Vector3 localPosition = robotRoot.InverseTransformPoint(
                robot.jointTransforms[i].position
            );
            gazeboFrameError = Mathf.Max(
                gazeboFrameError,
                Vector3.Distance(localPosition, expectedGazeboHomeFrames[i])
            );
        }
        bool hierarchyConnected = true;
        for (int i = 1; i < robot.jointTransforms.Length; i++)
        {
            hierarchyConnected &= robot.jointTransforms[i].IsChildOf(robot.jointTransforms[i - 1]);
        }

        Debug.Log(
            "[REX_VERIFY] UR5e hierarchy_connected=" + hierarchyConnected +
            " max_link_error_m=" + maxError.ToString("F5") +
            " wrist3_mesh_offset_m=" + wristMeshDistance.ToString("F5") +
            " gazebo_fk_error_m=" + gazeboFrameError.ToString("F5")
        );
        return hierarchyConnected &&
            maxError < 0.002f &&
            Mathf.Abs(wristMeshDistance - 0.0989f) < 0.005f &&
            gazeboFrameError < 0.002f;
    }

    private static void LogOversizedRenderers()
    {
        foreach (Renderer renderer in FindObjectsOfType<Renderer>())
        {
            Vector3 size = renderer.bounds.size;
            float maximumDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maximumDimension > 1.5f && renderer.gameObject.name != "Floor")
            {
                Debug.LogWarning(
                    "[REX_VERIFY] Oversized renderer name=" + renderer.gameObject.name +
                    " root=" + renderer.transform.root.name +
                    " bounds_size=" + size.ToString("F3") +
                    " bounds_center=" + renderer.bounds.center.ToString("F3")
                );
            }
        }
    }

    private bool ValidateInteractionWiring()
    {
        bool ready = EventSystem.current != null;
        foreach (MotorPartInfoCanvas info in partCanvases)
        {
            MotorPart part = info == null ? null : info.targetPart;
            Button closeButton = info == null || info.panel == null
                ? null
                : info.panel.GetComponentInChildren<Button>(true);
            ready &= part != null &&
                part.GetComponent<Collider>() != null &&
                part.infoCanvas == info &&
                closeButton != null &&
                closeButton.onClick.GetPersistentEventCount() > 0;
        }
        Debug.Log("[REX_VERIFY] Part click/canvas/X wiring ready: " + ready);
        return ready;
    }

    private IEnumerator CaptureAngle(string fileName, Vector3 cameraPosition, Vector3 lookAt)
    {
        yield return CaptureAngleInFolder(OutputFolder, fileName, cameraPosition, lookAt);
    }

    private IEnumerator CaptureAngleInFolder(
        string folder,
        string fileName,
        Vector3 cameraPosition,
        Vector3 lookAt)
    {
        if (sceneCamera != null)
        {
            sceneCamera.transform.position = cameraPosition;
            sceneCamera.transform.LookAt(lookAt);
        }
        yield return null;
        yield return new WaitForEndOfFrame();
        CaptureCameraPng(Path.Combine(folder, fileName));
        yield return new WaitForSeconds(0.2f);
    }

    private void CaptureCameraPng(string path)
    {
        if (sceneCamera == null)
        {
            return;
        }

        RenderTexture target = new RenderTexture(1920, 1080, 24);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = sceneCamera.targetTexture;
        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            sceneCamera.targetTexture = target;
            RenderTexture.active = target;
            sceneCamera.Render();
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            WriteBmp(path, image);
        }
        finally
        {
            sceneCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            Destroy(target);
            Destroy(image);
        }
    }

    private static void WriteBmp(string path, Texture2D image)
    {
        int width = image.width;
        int height = image.height;
        int rowSize = (width * 3 + 3) & ~3;
        int pixelDataSize = rowSize * height;
        Color32[] pixels = image.GetPixels32();

        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + pixelDataSize);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixelDataSize);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);

            int padding = rowSize - width * 3;
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[rowStart + x];
                    writer.Write(pixel.b);
                    writer.Write(pixel.g);
                    writer.Write(pixel.r);
                }
                for (int i = 0; i < padding; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }
        return null;
    }

    private static void Complete(string folder, bool passed)
    {
        string result = passed ? "PASS" : "FAIL";
        File.WriteAllText(Path.Combine(folder, "complete.txt"), result);
        Debug.Log("[REX_VERIFY] COMPLETE " + result);
    }
}
