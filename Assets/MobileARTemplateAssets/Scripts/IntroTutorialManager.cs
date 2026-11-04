using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a 4-step introduction tutorial shown between the Welcome screen and the AR experience.
/// Builds its UI programmatically under the existing Canvas, matching the project's navy/teal theme.
/// </summary>
public class IntroTutorialManager : MonoBehaviour
{
    // Tutorial step data
    struct TutorialStep
    {
        public string title;
        public string description;
        public string icon; // Unicode emoji used as a simple icon
    }

    static readonly TutorialStep[] s_Steps = new TutorialStep[]
    {
        new TutorialStep
        {
            title = "Scan Your Surface",
            description = "Move your phone slowly around the room.\nPoint the camera at the floor or any flat surface you want to use.",
            icon = "\U0001F4F1" // 📱
        },
        new TutorialStep
        {
            title = "Detect the Surface",
            description = "Wait for the surface indicator to appear.\nOnce detected, tap on the highlighted area to confirm placement.",
            icon = "\U0001F50D" // 🔍
        },
        new TutorialStep
        {
            title = "Select Furniture",
            description = "Open the furniture menu and browse the catalog.\nTap on any item to select it for placement.",
            icon = "\U0001F6CB" // 🛋
        },
        new TutorialStep
        {
            title = "Place and Customize",
            description = "Tap on the surface to place your furniture.\nDrag to move, pinch to resize, and use the lock button to fix it in place.",
            icon = "\u2728" // ✨
        }
    };

    // --- Runtime references (built dynamically) ---
    GameObject m_TutorialPanel;
    TextMeshProUGUI m_IconLabel;
    TextMeshProUGUI m_TitleLabel;
    TextMeshProUGUI m_DescriptionLabel;
    TextMeshProUGUI m_StepIndicatorLabel;
    Button m_NextButton;
    Button m_BackButton;
    Button m_SkipButton;
    TextMeshProUGUI m_NextButtonText;

    int m_CurrentStep;
    GoalManager m_GoalManager;
    TMP_FontAsset m_Font;

    /// <summary>
    /// Show the tutorial panel. Called by GoalManager.StartCoaching() instead of
    /// immediately entering the AR experience.
    /// </summary>
    public void ShowTutorial(GoalManager goalManager)
    {
        m_GoalManager = goalManager;
        m_CurrentStep = 0;

        if (m_TutorialPanel == null)
            BuildUI();

        m_TutorialPanel.SetActive(true);
        RefreshStep();
    }

    /// <summary>
    /// Called when the user finishes or skips the tutorial.
    /// Hides the tutorial panel and tells GoalManager to proceed to AR.
    /// </summary>
    void CompleteTutorial()
    {
        if (m_TutorialPanel != null)
            m_TutorialPanel.SetActive(false);

        if (m_GoalManager != null)
            m_GoalManager.StartARExperience();
    }

    void GoNext()
    {
        if (m_CurrentStep < s_Steps.Length - 1)
        {
            m_CurrentStep++;
            RefreshStep();
        }
        else
        {
            CompleteTutorial();
        }
    }

    void GoBack()
    {
        if (m_CurrentStep > 0)
        {
            m_CurrentStep--;
            RefreshStep();
        }
    }

    void RefreshStep()
    {
        var step = s_Steps[m_CurrentStep];
        bool isLast = m_CurrentStep == s_Steps.Length - 1;

        m_IconLabel.text = step.icon;
        m_TitleLabel.text = step.title;
        m_DescriptionLabel.text = step.description;
        m_StepIndicatorLabel.text = (m_CurrentStep + 1) + " of " + s_Steps.Length;

        // Back button: hidden on first step
        m_BackButton.gameObject.SetActive(m_CurrentStep > 0);

        // Next/Start button text
        m_NextButtonText.text = isLast ? "Start Designing" : "Next";

        // Skip button: hidden on last step (Next becomes "Start Designing")
        m_SkipButton.gameObject.SetActive(!isLast);

        // Update step dots
        RefreshDots();
    }

    // --- Step dot indicators ---
    GameObject[] m_Dots;

    void RefreshDots()
    {
        if (m_Dots == null) return;
        for (int i = 0; i < m_Dots.Length; i++)
        {
            var img = m_Dots[i].GetComponent<Image>();
            if (img != null)
            {
                bool active = (i == m_CurrentStep);
                img.color = active
                    ? new Color(0.08f, 0.72f, 0.65f, 1f)   // Teal #14B8A6
                    : new Color(0.45f, 0.55f, 0.65f, 0.5f); // Muted slate
                var dotRect = m_Dots[i].GetComponent<RectTransform>();
                dotRect.sizeDelta = active ? new Vector2(24f, 8f) : new Vector2(8f, 8f);
            }
        }
    }

