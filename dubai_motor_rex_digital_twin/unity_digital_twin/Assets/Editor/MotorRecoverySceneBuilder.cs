using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MotorRecoverySceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MotorRecoveryDigitalTwin.unity";
    private const float DetailedMotorVisualScale = 0.70f;

    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MotorRecoveryDigitalTwin";

        EnsureUniversalRenderPipeline();

        Material reuse = CreateMaterial("Rex Reuse", new Color(0.07f, 0.62f, 0.27f), 0.05f, 0.48f);
        Material repair = CreateMaterial("Rex Repair", new Color(0.95f, 0.70f, 0.12f), 0.05f, 0.44f);
        Material replace = CreateMaterial("Rex Replace", new Color(0.80f, 0.12f, 0.10f), 0.05f, 0.42f);
        Material recycle = CreateMaterial("Rex Recycle", new Color(0.06f, 0.28f, 0.78f), 0.05f, 0.46f);
        Material metal = CreateMaterial("Rex Light Metal", new Color(0.70f, 0.74f, 0.76f), 0.85f, 0.34f);
        Material steel = CreateMaterial("Rex Steel", new Color(0.48f, 0.50f, 0.51f), 1.0f, 0.26f);
        Material copper = CreateMaterial("Rex Copper", new Color(0.95f, 0.43f, 0.16f), 1.0f, 0.22f);
        Material dark = CreateMaterial("Rex Dark", new Color(0.07f, 0.08f, 0.09f), 0.65f, 0.38f);
        Material table = CreateMaterial("Rex Table", new Color(0.40f, 0.42f, 0.43f), 0.35f, 0.50f);
        Material unknown = CreateMaterial("Rex Unknown", new Color(0.42f, 0.48f, 0.52f), 0.20f, 0.52f);
        Material robotBlue = CreateMaterial("Rex Robot Blue", new Color(0.06f, 0.31f, 0.55f), 0.35f, 0.36f);

        CreateCameraAndLighting();
        CreateWorkcell(table, reuse, repair, replace, recycle, dark);
        Transform[] sortingRobotJoints = CreateRobot(
            robotBlue,
            dark,
            "UR5e - Motor Part Sorting",
            new Vector3(0.42f, 0.34f, -0.58f),
            new Vector3(0f, -90f, 0f)
        );
        List<MotorPart> parts = CreateMotorParts(metal, steel, copper, dark, recycle, replace);

        Canvas canvas = CreateCanvas();
        CreateEventSystem();
        Text passportText = CreatePanelText(canvas.transform, "PassportText", new Vector2(24, -66), new Vector2(410, 94), 15);
        Text taskText = CreatePanelText(canvas.transform, "CurrentTaskText", new Vector2(24, -170), new Vector2(620, 52), 16);
        Slider slider = CreateSlider(canvas.transform, "TaskTimeline", new Vector2(24, -232), new Vector2(520, 22));
        Text detailTitle = CreatePanelText(canvas.transform, "DetailTitle", new Vector2(-430, -66), new Vector2(390, 32), 18);
        Text detailBody = CreatePanelText(canvas.transform, "DetailBody", new Vector2(-430, -106), new Vector2(390, 176), 14);

        GameObject loaderObject = new GameObject("DigitalProductPassport");
        DigitalPassportLoader loader = loaderObject.AddComponent<DigitalPassportLoader>();
        loader.passportJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/sample_motor_passport.json");
        loader.LoadPassport();
        foreach (MotorPart part in parts)
        {
            CreatePartInfoCanvas(part, loader);
        }

        GameObject assemblyObject = new GameObject("MotorAssemblyController");
        MotorAssemblyController assembly = assemblyObject.AddComponent<MotorAssemblyController>();
        assembly.parts = parts.ToArray();

        GameObject sortingRobotObject = new GameObject("ArmA_SortingRobotStateVisualizer");
        RobotStateVisualizer sortingRobot = sortingRobotObject.AddComponent<RobotStateVisualizer>();
        sortingRobot.jointTransforms = sortingRobotJoints;
        ConfigureRobotVisualizer(sortingRobot, sortingRobotJoints);

        GameObject taskObject = new GameObject("TaskProgressSubscriber");
        TaskProgressSubscriber taskProgress = taskObject.AddComponent<TaskProgressSubscriber>();
        taskProgress.events = MockEvents();
        taskProgress.robotStateVisualizer = sortingRobot;
        taskProgress.sortingRobotStateVisualizer = sortingRobot;
        taskProgress.assemblyController = assembly;

        GameObject rosBridgeObject = new GameObject("ROS_Gazebo_JointState_UDP_Bridge");
        RosJointStateUdpReceiver rosBridge = rosBridgeObject.AddComponent<RosJointStateUdpReceiver>();
        rosBridge.listenPort = 15000;
        rosBridge.robot = sortingRobot;
        rosBridge.assemblyController = assembly;

        GameObject decisionObject = new GameObject("ReXDecisionVisualizer");
        ReXDecisionVisualizer decision = decisionObject.AddComponent<ReXDecisionVisualizer>();
        decision.passportLoader = loader;
        decision.parts = parts.ToArray();
        decision.reuseMaterial = reuse;
        decision.repairMaterial = repair;
        decision.replaceMaterial = replace;
        decision.recycleMaterial = recycle;
        decision.unknownMaterial = unknown;

        GameObject dashboardObject = new GameObject("DashboardUIManager");
        DashboardUIManager dashboard = dashboardObject.AddComponent<DashboardUIManager>();
        dashboard.passportLoader = loader;
        dashboard.assemblyController = assembly;
        dashboard.decisionVisualizer = decision;
        dashboard.taskProgress = taskProgress;
        dashboard.passportText = passportText;
        dashboard.currentTaskText = taskText;
        dashboard.timelineSlider = slider;
        taskProgress.dashboard = dashboard;

        GameObject detailObject = new GameObject("ComponentDetailPanel");
        ComponentDetailPanel detail = detailObject.AddComponent<ComponentDetailPanel>();
        detail.passportLoader = loader;
        detail.titleText = detailTitle;
        detail.bodyText = detailBody;

        GameObject playbackObject = new GameObject("SimulationPlaybackManager");
        SimulationPlaybackManager playback = playbackObject.AddComponent<SimulationPlaybackManager>();
        playback.dashboard = dashboard;
        playback.assemblyController = assembly;
        playback.decisionVisualizer = decision;
        playback.taskProgress = taskProgress;
        playback.stepDelaySeconds = 0.8f;

        GameObject verificationObject = new GameObject("UnitySceneVerificationHarness");
        UnitySceneVerificationHarness verification = verificationObject.AddComponent<UnitySceneVerificationHarness>();
        verification.sceneCamera = Camera.main;
        verification.robot = sortingRobot;
        verification.assemblyController = assembly;
        verification.taskProgress = taskProgress;
        verification.rosBridge = rosBridge;
        verification.partCanvases = parts.ConvertAll(part => part.infoCanvas).ToArray();

        CreateButton(canvas.transform, "Load Passport", new Vector2(24, -292), dashboard.LoadPassport);
        CreateButton(canvas.transform, "Apply Decisions", new Vector2(174, -292), dashboard.RunDecisions);
        CreateButton(canvas.transform, "Next Robot Step", new Vector2(344, -292), dashboard.StartRobotWorkflow);
        CreateButton(canvas.transform, "Preview Poses", new Vector2(514, -292), playback.StartSimulation);
        CreateButton(canvas.transform, "Reset", new Vector2(664, -292), dashboard.ResetScene);

        dashboard.LoadPassport();
        decision.ApplyDecisionColors();
        dashboard.SetTask("Ready", 0, 17);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Built Dubai Motor Re-X Unity scene at " + ScenePath);
    }

    private static void EnsureUniversalRenderPipeline()
    {
        if (Shader.Find("Universal Render Pipeline/Lit") == null)
        {
            Debug.LogWarning("URP Lit shader is not available yet; falling back to Standard materials.");
            return;
        }

        const string settingsFolder = "Assets/Settings";
        const string pipelinePath = settingsFolder + "/MotorRecoveryURPAsset.asset";
        const string rendererPath = settingsFolder + "/MotorRecoveryURPRenderer.asset";
        if (!AssetDatabase.IsValidFolder(settingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);
        if (pipeline != null && !PipelineHasRendererData(pipeline))
        {
            AssetDatabase.DeleteAsset(pipelinePath);
            pipeline = null;
        }

        if (pipeline == null)
        {
            pipeline = CreateUrpPipelineAsset(pipelinePath, rendererPath);
        }

        if (pipeline != null)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }
    }

    private static RenderPipelineAsset CreateUrpPipelineAsset(string pipelinePath, string rendererPath)
    {
        Type urpType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
        Type rendererType = Type.GetType("UnityEngine.Rendering.Universal.RendererType, Unity.RenderPipelines.Universal.Runtime");
        if (urpType == null || rendererType == null)
        {
            return null;
        }

        object rendererEnum = Enum.Parse(rendererType, "UniversalRenderer");
        object rendererData = null;
        foreach (MethodInfo method in urpType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == "CreateRendererAsset" && parameters.Length == 4)
            {
                rendererData = method.Invoke(null, new object[] { rendererPath, rendererEnum, false, "Renderer" });
                break;
            }
        }

        if (rendererData == null)
        {
            return null;
        }

        foreach (MethodInfo method in urpType.GetMethods(BindingFlags.Static | BindingFlags.Public))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == "Create" && parameters.Length == 1)
            {
                RenderPipelineAsset pipeline = method.Invoke(null, new[] { rendererData }) as RenderPipelineAsset;
                if (pipeline != null)
                {
                    AssetDatabase.CreateAsset(pipeline, pipelinePath);
                    return pipeline;
                }
            }
        }

        return null;
    }

    private static bool PipelineHasRendererData(RenderPipelineAsset pipeline)
    {
        SerializedObject serialized = new SerializedObject(pipeline);
        SerializedProperty rendererDataList = serialized.FindProperty("m_RendererDataList");
        if (rendererDataList == null || !rendererDataList.isArray || rendererDataList.arraySize == 0)
        {
            return false;
        }

        return rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue != null;
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.45f)
    {
        string path = "Assets/Materials/" + name.Replace(" ", "") + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);
        ConfigureDoubleSided(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureDoubleSided(Material material)
    {
        if (material == null)
        {
            return;
        }
        SetMaterialFloat(material, "_Cull", 0f);
        SetMaterialFloat(material, "_DoubleSidedEnable", 1f);
        material.doubleSidedGI = true;
        material.EnableKeyword("_DOUBLESIDED_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void CreateCameraAndLighting()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(2.20f, 1.75f, -2.35f);
        cameraObject.transform.LookAt(new Vector3(0.38f, 0.52f, -0.40f));
        camera.fieldOfView = 48f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.78f, 0.82f);

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

        GameObject inspectionLightObject = new GameObject("Motor Inspection Key Light");
        Light inspectionLight = inspectionLightObject.AddComponent<Light>();
        inspectionLight.type = LightType.Point;
        inspectionLight.intensity = 2.4f;
        inspectionLight.range = 4.0f;
        inspectionLightObject.transform.position = new Vector3(0.2f, 2.1f, -0.8f);

        GameObject probeObject = new GameObject("Motor Reflection Probe");
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(4f, 3f, 4f);
        probeObject.transform.position = new Vector3(0.5f, 1.0f, 0f);
    }

    private static void CreateWorkcell(Material table, Material reuse, Material repair, Material replace, Material recycle, Material dark)
    {
        Cube("Floor", new Vector3(0f, -0.03f, 0f), new Vector3(5f, 0.06f, 4f), table);
        Cube("Motor Parts Table", new Vector3(0.43f, 0.40f, -0.08f), new Vector3(1.50f, 0.10f, 0.70f), table);
        Cube("Reuse Category Table", new Vector3(1.02f, 0.40f, -0.72f), new Vector3(0.78f, 0.10f, 0.52f), reuse);
        Cube("Repair Category Table", new Vector3(0.72f, 0.40f, -1.20f), new Vector3(0.58f, 0.10f, 0.44f), repair);
        Cube("Replace Category Table", new Vector3(0.08f, 0.40f, -1.20f), new Vector3(0.58f, 0.10f, 0.44f), replace);
        Cube("Recycle Category Table", new Vector3(-0.22f, 0.40f, -0.70f), new Vector3(0.58f, 0.10f, 0.44f), recycle);
        Cube("UR5e Pedestal", new Vector3(0.42f, 0.17f, -0.58f), new Vector3(0.28f, 0.34f, 0.24f), dark);

        Label("REUSE", new Vector3(1.02f, 0.52f, -0.72f), reuse.color);
        Label("REPAIR", new Vector3(0.72f, 0.52f, -1.20f), repair.color);
        Label("REPLACE", new Vector3(0.08f, 0.52f, -1.20f), replace.color);
        Label("RECYCLE", new Vector3(-0.22f, 0.52f, -0.70f), recycle.color);
    }

    private static Transform[] CreateRobot(Material robotBlue, Material dark, string rootName, Vector3 rootPosition, Vector3 rootEuler)
    {
        Transform[] realRobot = CreateUr5eRobotFromAssets(robotBlue, dark, rootName, rootPosition, rootEuler);
        return realRobot ?? CreatePlaceholderRobot(robotBlue, dark, rootName, rootPosition, rootEuler);
    }

    private static void ConfigureRobotVisualizer(RobotStateVisualizer visualizer, Transform[] joints)
    {
        if (visualizer == null || joints == null || joints.Length == 0)
        {
            return;
        }

        Transform root = joints[0].root;
        visualizer.jointAxes = new[]
        {
            Vector3.down,
            Vector3.down,
            Vector3.down,
            Vector3.down,
            Vector3.down,
            Vector3.down,
        };
        visualizer.speedScale = 0.65f;
        visualizer.toolFrameTransform = FindDeepChild(root, "tool0");
        visualizer.toolTransform = FindDeepChild(root, "grasp_anchor");
        visualizer.worldVerticalGraspOffset = 0.17f;
        visualizer.gripperFingerTransforms = new[]
        {
            FindDeepChild(root, "robotiq_85_left_knuckle_link"),
            FindDeepChild(root, "robotiq_85_right_knuckle_link"),
            FindDeepChild(root, "robotiq_85_left_inner_knuckle_link"),
            FindDeepChild(root, "robotiq_85_right_inner_knuckle_link"),
            FindDeepChild(root, "robotiq_85_left_finger_tip_link"),
            FindDeepChild(root, "robotiq_85_right_finger_tip_link"),
        };
        visualizer.gripperAngleMultipliers = new[] { 1f, -1f, 1f, -1f, -1f, 1f };
    }

    private static Transform[] CreateUr5eRobotFromAssets(Material robotBlue, Material dark, string rootName, Vector3 rootPosition, Vector3 rootEuler)
    {
        const string urPath = "Assets/Models/Robots/UR5e/visual/";
        const string urCollisionPath = "Assets/Models/Robots/UR5e/collision/";
        const string gripperPath = "Assets/Models/Grippers/Robotiq2F85/visual/";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(urPath + "base.dae") == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(gripperPath + "robotiq_base.dae") == null)
        {
            return null;
        }

        GameObject root = new GameObject(rootName);
        root.transform.position = rootPosition;
        root.transform.rotation = Quaternion.Euler(rootEuler);

        InstantiateModel(urPath + "base.dae", "ur5e_base_mesh", root.transform, Vector3.zero, new Vector3(0f, 180f, 0f), Vector3.one);

        Transform baseInertia = Joint(
            root.transform,
            "base_link_inertia",
            Vector3.zero,
            new Vector3(0f, 180f, 0f)
        );
        Transform shoulder = Joint(baseInertia, "shoulder_pan_joint", new Vector3(0f, 0.1625f, 0f));
        InstantiateModel(urPath + "shoulder.dae", "ur5e_shoulder_mesh", shoulder, Vector3.zero, new Vector3(0f, 180f, 0f), Vector3.one);

        Transform upper = Joint(shoulder, "shoulder_lift_joint", Vector3.zero, new Vector3(-90f, 0f, 0f));
        InstantiateModel(urPath + "upperarm.dae", "ur5e_upper_arm_mesh", upper, new Vector3(0f, 0.138f, 0f), new Vector3(90f, -90f, 0f), Vector3.one);

        Transform elbow = Joint(upper, "elbow_joint", new Vector3(-0.425f, 0f, 0f));
        InstantiateModel(urPath + "forearm.dae", "ur5e_forearm_mesh", elbow, new Vector3(0f, 0.007f, 0f), new Vector3(90f, -90f, 0f), Vector3.one);

        Transform wrist1 = Joint(elbow, "wrist_1_joint", new Vector3(-0.3922f, 0.1333f, 0f));
        InstantiateModel(urPath + "wrist1.dae", "ur5e_wrist_1_mesh", wrist1, new Vector3(0f, -0.127f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);

        Transform wrist2 = Joint(wrist1, "wrist_2_joint", new Vector3(0f, 0f, -0.0997f), new Vector3(-90f, 0f, 0f));
        InstantiateModel(urPath + "wrist2.dae", "ur5e_wrist_2_mesh", wrist2, new Vector3(0f, -0.0997f, 0f), Vector3.zero, Vector3.one);

        Transform wrist3 = Joint(wrist2, "wrist_3_joint", new Vector3(0f, 0f, 0.0996f), new Vector3(90f, 0f, 0f));
        GameObject wrist3Mesh = InstantiateModel(
            urCollisionPath + "wrist3.obj",
            "ur5e_wrist_3_mesh",
            wrist3,
            new Vector3(0f, -0.0989f, -0.0005f),
            new Vector3(90f, 0f, 0f),
            Vector3.one
        );
        if (wrist3Mesh != null)
        {
            ApplyMaterialRecursively(wrist3Mesh, robotBlue);
        }

        Transform tool = Joint(wrist3, "tool0", Vector3.zero);
        InstantiateModel(gripperPath + "ur_to_robotiq_adapter.dae", "robotiq_ur_adapter_mesh", tool, Vector3.zero, Vector3.zero, Vector3.one);
        CreateRobotiqGripperFromAssets(gripperPath, tool, dark);
        UpgradeImportedMaterialsToUrp(root);

        return new[] { shoulder, upper, elbow, wrist1, wrist2, wrist3 };
    }

    private static void UpgradeImportedMaterialsToUrp(GameObject root)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (root == null || shader == null)
        {
            return;
        }

        const string folder = "Assets/Materials/ImportedURP";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "ImportedURP");
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] upgraded = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                if (source == null)
                {
                    continue;
                }
                Color sourceColor = ReadMaterialColor(source);
                Texture sourceTexture = ReadMaterialTexture(source);
                string fileName = SanitizeAssetName(root.name + "_" + renderer.name + "_" + i);
                string path = folder + "/" + fileName + ".mat";
                Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (target == null)
                {
                    target = new Material(shader);
                    AssetDatabase.CreateAsset(target, path);
                }
                else
                {
                    target.shader = shader;
                }
                SetMaterialColor(target, sourceColor);
                if (sourceTexture != null)
                {
                    if (target.HasProperty("_BaseMap")) target.SetTexture("_BaseMap", sourceTexture);
                    if (target.HasProperty("_MainTex")) target.SetTexture("_MainTex", sourceTexture);
                }
                SetMaterialFloat(target, "_Metallic", 0.18f);
                SetMaterialFloat(target, "_Smoothness", 0.42f);
                ConfigureDoubleSided(target);
                EditorUtility.SetDirty(target);
                upgraded[i] = target;
            }
            renderer.sharedMaterials = upgraded;
        }
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    private static Texture ReadMaterialTexture(Material material)
    {
        if (material.HasProperty("_BaseMap")) return material.GetTexture("_BaseMap");
        if (material.HasProperty("_MainTex")) return material.GetTexture("_MainTex");
        return null;
    }

    private static string SanitizeAssetName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value.Replace(" ", "_").Replace("(Clone)", "");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    private static void CreateRobotiqGripperFromAssets(string gripperPath, Transform tool, Material dark)
    {
        Transform baseLink = Joint(tool, "robotiq_85_base_link", new Vector3(0f, 0.031f, 0f));
        InstantiateModel(gripperPath + "robotiq_base.dae", "robotiq_base_mesh", baseLink, Vector3.zero, Vector3.zero, Vector3.one);

        Transform leftKnuckle = Joint(baseLink, "robotiq_85_left_knuckle_link", new Vector3(0.0306f, 0.0549f, 0f));
        Transform rightKnuckle = Joint(baseLink, "robotiq_85_right_knuckle_link", new Vector3(-0.0306f, 0.0549f, 0f));
        Transform leftInnerKnuckle = Joint(baseLink, "robotiq_85_left_inner_knuckle_link", new Vector3(0.0127f, 0.0614f, 0f));
        Transform rightInnerKnuckle = Joint(baseLink, "robotiq_85_right_inner_knuckle_link", new Vector3(-0.0127f, 0.0614f, 0f));
        Transform leftFinger = Joint(leftKnuckle, "robotiq_85_left_finger_link", new Vector3(0.0315f, -0.0038f, 0f));
        Transform rightFinger = Joint(rightKnuckle, "robotiq_85_right_finger_link", new Vector3(-0.0315f, -0.0038f, 0f));
        Transform leftTip = Joint(leftFinger, "robotiq_85_left_finger_tip_link", new Vector3(0.0056f, 0.0472f, 0f));
        Transform rightTip = Joint(rightFinger, "robotiq_85_right_finger_tip_link", new Vector3(-0.0056f, 0.0472f, 0f));

        InstantiateModel(gripperPath + "left_knuckle.dae", "robotiq_left_knuckle_mesh", leftKnuckle, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "right_knuckle.dae", "robotiq_right_knuckle_mesh", rightKnuckle, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "left_inner_knuckle.dae", "robotiq_left_inner_knuckle_mesh", leftInnerKnuckle, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "right_inner_knuckle.dae", "robotiq_right_inner_knuckle_mesh", rightInnerKnuckle, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "left_finger.dae", "robotiq_left_finger_mesh", leftFinger, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "right_finger.dae", "robotiq_right_finger_mesh", rightFinger, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "left_finger_tip.dae", "robotiq_left_tip_mesh", leftTip, Vector3.zero, Vector3.zero, Vector3.one);
        InstantiateModel(gripperPath + "right_finger_tip.dae", "robotiq_right_tip_mesh", rightTip, Vector3.zero, Vector3.zero, Vector3.one);

        Joint(baseLink, "grasp_anchor", new Vector3(0f, 0.17f, 0f));
    }

    private static Transform[] CreatePlaceholderRobot(Material robotBlue, Material dark, string rootName, Vector3 rootPosition, Vector3 rootEuler)
    {
        GameObject root = new GameObject(rootName + " Placeholder");
        root.transform.position = rootPosition;
        root.transform.rotation = Quaternion.Euler(rootEuler);
        Cylinder("Robot Base", root.transform, Vector3.zero, 0.15f, 0.12f, robotBlue, 0f);

        Transform shoulder = Joint(root.transform, "shoulder_pan_joint", new Vector3(0f, 0.18f, 0f));
        Cube("shoulder_link", shoulder, Vector3.zero, new Vector3(0.16f, 0.2f, 0.16f), robotBlue);
        Transform upper = Joint(shoulder, "shoulder_lift_joint", new Vector3(0.2f, 0.08f, 0f));
        Cube("upper_arm_link", upper, new Vector3(0.2f, 0f, 0f), new Vector3(0.42f, 0.11f, 0.11f), robotBlue);
        Transform elbow = Joint(upper, "elbow_joint", new Vector3(0.42f, 0f, 0f));
        Cube("forearm_link", elbow, new Vector3(0.19f, 0f, 0f), new Vector3(0.38f, 0.1f, 0.1f), robotBlue);
        Transform wrist1 = Joint(elbow, "wrist_1_joint", new Vector3(0.38f, 0f, 0f));
        Cube("wrist_1_link", wrist1, new Vector3(0.09f, 0f, 0f), new Vector3(0.18f, 0.09f, 0.09f), robotBlue);
        Transform wrist2 = Joint(wrist1, "wrist_2_joint", new Vector3(0.18f, 0f, 0f));
        Cube("wrist_2_link", wrist2, new Vector3(0.075f, 0f, 0f), new Vector3(0.15f, 0.08f, 0.08f), robotBlue);
        Transform wrist3 = Joint(wrist2, "wrist_3_joint", new Vector3(0.15f, 0f, 0f));
        Cube("tool0", wrist3, new Vector3(0.08f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.08f), dark);
        Cube("left_finger", wrist3, new Vector3(0.14f, 0.04f, 0f), new Vector3(0.03f, 0.14f, 0.025f), dark);
        Cube("right_finger", wrist3, new Vector3(0.14f, -0.04f, 0f), new Vector3(0.03f, 0.14f, 0.025f), dark);
        return new[] { shoulder, upper, elbow, wrist1, wrist2, wrist3 };
    }

    private static GameObject InstantiateModel(string path, string name, Transform parent, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
    {
        if (!AssetPathExists(path))
        {
            return null;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale;
        return instance;
    }

    private static bool AssetPathExists(string path)
    {
        if (File.Exists(path))
        {
            return true;
        }

        if (path.StartsWith("Assets/"))
        {
            string absolutePath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            return File.Exists(absolutePath);
        }

        return false;
    }

    private static List<MotorPart> CreateMotorParts(Material metal, Material steel, Material copper, Material dark, Material recycle, Material warning)
    {
        List<MotorPart> detailedParts = CreateDetailedMotorParts(metal, steel, copper, dark, recycle, warning);
        if (detailedParts != null)
        {
            return detailedParts;
        }

        return CreatePrimitiveMotorParts(metal, steel, copper, dark, recycle, warning);
    }

    private static List<MotorPart> CreateDetailedMotorParts(Material metal, Material steel, Material copper, Material dark, Material recycle, Material warning)
    {
        const string motorPath = "Assets/Models/Motors/DetailedCoolingMotor/";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(motorPath + "housing.obj") == null)
        {
            AssetDatabase.ImportAsset(motorPath + "housing.obj", ImportAssetOptions.ForceSynchronousImport);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(motorPath + "housing.obj") == null)
        {
            return null;
        }

        GameObject root = new GameObject("Detailed Separable Motor Mesh");
        root.transform.position = Vector3.zero;
        List<MotorPart> parts = new List<MotorPart>();
        parts.Add(Part(MotorMesh("terminal_box", root.transform, new Vector3(0.02f, 0.54f, 0.12f), metal), "terminal_box", "Terminal Box", new Vector3(0.02f, 0.82f, 0.12f)));
        parts.Add(Part(MotorMesh("bolts", root.transform, new Vector3(0.29f, 0.50f, 0.12f), recycle), "bolts", "Bolts", new Vector3(0.29f, 0.78f, 0.12f)));
        parts.Add(Part(MotorMesh("bearing_front", root.transform, new Vector3(0.52f, 0.52f, 0.12f), warning), "bearing_front", "Front Bearing", new Vector3(0.52f, 0.80f, 0.12f)));
        parts.Add(Part(MotorMesh("bearing_rear", root.transform, new Vector3(0.74f, 0.52f, 0.12f), warning), "bearing_rear", "Rear Bearing", new Vector3(0.74f, 0.80f, 0.12f)));
        parts.Add(Part(MotorMesh("front_cover", root.transform, new Vector3(0.02f, 0.60f, -0.08f), steel), "front_cover", "Front Cover", new Vector3(0.02f, 0.90f, -0.08f)));
        parts.Add(Part(MotorMesh("housing", root.transform, new Vector3(0.30f, 0.58f, -0.08f), metal), "housing", "Housing", new Vector3(0.30f, 0.90f, -0.08f)));
        parts.Add(Part(MotorMesh("stator", root.transform, new Vector3(0.60f, 0.59f, -0.08f), steel), "stator", "Stator", new Vector3(0.60f, 0.89f, -0.08f)));
        parts.Add(Part(MotorMesh("rear_cover", root.transform, new Vector3(0.86f, 0.60f, -0.08f), steel), "rear_cover", "Rear Cover", new Vector3(0.86f, 0.90f, -0.08f)));
        parts.Add(Part(MotorMesh("fan", root.transform, new Vector3(0.04f, 0.60f, -0.28f), recycle), "fan", "Fan", new Vector3(0.04f, 0.92f, -0.28f)));
        parts.Add(Part(MotorMesh("rotor", root.transform, new Vector3(0.40f, 0.53f, -0.28f), copper), "rotor", "Rotor", new Vector3(0.40f, 0.83f, -0.28f)));
        parts.Add(Part(MotorMesh("shaft", root.transform, new Vector3(0.76f, 0.50f, -0.28f), dark), "shaft", "Shaft", new Vector3(0.76f, 0.78f, -0.28f)));
        return parts;
    }

    private static GameObject MotorMesh(string id, Transform parent, Vector3 localPosition, Material material)
    {
        GameObject obj = InstantiateModel("Assets/Models/Motors/DetailedCoolingMotor/" + id + ".obj", id, parent, localPosition, Vector3.zero, Vector3.one * DetailedMotorVisualScale);
        if (obj == null)
        {
            obj = Cube(id, parent, localPosition, new Vector3(0.12f, 0.12f, 0.12f), material);
        }
        ApplyMaterialRecursively(obj, material);
        return obj;
    }

    private static void ApplyMaterialRecursively(GameObject obj, Material material)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static List<MotorPart> CreatePrimitiveMotorParts(Material metal, Material steel, Material copper, Material dark, Material recycle, Material warning)
    {
        GameObject root = new GameObject("Separable Primitive Motor");
        root.transform.position = Vector3.zero;
        List<MotorPart> parts = new List<MotorPart>();
        parts.Add(Part(Cube("terminal_box", root.transform, new Vector3(0.02f, 0.54f, 0.12f), new Vector3(0.16f, 0.10f, 0.12f), metal), "terminal_box", "Terminal Box", new Vector3(0.02f, 0.82f, 0.12f)));
        parts.Add(Part(Cube("bolts", root.transform, new Vector3(0.29f, 0.50f, 0.12f), new Vector3(0.12f, 0.04f, 0.08f), recycle), "bolts", "Bolts", new Vector3(0.29f, 0.78f, 0.12f)));
        parts.Add(Part(Cylinder("bearing_front", root.transform, new Vector3(0.52f, 0.52f, 0.12f), 0.045f, 0.04f, warning, 90f), "bearing_front", "Front Bearing", new Vector3(0.52f, 0.80f, 0.12f)));
        parts.Add(Part(Cylinder("bearing_rear", root.transform, new Vector3(0.74f, 0.52f, 0.12f), 0.045f, 0.04f, warning, 90f), "bearing_rear", "Rear Bearing", new Vector3(0.74f, 0.80f, 0.12f)));
        parts.Add(Part(Cylinder("front_cover", root.transform, new Vector3(0.02f, 0.60f, -0.08f), 0.12f, 0.08f, steel, 90f), "front_cover", "Front Cover", new Vector3(0.02f, 0.90f, -0.08f)));
        parts.Add(Part(Cube("housing", root.transform, new Vector3(0.30f, 0.58f, -0.08f), new Vector3(0.30f, 0.20f, 0.20f), metal), "housing", "Housing", new Vector3(0.30f, 0.90f, -0.08f)));
        parts.Add(Part(Cylinder("stator", root.transform, new Vector3(0.60f, 0.59f, -0.08f), 0.11f, 0.16f, steel, 90f), "stator", "Stator", new Vector3(0.60f, 0.89f, -0.08f)));
        parts.Add(Part(Cylinder("rear_cover", root.transform, new Vector3(0.86f, 0.60f, -0.08f), 0.12f, 0.08f, steel, 90f), "rear_cover", "Rear Cover", new Vector3(0.86f, 0.90f, -0.08f)));
        parts.Add(Part(Cube("fan", root.transform, new Vector3(0.04f, 0.60f, -0.28f), new Vector3(0.04f, 0.24f, 0.24f), recycle), "fan", "Fan", new Vector3(0.04f, 0.92f, -0.28f)));
        parts.Add(Part(Cylinder("rotor", root.transform, new Vector3(0.40f, 0.53f, -0.28f), 0.055f, 0.26f, copper, 90f), "rotor", "Rotor", new Vector3(0.40f, 0.83f, -0.28f)));
        parts.Add(Part(Cylinder("shaft", root.transform, new Vector3(0.76f, 0.50f, -0.28f), 0.025f, 0.42f, dark, 90f), "shaft", "Shaft", new Vector3(0.76f, 0.78f, -0.28f)));
        return parts;
    }

    private static MotorPart Part(GameObject gameObject, string id, string name, Vector3 explodedLocalPosition)
    {
        MotorPart part = gameObject.AddComponent<MotorPart>();
        part.componentId = id;
        part.displayName = name;
        ConfigureDynamicMotorPart(gameObject, id);
        return part;
    }

    private static void ConfigureDynamicMotorPart(GameObject gameObject, string componentId)
    {
        if (gameObject.GetComponent<Collider>() == null)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.center = gameObject.transform.InverseTransformPoint(bounds.center);
                Vector3 scale = gameObject.transform.lossyScale;
                collider.size = new Vector3(
                    bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z))
                );
            }
        }
        Rigidbody body = gameObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.mass = MotorPartMass(componentId);
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.drag = 0.08f;
        body.angularDrag = 0.08f;
    }

    private static float MotorPartMass(string componentId)
    {
        switch (componentId)
        {
            case "housing": return 8.0f;
            case "stator": return 6.3f;
            case "rotor": return 4.2f;
            case "shaft": return 1.2f;
            case "front_cover":
            case "rear_cover": return 1.4f;
            case "terminal_box": return 0.9f;
            case "fan": return 0.5f;
            case "bearing_front":
            case "bearing_rear": return 0.35f;
            default: return 0.2f;
        }
    }

    private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static GameObject Cube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject obj = Cube(name, Vector3.zero, scale, material);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        return obj;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float length, Material material, float zRotation)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        obj.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static Transform Joint(Transform parent, string name, Vector3 localPosition)
    {
        return Joint(parent, name, localPosition, Vector3.zero);
    }

    private static Transform Joint(Transform parent, string name, Vector3 localPosition, Vector3 localEuler)
    {
        GameObject joint = new GameObject(name);
        joint.transform.SetParent(parent, false);
        joint.transform.localPosition = localPosition;
        joint.transform.localRotation = Quaternion.Euler(localEuler);
        return joint.transform;
    }

    private static void CreatePartInfoCanvas(MotorPart part, DigitalPassportLoader loader)
    {
        if (part == null)
        {
            return;
        }

        GameObject root = new GameObject(part.componentId + " Evidence Canvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 20;
        root.AddComponent<GraphicRaycaster>();
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(440f, 300f);
        canvasRect.localScale = Vector3.one * 0.00125f;

        MotorPartInfoCanvas info = root.AddComponent<MotorPartInfoCanvas>();
        info.targetPart = part;
        info.passportLoader = loader;
        part.infoCanvas = info;

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.055f, 0.075f, 0.09f, 0.96f);
        Stretch(panel.GetComponent<RectTransform>());
        info.panel = panel;

        info.titleText = CreateWorldText(panel.transform, "Title", new Vector2(18f, -14f), new Vector2(350f, 34f), 24, FontStyle.Bold);
        info.decisionText = CreateWorldText(panel.transform, "Decision", new Vector2(18f, -54f), new Vector2(400f, 34f), 19, FontStyle.Bold);
        info.metricsText = CreateWorldText(panel.transform, "Metrics", new Vector2(18f, -96f), new Vector2(404f, 104f), 17, FontStyle.Normal);
        info.reasonText = CreateWorldText(panel.transform, "Reason", new Vector2(18f, -206f), new Vector2(404f, 76f), 16, FontStyle.Italic);

        GameObject closeObject = new GameObject("Close Button");
        closeObject.transform.SetParent(panel.transform, false);
        Image closeImage = closeObject.AddComponent<Image>();
        closeImage.color = new Color(0.78f, 0.16f, 0.14f, 1f);
        Button closeButton = closeObject.AddComponent<Button>();
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-10f, -10f);
        closeRect.sizeDelta = new Vector2(34f, 34f);
        UnityEventTools.AddPersistentListener(closeButton.onClick, info.Hide);

        Text closeText = CreateWorldText(closeObject.transform, "X", Vector2.zero, new Vector2(34f, 34f), 20, FontStyle.Bold);
        closeText.text = "X";
        closeText.alignment = TextAnchor.MiddleCenter;
        Stretch(closeText.GetComponent<RectTransform>());
        panel.SetActive(false);
    }

    private static Text CreateWorldText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static void Label(string text, Vector3 position, Color color)
    {
        GameObject label = new GameObject(text + " Label");
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 24;
        mesh.characterSize = 0.018f;
        mesh.color = color;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Operator Dashboard Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static Text CreatePanelText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Slider slider = obj.AddComponent<Slider>();
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        GameObject background = new GameObject("Background");
        background.transform.SetParent(obj.transform, false);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.20f, 0.22f, 0.9f);
        Stretch(background.GetComponent<RectTransform>());

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(obj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        Stretch(fillAreaRect);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.1f, 0.72f, 0.32f, 1f);
        Stretch(fill.GetComponent<RectTransform>());
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = fillImage;
        return slider;
    }

    private static void CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityAction action)
    {
        GameObject obj = new GameObject(label + " Button");
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.20f, 0.92f);
        Button button = obj.AddComponent<Button>();
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(140, 34);
        UnityEventTools.AddPersistentListener(button.onClick, action);

        Text text = CreatePanelText(obj.transform, "Label", Vector2.zero, new Vector2(140, 34), 14);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static List<TaskProgressEvent> MockEvents()
    {
        return new List<TaskProgressEvent>
        {
            Event(1, "Load motor passport"),
            Event(2, "Scan motor"),
            Event(3, "Evaluate health"),
            Event(4, "Generate Re-X decisions"),
            Event(5, "Move robot to home"),
            Event(6, "Pick terminal_box -> reuse_bin (reuse)"),
            Event(7, "Pick fan -> recycle_bin (recycle)"),
            Event(8, "Pick bearing_front -> replace_bin (replace)"),
            Event(9, "Pick bearing_rear -> replace_bin (replace)"),
            Event(10, "Pick rotor -> reuse_bin (reuse)"),
            Event(11, "Pick stator -> repair_bin (repair/remanufacture)"),
            Event(12, "Pick shaft -> repair_bin (repair/remanufacture)"),
            Event(13, "Pick housing -> reuse_bin (reuse)"),
            Event(14, "Pick front_cover -> reuse_bin (reuse)"),
            Event(15, "Pick rear_cover -> reuse_bin (reuse)"),
            Event(16, "Pick bolts -> recycle_bin (recycle)"),
            Event(17, "Publish final summary"),
        };
    }

    private static TaskProgressEvent Event(int step, string description)
    {
        return new TaskProgressEvent
        {
            step = step,
            total_steps = 17,
            description = description,
            status = "completed",
        };
    }
}
