using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobileARTemplateAssets.Scripts
{
    /// <summary>
    /// Manages the responsive layout and behavior of the Welcome / Onboarding screen.
    /// Ensures proper spacing, non-overlapping elements, safe area compliance,
    /// and reliable button interaction for mobile devices in portrait orientation.
    /// </summary>
    [ExecuteAlways]
    public class WelcomeScreenLayout : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform m_CardContainer;
        [SerializeField] private TextMeshProUGUI m_HeadingText;
        [SerializeField] private TextMeshProUGUI m_SubtitleText;
        [SerializeField] private Image m_LogoImage;
        [SerializeField] private Button m_GetStartedButton;
        [SerializeField] private GoalManager m_GoalManager;

        private Rect m_LastSafeArea = Rect.zero;
        private Vector2Int m_LastScreenSize = Vector2Int.zero;

        public RectTransform cardContainer => m_CardContainer;
        public TextMeshProUGUI headingText => m_HeadingText;
        public TextMeshProUGUI subtitleText => m_SubtitleText;
        public Image logoImage => m_LogoImage;
        public Button getStartedButton => m_GetStartedButton;

        private void Awake()
        {
            EnsureScrim();
            FindReferencesIfNull();
            ConfigureLayout();
            ApplySafeArea();
        }

        private void Start()
        {
            EnsureScrim();
            FindReferencesIfNull();
            ConfigureLayout();
            ApplySafeArea();
            WireButtonEvent();
        }

        private void Update()
        {
            if (m_LastSafeArea != Screen.safeArea ||
                m_LastScreenSize.x != Screen.width ||
                m_LastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        public void EnsureScrim()
        {
            // Dim translucent navy scrim covering camera feed during onboarding
            Image scrim = GetComponent<Image>();
            if (scrim == null)
            {
                scrim = gameObject.AddComponent<Image>();
            }
            scrim.sprite = null;
            scrim.color = new Color(0.06f, 0.09f, 0.16f, 0.88f);
            scrim.raycastTarget = true;
        }

        public void FindReferencesIfNull()
        {
            if (m_GoalManager == null)
            {
                m_GoalManager = FindObjectOfType<GoalManager>(true);
            }

            if (m_CardContainer == null)
            {
                Transform ctaTrans = transform.Find("GreetingCTA");
                if (ctaTrans == null && transform.childCount > 0)
                {
                    ctaTrans = transform.GetChild(0);
                }
                if (ctaTrans != null)
                {
                    m_CardContainer = ctaTrans.GetComponent<RectTransform>();
                }
            }

            if (m_CardContainer != null)
            {
                if (m_HeadingText == null)
                {
                    Transform h = m_CardContainer.Find("Welcome Heading");
                    if (h == null) h = m_CardContainer.Find("Text Greeting");
                    if (h != null) m_HeadingText = h.GetComponent<TextMeshProUGUI>();
                }

                if (m_SubtitleText == null)
                {
                    Transform s = m_CardContainer.Find("Welcome Subtitle");
                    if (s != null) m_SubtitleText = s.GetComponent<TextMeshProUGUI>();
                }

                if (m_LogoImage == null)
                {
                    Transform l = m_CardContainer.Find("Logo");
                    if (l == null) l = m_CardContainer.Find("Image");
                    if (l != null) m_LogoImage = l.GetComponent<Image>();
                }

                if (m_GetStartedButton == null)
                {
                    Transform b = m_CardContainer.Find("Get Started Button");
                    if (b == null) b = m_CardContainer.Find("Button");
                    if (b == null) b = m_CardContainer.Find("Continue Button");
                    if (b != null) m_GetStartedButton = b.GetComponent<Button>();
                }
            }
        }

        public void ConfigureLayout()
        {
            if (m_CardContainer == null) return;

            // Ensure GreetingCTA scale is strictly 1,1,1
            m_CardContainer.localScale = Vector3.one;

            // Ensure Logo preserves aspect ratio
            if (m_LogoImage != null)
            {
                m_LogoImage.preserveAspect = true;
            }

            // Ensure Heading text styling
            if (m_HeadingText != null)
            {
                m_HeadingText.alignment = TextAlignmentOptions.Center;
                m_HeadingText.enableWordWrapping = true;

                // If subtitle is separate, keep heading short
                if (m_SubtitleText != null)
                {
                    if (string.IsNullOrEmpty(m_HeadingText.text) || m_HeadingText.text.Contains("\n"))
                    {
                        m_HeadingText.text = "Welcome to MebelAR";
                    }
                }
                else
                {
                    // Combined heading + subtitle in one TMP component
                    if (string.IsNullOrEmpty(m_HeadingText.text) || !m_HeadingText.text.Contains("Welcome to MebelAR"))
                    {
                        m_HeadingText.text = "<b><size=32>Welcome to MebelAR</size></b>\n<size=8>\n</size><color=#CBD5E1><size=18>Visualize and arrange furniture in your space with augmented reality</size></color>";
                    }
                }
            }

            // Ensure Subtitle text styling if separate
            if (m_SubtitleText != null)
            {
                if (string.IsNullOrEmpty(m_SubtitleText.text))
                {
                    m_SubtitleText.text = "Visualize and arrange furniture in your space with augmented reality";
                }
                m_SubtitleText.alignment = TextAlignmentOptions.Center;
                m_SubtitleText.enableWordWrapping = true;
            }

            // Ensure Button text is "Get Started"
            if (m_GetStartedButton != null)
            {
                var btnTMP = m_GetStartedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTMP != null)
                {
                    btnTMP.text = "Get Started";
                    btnTMP.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        public void ApplySafeArea()
        {
            m_LastSafeArea = Screen.safeArea;
            m_LastScreenSize = new Vector2Int(Screen.width, Screen.height);

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null) return;

            // Keep card centered with comfortable margins
            if (m_CardContainer != null)
            {
                m_CardContainer.anchorMin = new Vector2(0.5f, 0.5f);
                m_CardContainer.anchorMax = new Vector2(0.5f, 0.5f);
                m_CardContainer.pivot = new Vector2(0.5f, 0.5f);
                m_CardContainer.anchoredPosition = Vector2.zero;

                // Responsive width: comfortable 580 on 800-width canvas
                float targetWidth = 580f;
                float targetHeight = 640f;

                m_CardContainer.sizeDelta = new Vector2(targetWidth, targetHeight);
            }
        }

        public void WireButtonEvent()
        {
            if (m_GetStartedButton == null) return;

            m_GetStartedButton.onClick.RemoveListener(OnGetStartedClicked);
            m_GetStartedButton.onClick.AddListener(OnGetStartedClicked);
        }

        private void OnGetStartedClicked()
        {
            if (m_GoalManager != null)
            {
                m_GoalManager.StartCoaching();
            }
            else
            {
                var gm = FindObjectOfType<GoalManager>(true);
                if (gm != null)
                {
                    gm.StartCoaching();
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