    // ======================================================================
    // UI Construction — all programmatic, no scene/prefab changes needed
    // ======================================================================
    void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Try to grab the same font used by existing TMP text in the project
        var existingTMP = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existingTMP != null)
            m_Font = existingTMP.font;

        // --- Full-screen scrim panel ---
        m_TutorialPanel = new GameObject("IntroTutorialPanel");
        m_TutorialPanel.transform.SetParent(canvas.transform, false);
        m_TutorialPanel.transform.SetAsLastSibling();

        RectTransform panelRect = m_TutorialPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image scrim = m_TutorialPanel.AddComponent<Image>();
        scrim.sprite = null;
        scrim.color = new Color(0.06f, 0.09f, 0.16f, 0.92f); // Dark navy scrim
        scrim.raycastTarget = true;

        // --- Content card (centered) ---
        GameObject card = CreateChild(m_TutorialPanel, "TutorialCard");
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 10f);
        cardRect.sizeDelta = new Vector2(560f, 480f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.sprite = null;
        cardBg.color = new Color(0.09f, 0.14f, 0.23f, 0.98f); // Deep navy #17243A
        cardBg.raycastTarget = false;

        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.40f);
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // --- Step Indicator (top of card) ---
        m_StepIndicatorLabel = CreateTMP(card, "StepIndicator",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(200f, 30f),
            14f, FontStyles.Normal, new Color(0.80f, 0.84f, 0.88f, 0.8f),
            TextAlignmentOptions.Center, "1 of 4");

        // --- Icon ---
        m_IconLabel = CreateTMP(card, "Icon",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -65f), new Vector2(100f, 70f),
            48f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.Center, "");

        // --- Title ---
        m_TitleLabel = CreateTMP(card, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -140f), new Vector2(480f, 44f),
            26f, FontStyles.Bold, Color.white,
            TextAlignmentOptions.Center, "");

        // --- Description ---
        m_DescriptionLabel = CreateTMP(card, "Description",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -210f), new Vector2(460f, 90f),
            16f, FontStyles.Normal, new Color(0.80f, 0.84f, 0.88f, 1f),
            TextAlignmentOptions.Center, "");
        m_DescriptionLabel.enableWordWrapping = true;
        m_DescriptionLabel.lineSpacing = 8f;

        // --- Step Dots ---
        GameObject dotsContainer = CreateChild(card, "Dots");
        RectTransform dotsRect = dotsContainer.GetComponent<RectTransform>();
        dotsRect.anchorMin = new Vector2(0.5f, 1f);
        dotsRect.anchorMax = new Vector2(0.5f, 1f);
        dotsRect.pivot = new Vector2(0.5f, 1f);
        dotsRect.anchoredPosition = new Vector2(0f, -300f);
        dotsRect.sizeDelta = new Vector2(120f, 10f);

        HorizontalLayoutGroup hlg = dotsContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        m_Dots = new GameObject[s_Steps.Length];
        for (int i = 0; i < s_Steps.Length; i++)
        {
            GameObject dot = new GameObject("Dot" + i);
            dot.transform.SetParent(dotsContainer.transform, false);
            RectTransform dotRect = dot.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(8f, 8f);
            Image dotImg = dot.AddComponent<Image>();
            dotImg.sprite = null;
            dotImg.color = new Color(0.45f, 0.55f, 0.65f, 0.5f);
            dotImg.raycastTarget = false;
            m_Dots[i] = dot;
        }

        // --- Buttons row ---
        // Back button (bottom-left of card)
        m_BackButton = CreateButton(card, "BackButton",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30f, 30f), new Vector2(110f, 44f),
            "Back", 16f,
            new Color(0.18f, 0.26f, 0.38f, 0.95f), // Slate bg
            Color.white);
        m_BackButton.onClick.AddListener(GoBack);

        // Next / Start Designing button (bottom-right of card)
        m_NextButton = CreateButton(card, "NextButton",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 30f), new Vector2(170f, 44f),
            "Next", 16f,
            new Color(0.08f, 0.72f, 0.65f, 1f), // Teal #14B8A6
            Color.white);
        m_NextButtonText = m_NextButton.GetComponentInChildren<TextMeshProUGUI>();
        m_NextButton.onClick.AddListener(GoNext);

        // Skip Intro (top-right of full panel, not inside card)
        m_SkipButton = CreateButton(m_TutorialPanel, "SkipIntroButton",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -40f), new Vector2(120f, 36f),
            "Skip Intro", 14f,
            new Color(0.18f, 0.26f, 0.38f, 0.70f), // Subtle slate
            new Color(0.80f, 0.84f, 0.88f, 0.9f));
        m_SkipButton.onClick.AddListener(CompleteTutorial);
    }

    // ======================================================================
    // Helper methods for UI construction
    // ======================================================================
    GameObject CreateChild(GameObject parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    TextMeshProUGUI CreateTMP(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        float fontSize, FontStyles style, Color color,
        TextAlignmentOptions alignment, string text)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (m_Font != null) tmp.font = m_Font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.text = text;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    Button CreateButton(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        string label, float fontSize,
        Color bgColor, Color textColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.sprite = null;
        bg.color = bgColor;
        bg.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;

        // Button label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8f, 0f);
        labelRT.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        if (m_Font != null) tmp.font = m_Font;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }
}

