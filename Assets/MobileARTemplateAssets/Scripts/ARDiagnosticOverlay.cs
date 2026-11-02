using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MebelAR.Diagnostics
{
    /// <summary>
    /// Comprehensive diagnostic overlay and runtime renderer inspector for AR Foundation + URP 13.
    /// Inspects and logs:
    /// 1. ARCameraBackground texture bindings, native pointers, property names, and dimensions.
    /// 2. ARCoreBackground.shader material parameters and _UnityDisplayTransform matrix.
    /// 3. URP ScriptableRenderer activeRenderPassQueue passes, order, and clear flags.
    /// 4. Detects and optionally suppresses the URP 13 bug where DrawObjectsPass clears color to black
    ///    after the AR background pass has rendered.
    /// 5. Optional corner picture-in-picture (PIP) test quad to independently verify camera texture rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class ARDiagnosticOverlay : MonoBehaviour
    {
        [Header("References (Auto-found if null)")]
        [SerializeField] private ARCameraManager m_CameraManager;
        [SerializeField] private ARCameraBackground m_CameraBackground;
        [SerializeField] private Camera m_Camera;

        [Header("Runtime Fix & Test Settings")]
        [Tooltip("If true, intercepts DrawObjectsPass clear flag to prevent it from wiping the AR background to black.")]
        [SerializeField] private bool m_EnableClearFix = true;

        [Tooltip("If true, enqueues a corner PIP pass after transparents to independently verify shader & camera texture sampling.")]
        [SerializeField] private bool m_EnableCornerPip = false;

        [Header("Display Settings")]
        [Tooltip("When false, no diagnostic overlay is rendered on screen.")]
        [SerializeField] private bool m_ShowOnScreen = false;
        [SerializeField] private int m_FontSize = 24;

        // Runtime diagnostic statistics
        private int m_FrameCount = 0;
        private double m_LastFrameTimestampNs = 0;
        private float m_LastFrameTime = 0f;
        private int m_LastTextureCount = 0;
        private string m_LastTextureDetails = "None";
        private float m_LogTimer = 0f;
        private bool m_Collapsed = false;

        // Texture & Material diagnostics
        private IntPtr m_LastNativeTexPtr = IntPtr.Zero;
        private string m_TexturePropertyNames = "None";
        private string m_DisplayTransformSummary = "Not received";
        private Matrix4x4 m_LastDisplayTransform = Matrix4x4.identity;
        private bool m_DisplayMatrixValid = false;
        private string m_ShaderKeywords = "None";
        private bool m_ShaderSupported = false;

        // Render queue & pass diagnostics
        private int m_PassQueueCount = 0;
        private string m_PassQueueSummary = "None";
        private string m_OpaquePassClearStatus = "Unknown";
        private bool m_ArBackgroundPassFound = false;
        private int m_ColorClearsSuppressedCount = 0;

        // Diagnostic render passes
        private ARQueueMonitorPass m_MonitorPass;
        private ARCornerPipPass m_PipPass;

        // Reflection cache for ScriptableRenderer.activeRenderPassQueue
        private static PropertyInfo s_ActiveRenderPassQueueProp;
        private static FieldInfo s_ActiveRenderPassQueueField;

        private void Awake()
        {
            if (m_Camera == null)
            {
                if (Camera.main != null)
                {
                    m_Camera = Camera.main;
                }
                else
                {
                    m_Camera = FindObjectOfType<Camera>();
                }
            }

            if (m_Camera != null)
            {
                if (m_CameraManager == null)
                    m_CameraManager = m_Camera.GetComponent<ARCameraManager>();

                if (m_CameraBackground == null)
                    m_CameraBackground = m_Camera.GetComponent<ARCameraBackground>();
            }

            if (m_CameraManager == null)
                m_CameraManager = FindObjectOfType<ARCameraManager>();

            if (m_CameraBackground == null)
                m_CameraBackground = FindObjectOfType<ARCameraBackground>();

            CacheReflection();

            m_MonitorPass = new ARQueueMonitorPass(this);
            m_PipPass = new ARCornerPipPass(this);

            string cameraName = (m_Camera != null) ? m_Camera.name : "None";
            Debug.Log($"[AR_DIAG] ARDiagnosticOverlay initialized. Camera={cameraName}, Mgr={m_CameraManager != null}, Bg={m_CameraBackground != null}, ClearFix={m_EnableClearFix}");
        }

        private static void CacheReflection()
        {
            if (s_ActiveRenderPassQueueProp == null && s_ActiveRenderPassQueueField == null)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                s_ActiveRenderPassQueueProp = typeof(ScriptableRenderer).GetProperty("activeRenderPassQueue", flags);
                s_ActiveRenderPassQueueField = typeof(ScriptableRenderer).GetField("m_ActiveRenderPassQueue", flags);
            }
        }

        private static List<ScriptableRenderPass> GetActivePassQueue(ScriptableRenderer renderer)
        {
            if (renderer == null) return null;
            CacheReflection();
            return (s_ActiveRenderPassQueueProp?.GetValue(renderer) ?? s_ActiveRenderPassQueueField?.GetValue(renderer)) as List<ScriptableRenderPass>;
        }

        private void OnEnable()
        {
            if (m_CameraManager != null)
            {
                m_CameraManager.frameReceived += OnCameraFrameReceived;
            }
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            if (m_CameraManager != null)
            {
                m_CameraManager.frameReceived -= OnCameraFrameReceived;
            }
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == m_Camera)
            {
                var additionalData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (additionalData != null && additionalData.scriptableRenderer != null)
                {
                    // Enqueue queue inspector/fix pass before opaques
                    additionalData.scriptableRenderer.EnqueuePass(m_MonitorPass);

                    // Enqueue corner test pass after transparents if requested
                    if (m_EnableCornerPip)
                    {
                        additionalData.scriptableRenderer.EnqueuePass(m_PipPass);
                    }
                }
            }
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            m_FrameCount++;
            m_LastFrameTime = Time.realtimeSinceStartup;
            m_LastFrameTimestampNs = args.timestampNs.GetValueOrDefault();
            m_LastTextureCount = args.textures != null ? args.textures.Count : 0;

            if (m_LastTextureCount > 0 && args.textures != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < args.textures.Count; i++)
                {
                    var tex = args.textures[i];
                    if (tex != null)
                    {
                        var ptr = tex.GetNativeTexturePtr();
                        if (i == 0) m_LastNativeTexPtr = ptr;
                        sb.Append($"[{i}]:{tex.width}x{tex.height}({tex.format}) Ptr={ptr} ");
                    }
                }
                m_LastTextureDetails = sb.ToString();
            }
            else
            {
                m_LastTextureDetails = "0 textures";
            }

            if (args.displayMatrix.HasValue)
            {
                m_LastDisplayTransform = args.displayMatrix.Value;
                m_DisplayMatrixValid = !m_LastDisplayTransform.isIdentity && m_LastDisplayTransform != Matrix4x4.zero;
                m_DisplayTransformSummary = $"Valid (scale=[{m_LastDisplayTransform.m00:F2}, {m_LastDisplayTransform.m11:F2}])";
            }
            else
            {
                m_DisplayMatrixValid = false;
                m_DisplayTransformSummary = "NULL (HasValue=false)";
            }

            InspectMaterialState();
        }

        private void InspectMaterialState()
        {
            var mat = m_CameraBackground != null ? m_CameraBackground.material : null;
            if (mat != null)
            {
                m_ShaderSupported = mat.shader != null && mat.shader.isSupported;

                var propNames = mat.GetTexturePropertyNames();
                m_TexturePropertyNames = propNames != null && propNames.Length > 0 ? string.Join(", ", propNames) : "None";

                var keywords = mat.shaderKeywords;
                m_ShaderKeywords = keywords != null && keywords.Length > 0 ? string.Join(", ", keywords) : "(None)";
            }
        }

        private void Update()
        {
            m_LogTimer += Time.unscaledDeltaTime;
            if (m_LogTimer >= 2.5f)
            {
                m_LogTimer = 0f;
                LogDiagnosticState();
            }
        }

        /// <summary>
        /// Called by the ARQueueMonitorPass during URP camera rendering.
        /// Inspects all passes in the queue, checks their clear flags, and suppresses
        /// invalid color clearing on DrawObjectsPass when clear fix is enabled.
        /// </summary>
        internal void ProcessRenderQueue(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var queue = GetActivePassQueue(renderer);
            if (queue == null) return;

            m_PassQueueCount = queue.Count;
            m_ArBackgroundPassFound = false;
            m_OpaquePassClearStatus = "Not Found";

            var sb = new StringBuilder();

            for (int i = 0; i < queue.Count; i++)
            {
                var pass = queue[i];
                if (pass == null) continue;

                string passName = pass.GetType().Name;
                bool isBgPass = passName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isOpaquePass = passName.IndexOf("DrawObjectsPass", StringComparison.OrdinalIgnoreCase) >= 0 
                                 || passName.IndexOf("DrawOpaqueObjects", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isBgPass)
                {
                    m_ArBackgroundPassFound = true;
                }

                if (isOpaquePass)
                {
                    m_OpaquePassClearStatus = $"{pass.clearFlag} ({pass.clearColor})";

                    // Apply the Anti-BlackScreen Clear Fix:
                    // If URP's UniversalRenderer enqueued DrawObjectsPass with ClearFlag.Color,
                    // it will clear the camera background that was just drawn. Suppress ClearFlag.Color!
                    if (m_EnableClearFix && (pass.clearFlag & ClearFlag.Color) != 0)
                    {
                        var newClear = pass.clearFlag & ~ClearFlag.Color;
                        pass.ConfigureClear(newClear, pass.clearColor);
                        m_ColorClearsSuppressedCount++;
                        m_OpaquePassClearStatus = $"{newClear} [COLOR_CLEAR_SUPPRESSED]";
                    }
                }

                sb.Append($"[{i}:{passName}|Evt={pass.renderPassEvent}|Clr={pass.clearFlag}] ");
            }

            m_PassQueueSummary = sb.ToString();
        }

        private string BuildDiagnosticText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AR Camera Pipeline Diagnosis ===");
            sb.AppendLine($"Device: {SystemInfo.deviceModel} (API: {SystemInfo.graphicsDeviceType})");
            sb.AppendLine($"Quality: [{QualitySettings.GetQualityLevel()}] {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sb.AppendLine($"URP Asset: {(urpAsset != null ? urpAsset.name : "None")}");

            var subsystem = m_CameraManager != null ? m_CameraManager.subsystem : null;
            sb.AppendLine($"Subsystem: {(subsystem != null ? (subsystem.running ? "Running" : "Stopped") : "NULL")}");

            sb.AppendLine($"Frames: {m_FrameCount} (Last: {(Time.realtimeSinceStartup - m_LastFrameTime):F1}s ago)");
            sb.AppendLine($"NativeTexPtr: {m_LastNativeTexPtr} | Details: {m_LastTextureDetails}");

            Material mat = m_CameraBackground != null ? m_CameraBackground.material : null;
            Texture mainTex = mat != null ? mat.GetTexture("_MainTex") : null;
            sb.AppendLine($"Material: {(mat != null ? mat.name : "NULL")} | Shader: {(mat != null && mat.shader != null ? mat.shader.name : "NULL")} (Supported: {m_ShaderSupported})");
            sb.AppendLine($"_MainTex on Mat: {(mainTex != null ? $"{mainTex.width}x{mainTex.height} (Ptr={mainTex.GetNativeTexturePtr()})" : "NULL")}");
            sb.AppendLine($"Mat Texture Properties: {m_TexturePropertyNames}");
            sb.AppendLine($"DisplayTransform: {m_DisplayTransformSummary}");
            sb.AppendLine($"Shader Keywords: {m_ShaderKeywords}");

            sb.AppendLine($"--- URP Pass Queue ({m_PassQueueCount} passes) ---");
            sb.AppendLine($"AR Bg Pass Present: {m_ArBackgroundPassFound}");
            sb.AppendLine($"Opaque Pass Clear: {m_OpaquePassClearStatus}");
            sb.AppendLine($"Clear-Fix Active: {m_EnableClearFix} (Suppressed: {m_ColorClearsSuppressedCount})");

            return sb.ToString();
        }

        private void LogDiagnosticState()
        {
            string diag = BuildDiagnosticText();
            Debug.Log($"[AR_DIAG]\n{diag}\nQueue Dump: {m_PassQueueSummary}");
        }

        private void OnGUI()
        {
            if (!m_ShowOnScreen)
                return;

            int pad = 20;
            int width = Mathf.Min(Screen.width - pad * 2, 750);
            int height = m_Collapsed ? 80 : 660;

            GUI.color = new Color(0f, 0f, 0f, 0.90f);
            GUI.Box(new Rect(pad, pad, width, height), GUIContent.none);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = m_FontSize,
                wordWrap = true
            };

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = m_FontSize
            };

            if (m_CameraBackground != null && m_CameraBackground.backgroundRenderingEnabled && m_LastTextureCount > 0)
            {
                style.normal.textColor = Color.green;
            }
            else
            {
                style.normal.textColor = Color.yellow;
            }

            if (m_Collapsed)
            {
                string header = $"AR Diag (Tap to Expand): Frames={m_FrameCount}, BgPass={m_ArBackgroundPassFound}, Fix={m_EnableClearFix}";
                GUI.Label(new Rect(pad + 15, pad + 15, width - 30, height - 30), header, style);

                if (Event.current.type == EventType.MouseDown && new Rect(pad, pad, width, height).Contains(Event.current.mousePosition))
                {
                    m_Collapsed = false;
                    Event.current.Use();
                }
                return;
            }

            // Expanded view
            string text = BuildDiagnosticText();
            GUI.Label(new Rect(pad + 15, pad + 15, width - 30, 480), text, style);

            // Interactive action buttons
            float btnY = pad + 500;
            float btnW = (width - 45) / 2f;
            float btnH = 65;

            // Toggle Clear-Fix
            string fixLabel = m_EnableClearFix ? "Clear-Fix: ACTIVE [ON]" : "Clear-Fix: DISABLED [OFF]";
            GUI.backgroundColor = m_EnableClearFix ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);
            if (GUI.Button(new Rect(pad + 15, btnY, btnW, btnH), fixLabel, btnStyle))
            {
                m_EnableClearFix = !m_EnableClearFix;
                Debug.Log($"[AR_DIAG] User toggled Clear-Fix to: {m_EnableClearFix}");
            }

            // Toggle Corner PIP Quad
            string pipLabel = m_EnableCornerPip ? "Corner PIP: [ON]" : "Corner PIP: [OFF]";
            GUI.backgroundColor = m_EnableCornerPip ? new Color(0.2f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f);
            if (GUI.Button(new Rect(pad + 25 + btnW, btnY, btnW, btnH), pipLabel, btnStyle))
            {
                m_EnableCornerPip = !m_EnableCornerPip;
                Debug.Log($"[AR_DIAG] User toggled Corner PIP to: {m_EnableCornerPip}");
            }

            // Logcat Dump Button & Collapse
            GUI.backgroundColor = Color.white;
            float btn2Y = btnY + btnH + 10;
            if (GUI.Button(new Rect(pad + 15, btn2Y, btnW, 55), "Dump Full Logcat", btnStyle))
            {
                LogDiagnosticState();
            }

            if (GUI.Button(new Rect(pad + 25 + btnW, btn2Y, btnW, 55), "Collapse Overlay", btnStyle))
            {
                m_Collapsed = true;
            }
        }

        internal Material GetCameraMaterial()
        {
            return m_CameraBackground != null ? m_CameraBackground.material : null;
        }

        #region Diagnostic Render Passes

        /// <summary>
        /// Runs right before opaques to inspect the active URP pass queue and suppress
        /// spurious color clears that erase the AR background frame.
        /// </summary>
        private class ARQueueMonitorPass : ScriptableRenderPass
        {
            private readonly ARDiagnosticOverlay m_Overlay;

            public ARQueueMonitorPass(ARDiagnosticOverlay overlay)
            {
                m_Overlay = overlay;
                // Run after prepasses but strictly before or at opaques
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Overlay != null && renderingData.cameraData.camera != null)
                {
                    m_Overlay.ProcessRenderQueue(renderingData.cameraData.renderer, ref renderingData);
                }
            }
        }

        /// <summary>
        /// Secondary verification pass that draws a small PIP preview quad in the screen corner
        /// after transparent objects using the AR camera material.
        /// This independently proves whether the camera texture and ARCore background shader are functioning.
        /// </summary>
        private class ARCornerPipPass : ScriptableRenderPass
        {
            private readonly ARDiagnosticOverlay m_Overlay;
            private Mesh m_PipMesh;
            private static readonly Matrix4x4 s_PipOrtho = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -0.1f, 9.9f);

            public ARCornerPipPass(ARDiagnosticOverlay overlay)
            {
                m_Overlay = overlay;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

                m_PipMesh = new Mesh();
                // Top-right corner quad occupying (0.65 to 0.98 in X, 0.65 to 0.98 in Y)
                m_PipMesh.vertices = new Vector3[]
                {
                    new Vector3(0.65f, 0.65f, 0.1f),
                    new Vector3(0.65f, 0.98f, 0.1f),
                    new Vector3(0.98f, 0.98f, 0.1f),
                    new Vector3(0.98f, 0.65f, 0.1f)
                };
                m_PipMesh.uv = new Vector2[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                };
                m_PipMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Overlay == null) return;
                var mat = m_Overlay.GetCameraMaterial();
                if (mat == null) return;

                var cmd = CommandBufferPool.Get("AR_Corner_PIP_Pass");
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, s_PipOrtho);
                cmd.DrawMesh(m_PipMesh, Matrix4x4.identity, mat);
                cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,
                                              renderingData.cameraData.camera.projectionMatrix);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        #endregion
    }
}
