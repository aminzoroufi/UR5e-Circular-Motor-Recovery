using UnityEditor;
using UnityEditor.SceneManagement;

public static class UnityVerificationRunner
{
    public static void Start()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MotorRecoveryDigitalTwin.unity");
        EditorApplication.delayCall += EnterPlayMode;
    }

    private static void EnterPlayMode()
    {
        EditorApplication.isPlaying = true;
    }
}
