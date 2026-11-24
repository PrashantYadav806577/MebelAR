using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable Text-To-Speech (TTS) manager that connects to Android's built-in
/// native TextToSpeech engine (android.speech.tts.TextToSpeech) without requiring
/// recorded audio files, external plugins, or paid APIs.
/// Includes Editor-friendly speech simulation and test hooks for rapid validation.
/// </summary>
public class AndroidTTSManager : MonoBehaviour
{
    private static AndroidTTSManager s_Instance;

    /// <summary>
    /// Singleton instance accessor. Automatically creates a GameObject if none exists.
    /// </summary>
    public static AndroidTTSManager Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = FindObjectOfType<AndroidTTSManager>();
                if (s_Instance == null)
                {
                    GameObject go = new GameObject("AndroidTTSManager");
                    s_Instance = go.AddComponent<AndroidTTSManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_Instance;
        }
    }

    /// <summary>
    /// Event triggered whenever an instruction is spoken (useful for unit/integration tests).
    /// </summary>
    public static event Action<string> onSpeechRequested;

    /// <summary>
    /// Last spoken instruction text.
    /// </summary>
    public static string lastSpokenText { get; private set; }

    private bool m_IsInitialized = false;
    private string m_PendingSpeech = null;
    private int m_UtteranceCount = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject m_TTS;
    private AndroidJavaObject m_Bundle;

    // Listener proxy implementing android.speech.tts.TextToSpeech.OnInitListener
    private class TTSOnInitListener : AndroidJavaProxy
    {
        private readonly Action<int> m_Callback;

        public TTSOnInitListener(Action<int> callback) 
            : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            m_Callback = callback;
        }

        public void onInit(int status)
        {
            m_Callback?.Invoke(status);
        }
    }
#endif

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeTTS();
    }

    /// <summary>
    /// Initializes the native Android TextToSpeech engine.
    /// </summary>
    public void InitializeTTS()
    {
        if (m_IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");

                var listener = new TTSOnInitListener(OnInitCompleted);
                m_TTS = new AndroidJavaObject("android.speech.tts.TextToSpeech", context, listener);
                m_Bundle = new AndroidJavaObject("android.os.Bundle");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidTTSManager] Failed to create Android TextToSpeech: {ex.Message}");
        }
#else
        // In Unity Editor, mark ready immediately
        m_IsInitialized = true;
        Debug.Log("[AndroidTTSManager] Initialized TTS in Editor simulation mode.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnInitCompleted(int status)
    {
        // TextToSpeech.SUCCESS is 0
        if (status == 0 && m_TTS != null)
        {
            try
            {
                using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                {
                    var usLocale = localeClass.GetStatic<AndroidJavaObject>("US");
                    int langResult = m_TTS.Call<int>("setLanguage", usLocale);

                    // LANG_MISSING_DATA = -1, LANG_NOT_SUPPORTED = -2
                    if (langResult == -1 || langResult == -2)
                    {
                        Debug.LogWarning("[AndroidTTSManager] US Locale not supported; falling back to default English.");
                        var engLocale = localeClass.GetStatic<AndroidJavaObject>("ENGLISH");
                        m_TTS.Call<int>("setLanguage", engLocale);
                    }
                }

                // Set speech rate to natural pacing (1.0f is normal speed)
                m_TTS.Call<int>("setSpeechRate", 1.0f);
                m_IsInitialized = true;
                Debug.Log("[AndroidTTSManager] Android native TextToSpeech engine initialized successfully.");

                // If speech was queued while initializing, speak it now
                if (!string.IsNullOrEmpty(m_PendingSpeech))
                {
                    string pending = m_PendingSpeech;
                    m_PendingSpeech = null;
                    Speak(pending, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidTTSManager] Exception during TTS language setup: {ex.Message}");
            }
        }
        else
        {
            Debug.LogError($"[AndroidTTSManager] TextToSpeech initialization failed with status: {status}");
        }
    }
#endif

    /// <summary>
    /// Speaks the given instruction text using Android's native TTS engine.
    /// If an instruction is already playing, cancels it cleanly (interrupt=true) to prevent overlapping.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="interrupt">If true, stops previous speech immediately.</param>
    public void Speak(string text, bool interrupt = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        lastSpokenText = text;
        onSpeechRequested?.Invoke(text);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!m_IsInitialized)
        {
            m_PendingSpeech = text;
            Debug.Log($"[AndroidTTSManager] TTS engine still initializing. Queued: \"{text}\"");
            return;
        }

        try
        {
            if (m_TTS != null)
            {
                if (interrupt)
                {
                    // Stop any ongoing speech to prevent overlapping
                    m_TTS.Call<int>("stop");
                }

                m_UtteranceCount++;
                string utteranceId = "MebelAR_Utterance_" + m_UtteranceCount;

                // QUEUE_FLUSH = 0, QUEUE_ADD = 1
                int queueMode = interrupt ? 0 : 1;

                // Call speak(CharSequence text, int queueMode, Bundle params, String utteranceId)
                m_TTS.Call<int>("speak", text, queueMode, m_Bundle, utteranceId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidTTSManager] Failed to speak text: {ex.Message}");
        }
#else
        Debug.Log($"<color=#14B8A6>[TTS Voice Guidance]</color> Speaking: \"{text}\"");
        SpeakEditorSimulation(text);
#endif
    }

    /// <summary>
    /// Stops any ongoing speech immediately.
    /// </summary>
    public void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (m_TTS != null && m_IsInitialized)
        {
            try
            {
                m_TTS.Call<int>("stop");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidTTSManager] Error stopping speech: {ex.Message}");
            }
        }
#endif
    }

    private void SpeakEditorSimulation(string text)
    {
#if UNITY_EDITOR && UNITY_STANDALONE_WIN
        // On Windows Editor, speak asynchronously using SAPI speech synth via PowerShell (non-blocking)
        try
        {
            string sanitized = text.Replace("'", "").Replace("\"", "");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).SpeakAsync('{sanitized}') | Out-Null\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Fail silently in Editor simulation
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (m_TTS != null)
        {
            try
            {
                m_TTS.Call<int>("stop");
                m_TTS.Call("shutdown");
                m_TTS = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidTTSManager] Error shutting down TTS: {ex.Message}");
            }
        }
#endif
    }
}

