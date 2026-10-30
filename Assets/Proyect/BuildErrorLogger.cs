using System.Text;
using UnityEngine;

public class BuildErrorLogger : MonoBehaviour
{
    [SerializeField] private bool copyAutomatically = true;
    [SerializeField] private bool showOnScreen = true;
    [SerializeField] private int maxChars = 8000;

    private readonly StringBuilder log = new StringBuilder();
    private Vector2 scroll;
    private bool dirty;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += OnLog;

        log.AppendLine($"API grafica: {SystemInfo.graphicsDeviceType}");
        log.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
        log.AppendLine($"Plataforma: {Application.platform} | Unity {Application.unityVersion}");
        log.AppendLine($"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        log.AppendLine($"Pipeline activo: {(rp != null ? rp.name : "NINGUNO")}");

        // Diagnostico del shader de pixelacion
        var shader = Shader.Find("Hidden/SelectivePixelationComposite");
        if (shader == null)
            log.AppendLine("SHADER: Shader.Find devolvio NULL (no esta en el build)");
        else
            log.AppendLine($"SHADER: encontrado, isSupported = {shader.isSupported}");
        log.AppendLine("------");
    }

    private void OnDestroy() => Application.logMessageReceived -= OnLog;

    private void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        log.AppendLine($"[{type}] {message}");
        if (!string.IsNullOrEmpty(stackTrace))
            log.AppendLine(stackTrace);

        if (log.Length > maxChars)
            log.Remove(0, log.Length - maxChars);

        dirty = true;
    }

    private void LateUpdate()
    {
        if (copyAutomatically && dirty)
        {
            GUIUtility.systemCopyBuffer = log.ToString();
            dirty = false;
        }
    }

    private void OnGUI()
    {
        if (!showOnScreen) return;

        if (GUI.Button(new Rect(10, 10, 220, 60), "Copiar log"))
            GUIUtility.systemCopyBuffer = log.ToString();

        GUILayout.BeginArea(new Rect(10, 80, Screen.width - 20, Screen.height * 0.5f));
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(log.ToString());
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}   