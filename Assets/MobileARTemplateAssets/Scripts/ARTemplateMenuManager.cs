using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Handles dismissing the object menu when clicking out the UI bounds, and showing the
/// menu again when the create menu button is clicked after dismissal. Manages object deletion in the AR demo scene,
/// lock/unlock state of selected furniture, selected item name and dimension display, and placement hints.
/// </summary>
public class ARTemplateMenuManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Button that opens the create menu.")]
    Button m_CreateButton;

    /// <summary>
    /// Button that opens the create menu.
    /// </summary>
    public Button createButton
    {
        get => m_CreateButton;
        set => m_CreateButton = value;
    }

    [SerializeField]
    [Tooltip("Button that deletes a selected object.")]
    Button m_DeleteButton;

    /// <summary>
    /// Button that deletes a selected object.
    /// </summary>
    public Button deleteButton
    {
        get => m_DeleteButton;
        set => m_DeleteButton = value;
    }

    [SerializeField]
    [Tooltip("The menu with all the creatable objects.")]
    GameObject m_ObjectMenu;

    /// <summary>
    /// The menu with all the creatable objects.
    /// </summary>
    public GameObject objectMenu
    {
        get => m_ObjectMenu;
        set => m_ObjectMenu = value;
    }

    [SerializeField]
    [Tooltip("The modal with debug options.")]
    GameObject m_ModalMenu;

    /// <summary>
    /// The modal with debug options.
    /// </summary>
    public GameObject modalMenu
    {
        get => m_ModalMenu;
        set => m_ModalMenu = value;
    }

    [SerializeField]
    [Tooltip("The animator for the object creation menu.")]
    Animator m_ObjectMenuAnimator;

    /// <summary>
    /// The animator for the object creation menu.
    /// </summary>
    public Animator objectMenuAnimator
    {
        get => m_ObjectMenuAnimator;
        set => m_ObjectMenuAnimator = value;
    }

    [SerializeField]
    [Tooltip("The object spawner component in charge of spawning new objects.")]
    ObjectSpawner m_ObjectSpawner;

    /// <summary>
    /// The object spawner component in charge of spawning new objects.
    /// </summary>
    public ObjectSpawner objectSpawner
    {
        get => m_ObjectSpawner;
        set => m_ObjectSpawner = value;
    }

    [SerializeField]
    [Tooltip("Button that closes the object creation menu.")]
    Button m_CancelButton;

    /// <summary>
    /// Button that closes the object creation menu.
    /// </summary>
    public Button cancelButton
    {
        get => m_CancelButton;
        set => m_CancelButton = value;
    }

    [SerializeField]
    [Tooltip("The screen space controller associated with the demo scene.")]
    XRScreenSpaceController m_ScreenSpaceController;

    /// <summary>
    /// The screen space controller associated with the demo scene.
    /// </summary>
    public XRScreenSpaceController screenSpaceController
    {
        get => m_ScreenSpaceController;
        set => m_ScreenSpaceController = value;
    }

    [SerializeField]
    [Tooltip("The interaction group for the AR demo scene.")]
    XRInteractionGroup m_InteractionGroup;

    /// <summary>
    /// The interaction group for the AR demo scene.
    /// </summary>
    public XRInteractionGroup interactionGroup
    {
        get => m_InteractionGroup;
        set => m_InteractionGroup = value;
    }

    [SerializeField]
    [Tooltip("The slider for activating plane debug visuals.")]
    DebugSlider m_DebugPlaneSlider;

    /// <summary>
    /// The slider for activating plane debug visuals.
    /// </summary>
    public DebugSlider debugPlaneSlider
    {
        get => m_DebugPlaneSlider;
        set => m_DebugPlaneSlider = value;
    }

    [SerializeField]
    [Tooltip("The plane prefab with shadows and debug visuals.")]
    GameObject m_DebugPlane;

    /// <summary>
    /// The plane prefab with shadows and debug visuals.
    /// </summary>
    public GameObject debugPlane
    {
        get => m_DebugPlane;
        set => m_DebugPlane = value;
    }

    [SerializeField]
    [Tooltip("The plane manager in the AR demo scene.")]
    ARPlaneManager m_PlaneManager;

    /// <summary>
    /// The plane manager in the AR demo scene.
    /// </summary>
    public ARPlaneManager planeManager
    {
        get => m_PlaneManager;
        set => m_PlaneManager = value;
    }

    [SerializeField]
    [Tooltip("The AR debug menu.")]
    ARDebugMenu m_DebugMenu;

    /// <summary>
    /// The AR debug menu.
    /// </summary>
    public ARDebugMenu debugMenu
    {
        get => m_DebugMenu;
        set => m_DebugMenu = value;
    }

    [SerializeField]
    [Tooltip("The slider for activating the debug menu.")]
    DebugSlider m_DebugMenuSlider;

    /// <summary>
    /// The slider for activating the debug menu.
    /// </summary>
    public DebugSlider debugMenuSlider
    {
        get => m_DebugMenuSlider;
        set => m_DebugMenuSlider = value;
    }

    [SerializeField]
    [Tooltip("Optional TextMeshProUGUI label shown briefly when hints or warnings are displayed.")]
    TextMeshProUGUI m_NoSelectionHintLabel;

    /// <summary>
    /// Optional label shown when the user taps the floor without selecting furniture first.
    /// </summary>
    public TextMeshProUGUI noSelectionHintLabel
    {
        get => m_NoSelectionHintLabel;
        set => m_NoSelectionHintLabel = value;
    }

    [Header("Feature UI References")]
    [SerializeField]
    GameObject m_SelectedNameBadgeGO;

    [SerializeField]
    TextMeshProUGUI m_SelectedFurnitureLabel;

    [SerializeField]
    Button m_LockButton;

    [SerializeField]
    TextMeshProUGUI m_LockButtonLabel;

    [SerializeField]
    Image m_LockButtonImage;

    [SerializeField]
    GameObject m_LockButtonCheckIndicator;

    string m_SelectedFurnitureName;

    /// <summary>
    /// Whether the furniture catalog menu is currently open.
    /// </summary>
    public bool showObjectMenu => m_ShowObjectMenu;

    bool m_IsPointerOverUI;
    bool m_ShowObjectMenu;
    bool m_ShowOptionsModal;
    bool m_InitializingDebugMenu;
    // Tracks whether the user selected a catalog item during the current menu session.
    bool m_ItemSelectedThisSession;
    Vector2 m_ObjectButtonOffset = Vector2.zero;
    Vector2 m_ObjectMenuOffset = Vector2.zero;
    readonly List<ARFeatheredPlaneMeshVisualizerCompanion> featheredPlaneMeshVisualizerCompanions = new List<ARFeatheredPlaneMeshVisualizerCompanion>();

    void Awake()
    {
        EnsureFeaturesUI();
    }

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    void OnEnable()
    {
        EnsureFeaturesUI();
        m_ScreenSpaceController.dragCurrentPositionAction.action.started += HideTapOutsideUI;
        m_ScreenSpaceController.tapStartPositionAction.action.started += HideTapOutsideUI;
        m_CreateButton.onClick.AddListener(ShowMenu);
        m_CancelButton.onClick.AddListener(HideMenu);
        m_DeleteButton.onClick.AddListener(DeleteFocusedObject);
        m_PlaneManager.planesChanged += OnPlaneChanged;

        ObjectSpawner.onSpawnBlockedNoSelection += ShowNoSelectionHint;
        ObjectSpawner.onSpawnBlockedOverlap += ShowOverlapHint;
        ObjectSpawner.placementValidator = ARObjectDimensions.ValidateSpawnCandidate;
        ARObjectDimensions.onLockStateChanged += OnLockStateChanged;
        ARObjectDimensions.onSelectionChanged += OnSelectionChanged;

        if (m_ObjectSpawner != null)
            m_ObjectSpawner.objectSpawned += OnFurnitureSpawned;
    }

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    void OnDisable()
    {
        m_ShowObjectMenu = false;
        m_ScreenSpaceController.dragCurrentPositionAction.action.started -= HideTapOutsideUI;
        m_ScreenSpaceController.tapStartPositionAction.action.started -= HideTapOutsideUI;
        m_CreateButton.onClick.RemoveListener(ShowMenu);
        m_CancelButton.onClick.RemoveListener(HideMenu);
        m_DeleteButton.onClick.RemoveListener(DeleteFocusedObject);
        m_PlaneManager.planesChanged -= OnPlaneChanged;

        ObjectSpawner.onSpawnBlockedNoSelection -= ShowNoSelectionHint;
        ObjectSpawner.onSpawnBlockedOverlap -= ShowOverlapHint;
        ObjectSpawner.placementValidator = null;
        ARObjectDimensions.onLockStateChanged -= OnLockStateChanged;
        ARObjectDimensions.onSelectionChanged -= OnSelectionChanged;

        if (m_ObjectSpawner != null)
            m_ObjectSpawner.objectSpawned -= OnFurnitureSpawned;
    }

    void OnFurnitureSpawned(GameObject spawnedObj)
    {
        if (spawnedObj != null)
        {
            var dims = spawnedObj.GetComponent<ARObjectDimensions>();
            if (dims == null)
                dims = spawnedObj.AddComponent<ARObjectDimensions>();

            ARObjectDimensions.SelectObject(dims);
            m_SelectedFurnitureName = dims.DisplayName;
            UpdateSelectedNameBadge();
            UpdateLockButtonVisual();
        }
    }

    void OnSelectionChanged(ARObjectDimensions obj)
    {
        if (obj != null)
        {
            m_SelectedFurnitureName = obj.DisplayName;
        }
        UpdateSelectedNameBadge();
        UpdateLockButtonVisual();
    }

    void OnLockStateChanged(ARObjectDimensions obj, bool locked)
    {
        UpdateLockButtonVisual();
    }

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    void Start()
    {
        if (m_DebugMenu != null)
            m_DebugMenu.gameObject.SetActive(false);
        m_InitializingDebugMenu = false;

        InitializeDebugMenuOffsets();
        HideMenu();
        if (m_PlaneManager != null)
            m_PlaneManager.planePrefab = m_DebugPlane;

        // Ensure no furniture is selected at startup.
        if (m_ObjectSpawner != null)
            m_ObjectSpawner.spawnOptionIndex = -1;

        // Ensure dynamic feature UI (Selected Name Badge, Lock Button, Hint Label) exists
        EnsureFeaturesUI();

        // Hide the hint label at startup.
        if (m_NoSelectionHintLabel != null)
            m_NoSelectionHintLabel.gameObject.SetActive(false);

        UpdateSelectedNameBadge();
    }

    void EnsureFeaturesUI()
    {
        Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Try to obtain a valid font from existing text components on Canvas
        TMP_FontAsset defaultFont = null;
        var existingTMP = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existingTMP != null)
            defaultFont = existingTMP.font;

        // 1. Resolve Hint Label if null
        if (m_NoSelectionHintLabel == null)
        {
            var hintTrans = canvas.transform.Find("No Selection Hint");
            if (hintTrans != null)
                m_NoSelectionHintLabel = hintTrans.GetComponent<TextMeshProUGUI>();
        }

        // 2. Build Selected Furniture Item Name Badge (Top-left, clean compact rectangle)
        if (m_SelectedNameBadgeGO == null)
        {
            Transform existingBadge = canvas.transform.Find("SelectedFurnitureBadge");
            if (existingBadge != null)
            {
                m_SelectedNameBadgeGO = existingBadge.gameObject;
                m_SelectedNameBadgeGO.transform.SetAsLastSibling();
                m_SelectedFurnitureLabel = existingBadge.GetComponentInChildren<TextMeshProUGUI>();
                if (m_SelectedFurnitureLabel != null && defaultFont != null && m_SelectedFurnitureLabel.font == null)
                    m_SelectedFurnitureLabel.font = defaultFont;

                var bg = existingBadge.GetComponent<Image>();
                if (bg != null)
                {
                    bg.sprite = null; // Clean rectangular card (no pill/oval)
                    bg.color = new Color(0.09f, 0.14f, 0.23f, 0.94f); // Deep navy #17243A
                }

                var outline = existingBadge.GetComponent<Outline>() ?? existingBadge.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.60f);
                outline.effectDistance = new Vector2(1f, -1f);

                if (existingBadge.TryGetComponent<RectTransform>(out var r))
                {
                    r.anchorMin = new Vector2(0f, 1f);
                    r.anchorMax = new Vector2(0f, 1f);
                    r.pivot = new Vector2(0f, 1f);
                    r.anchoredPosition = new Vector2(24f, -36f);
                }
            }
            else
            {
                m_SelectedNameBadgeGO = new GameObject("SelectedFurnitureBadge");
                m_SelectedNameBadgeGO.transform.SetParent(canvas.transform, false);
                m_SelectedNameBadgeGO.transform.SetAsLastSibling();

                RectTransform badgeRect = m_SelectedNameBadgeGO.AddComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(0f, 1f);
                badgeRect.pivot = new Vector2(0f, 1f);
                badgeRect.anchoredPosition = new Vector2(24f, -36f);
                badgeRect.sizeDelta = new Vector2(180f, 42f);

                Image bg = m_SelectedNameBadgeGO.AddComponent<Image>();
                bg.sprite = null; // Clean square-corner rectangle
                bg.color = new Color(0.09f, 0.14f, 0.23f, 0.94f); // Deep navy #17243A
                bg.raycastTarget = false;

                var outline = m_SelectedNameBadgeGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.60f);
                outline.effectDistance = new Vector2(1f, -1f);

                GameObject textGO = new GameObject("Label");
                textGO.transform.SetParent(m_SelectedNameBadgeGO.transform, false);

                RectTransform textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(14f, 0f);
                textRect.offsetMax = new Vector2(-14f, 0f);

                m_SelectedFurnitureLabel = textGO.AddComponent<TextMeshProUGUI>();
                if (defaultFont != null)
                    m_SelectedFurnitureLabel.font = defaultFont;
                m_SelectedFurnitureLabel.fontSize = 18f;
                m_SelectedFurnitureLabel.fontStyle = FontStyles.Bold;
                m_SelectedFurnitureLabel.color = Color.white;
                m_SelectedFurnitureLabel.alignment = TextAlignmentOptions.MidlineLeft;
                m_SelectedFurnitureLabel.enableWordWrapping = false;
                m_SelectedFurnitureLabel.overflowMode = TextOverflowModes.Ellipsis;
                m_SelectedFurnitureLabel.raycastTarget = false;
                m_SelectedFurnitureLabel.text = "";

                m_SelectedNameBadgeGO.SetActive(false);
            }
        }

        // 3. Build Lock / Unlock Button (Bottom-left corner, clean rectangle with check indicator)
        if (m_LockButton == null)
        {
            Transform existingLock = canvas.transform.Find("Lock Button");
            if (existingLock != null)
            {
                m_LockButton = existingLock.GetComponent<Button>();
                m_LockButtonImage = existingLock.GetComponent<Image>();
                m_LockButtonLabel = existingLock.GetComponentInChildren<TextMeshProUGUI>();
                if (m_LockButtonLabel != null && defaultFont != null && m_LockButtonLabel.font == null)
                    m_LockButtonLabel.font = defaultFont;

                if (m_LockButtonImage != null)
                    m_LockButtonImage.sprite = null; // Clean square-corner rectangle

                var outline = existingLock.GetComponent<Outline>() ?? existingLock.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.60f);
                outline.effectDistance = new Vector2(1f, -1f);

                if (existingLock.TryGetComponent<RectTransform>(out var r))
                {
                    r.anchorMin = new Vector2(0f, 0f);
                    r.anchorMax = new Vector2(0f, 0f);
                    r.pivot = new Vector2(0f, 0f);
                    r.anchoredPosition = new Vector2(24f, 130f);
                    r.sizeDelta = new Vector2(145f, 44f);
                }

                var checkTrans = existingLock.Find("CheckboxBox/CheckIndicator");
                if (checkTrans != null)
                {
                    m_LockButtonCheckIndicator = checkTrans.gameObject;
                }
                else
                {
                    EnsureCheckboxElements(existingLock.gameObject);
                }

                m_LockButton.onClick.RemoveListener(OnLockButtonClicked);
                m_LockButton.onClick.AddListener(OnLockButtonClicked);
                existingLock.SetAsLastSibling();
            }
            else
            {
                GameObject lockGO = new GameObject("Lock Button");
                lockGO.transform.SetParent(canvas.transform, false);
                lockGO.transform.SetAsLastSibling();

                RectTransform lockRect = lockGO.AddComponent<RectTransform>();
                lockRect.anchorMin = new Vector2(0f, 0f);
                lockRect.anchorMax = new Vector2(0f, 0f);
                lockRect.pivot = new Vector2(0f, 0f);
                lockRect.localScale = Vector3.one;
                lockRect.sizeDelta = new Vector2(145f, 44f);
                lockRect.anchoredPosition = new Vector2(24f, 130f); // Respects safe area, above bottom navigation bar

                m_LockButtonImage = lockGO.AddComponent<Image>();
                m_LockButtonImage.sprite = null; // Clean square-corner rectangle
                m_LockButtonImage.color = new Color(0.09f, 0.14f, 0.23f, 0.95f); // Deep navy #17243A

                var outline = lockGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.60f);
                outline.effectDistance = new Vector2(1f, -1f);

                m_LockButton = lockGO.AddComponent<Button>();
                m_LockButton.onClick.AddListener(OnLockButtonClicked);

                EnsureCheckboxElements(lockGO);

                GameObject labelGO = new GameObject("Label");
                labelGO.transform.SetParent(lockGO.transform, false);

                RectTransform labelRect = labelGO.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(40f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);

                m_LockButtonLabel = labelGO.AddComponent<TextMeshProUGUI>();
                if (defaultFont != null)
                    m_LockButtonLabel.font = defaultFont;
                m_LockButtonLabel.text = "Lock";
                m_LockButtonLabel.fontSize = 17f;
                m_LockButtonLabel.fontStyle = FontStyles.Bold;
                m_LockButtonLabel.alignment = TextAlignmentOptions.MidlineLeft;
                m_LockButtonLabel.color = Color.white;
                m_LockButtonLabel.raycastTarget = false;

                lockGO.SetActive(false);
            }
        }
    }

    void EnsureCheckboxElements(GameObject lockGO)
    {
        Transform boxTrans = lockGO.transform.Find("CheckboxBox");
        if (boxTrans == null)
        {
            GameObject boxGO = new GameObject("CheckboxBox");
            boxGO.transform.SetParent(lockGO.transform, false);
            RectTransform boxRect = boxGO.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(12f, 0f);
            boxRect.sizeDelta = new Vector2(20f, 20f);

            Image boxImg = boxGO.AddComponent<Image>();
            boxImg.sprite = null;
            boxImg.color = new Color(0.18f, 0.26f, 0.38f, 0.95f);
            boxImg.raycastTarget = false;

            var boxOutline = boxGO.AddComponent<Outline>();
            boxOutline.effectColor = Color.white;
            boxOutline.effectDistance = new Vector2(1f, -1f);

            GameObject checkGO = new GameObject("CheckIndicator");
            checkGO.transform.SetParent(boxGO.transform, false);
            RectTransform checkRect = checkGO.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(12f, 12f);

            Image checkImg = checkGO.AddComponent<Image>();
            checkImg.sprite = null;
            checkImg.color = Color.white;
            checkImg.raycastTarget = false;

            m_LockButtonCheckIndicator = checkGO;
            checkGO.SetActive(false);
        }
        else
        {
            Transform checkTrans = boxTrans.Find("CheckIndicator");
            if (checkTrans != null)
                m_LockButtonCheckIndicator = checkTrans.gameObject;
        }
    }

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    void Update()
    {
        if (m_InitializingDebugMenu)
        {
            if (m_DebugMenu != null)
                m_DebugMenu.gameObject.SetActive(false);
            m_InitializingDebugMenu = false;
        }

        var focused = m_InteractionGroup?.focusInteractable;
        ARObjectDimensions.UpdateFocusedInteractable(focused);

        bool hasSelectedObject = (ARObjectDimensions.currentSelected != null);

        // Delete button
        if (m_DeleteButton != null)
            m_DeleteButton.gameObject.SetActive((focused != null || hasSelectedObject) && !m_ShowObjectMenu);

        // Lock / Unlock button (bottom-left corner)
        if (m_LockButton != null)
        {
            bool showLock = hasSelectedObject && !m_ShowObjectMenu;
            if (m_LockButton.gameObject.activeSelf != showLock)
                m_LockButton.gameObject.SetActive(showLock);

            if (showLock)
            {
                UpdateLockButtonVisual();
            }
        }

        if (m_ShowObjectMenu || m_ShowOptionsModal)
        {
            m_IsPointerOverUI = CheckPointerOverUI();
        }
        else
        {
            m_IsPointerOverUI = false;
            if (m_CreateButton != null)
                m_CreateButton.gameObject.SetActive(true);
        }

        if (!m_IsPointerOverUI && m_ShowOptionsModal)
        {
            m_IsPointerOverUI = CheckPointerOverUI();
        }

        UpdateSelectedNameBadge();
    }

    void OnLockButtonClicked()
    {
        if (ARObjectDimensions.currentSelected != null)
        {
            ARObjectDimensions.currentSelected.ToggleLock();
            UpdateLockButtonVisual();
            UpdateSelectedNameBadge();
        }
    }

    void UpdateLockButtonVisual()
    {
        if (m_LockButton == null) return;

        bool isLocked = ARObjectDimensions.currentSelected != null && ARObjectDimensions.currentSelected.isLocked;

        // Visual check/uncheck indicator (checked/filled when locked, empty when unlocked)
        if (m_LockButtonCheckIndicator != null)
        {
            m_LockButtonCheckIndicator.SetActive(isLocked);
        }

        if (m_LockButtonLabel != null)
        {
            m_LockButtonLabel.text = isLocked ? "Unlock" : "Lock";
        }

        if (m_LockButtonImage != null)
        {
            // Crisp Coral #FF8066 when locked, Deep Navy #17243A when unlocked
            m_LockButtonImage.color = isLocked
                ? new Color(0.95f, 0.42f, 0.32f, 0.98f)  // Coral #FF8066
                : new Color(0.09f, 0.14f, 0.23f, 0.95f);  // Deep Navy #17243A
        }

        if (m_LockButton.TryGetComponent<Outline>(out var outline))
        {
            outline.effectColor = isLocked
                ? new Color(1.0f, 0.75f, 0.70f, 0.85f)
                : new Color(0.25f, 0.35f, 0.50f, 0.60f);
        }
    }

    void UpdateSelectedNameBadge()
    {
        if (m_SelectedNameBadgeGO == null || m_SelectedFurnitureLabel == null) return;

        // Don't overlap the catalog menu when open
        if (m_ShowObjectMenu)
        {
            m_SelectedNameBadgeGO.SetActive(false);
            return;
        }

        if (ARObjectDimensions.currentSelected != null)
        {
            m_SelectedFurnitureName = ARObjectDimensions.currentSelected.DisplayName;
        }
        else if (m_ObjectSpawner != null && m_ObjectSpawner.spawnOptionIndex >= 0 && m_ObjectSpawner.spawnOptionIndex < m_ObjectSpawner.objectPrefabs.Count)
        {
            var prefab = m_ObjectSpawner.objectPrefabs[m_ObjectSpawner.spawnOptionIndex];
            var fItem = prefab.GetComponentInChildren<FurnitureItem>(true);
            if (fItem != null && !string.IsNullOrEmpty(fItem.DisplayName) && !FurnitureItem.IsGenericDummyName(fItem.DisplayName))
                m_SelectedFurnitureName = fItem.DisplayName;
            else
                m_SelectedFurnitureName = FurnitureItem.CleanName(prefab.name);
        }

        if (!string.IsNullOrEmpty(m_SelectedFurnitureName))
        {
            m_SelectedFurnitureLabel.text = m_SelectedFurnitureName;

            // Dynamically adjust rectangular badge width to fit the exact name with clean padding
            if (m_SelectedNameBadgeGO.TryGetComponent<RectTransform>(out var badgeRect))
            {
                float preferredW = m_SelectedFurnitureLabel.preferredWidth;
                badgeRect.sizeDelta = new Vector2(Mathf.Clamp(preferredW + 36f, 130f, 360f), 42f);
            }

            if (!m_SelectedNameBadgeGO.activeSelf)
                m_SelectedNameBadgeGO.SetActive(true);
        }
        else
        {
            if (m_SelectedNameBadgeGO.activeSelf)
                m_SelectedNameBadgeGO.SetActive(false);
        }
    }

    bool CheckPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;
        if (EventSystem.current.IsPointerOverGameObject(-1)) return true;

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var t = Touchscreen.current.touches[i];
                if (t.isInProgress)
                {
                    int fingerId = t.touchId.ReadValue();
                    if (EventSystem.current.IsPointerOverGameObject(fingerId))
                        return true;
                }
            }
        }
#endif
        return false;
    }

    /// <summary>
    /// Set the index of the object in the list on the ObjectSpawner to a specific value.
    /// This is effectively an override of the default behavior or randomly spawning an object.
    /// </summary>
    /// <param name="objectIndex">The index in the array of the object to spawn with the ObjectSpawner</param>
    public void SetObjectToSpawn(int objectIndex)
    {
        if (m_ObjectSpawner == null)
        {
            Debug.LogWarning("Object Spawner not configured correctly: no ObjectSpawner set.");
        }
        else
        {
            if (m_ObjectSpawner.objectPrefabs.Count > objectIndex)
            {
                m_ObjectSpawner.spawnOptionIndex = objectIndex;
                m_ItemSelectedThisSession = true;   // user explicitly picked an item

                // User picked a new item to spawn: deselect any placed furniture
                ARObjectDimensions.DeselectAll();

                var prefab = m_ObjectSpawner.objectPrefabs[objectIndex];
                var fItem = prefab.GetComponentInChildren<FurnitureItem>(true);
                if (fItem != null && !string.IsNullOrEmpty(fItem.DisplayName) && !FurnitureItem.IsGenericDummyName(fItem.DisplayName))
                    m_SelectedFurnitureName = fItem.DisplayName;
                else
                    m_SelectedFurnitureName = FurnitureItem.CleanName(prefab.name);
            }
            else
            {
                Debug.LogWarning("Object Spawner not configured correctly: object index larger than number of Object Prefabs.");
            }
        }

        HideMenu();
        UpdateSelectedNameBadge();
        UpdateLockButtonVisual();
    }

    /// <summary>
    /// Shows a brief "Please select furniture first" hint. Called externally when the user taps
    /// the floor without having chosen an item from the catalog.
    /// </summary>
    public void ShowNoSelectionHint()
    {
        if (m_NoSelectionHintLabel == null) return;
        StopCoroutine(nameof(HideHintAfterDelay));
        m_NoSelectionHintLabel.text = "Please select furniture first";
        m_NoSelectionHintLabel.gameObject.SetActive(true);
        StartCoroutine(nameof(HideHintAfterDelay));
    }

    /// <summary>
    /// Shows a brief warning when a furniture placement is blocked due to overlapping an existing object.
    /// </summary>
    public void ShowOverlapHint()
    {
        if (m_NoSelectionHintLabel == null) return;
        StopCoroutine(nameof(HideHintAfterDelay));
        m_NoSelectionHintLabel.text = "Cannot place: Space occupied by another item";
        m_NoSelectionHintLabel.gameObject.SetActive(true);
        StartCoroutine(nameof(HideHintAfterDelay));
    }

    IEnumerator HideHintAfterDelay()
    {
        yield return new WaitForSeconds(2.2f);
        if (m_NoSelectionHintLabel != null)
            m_NoSelectionHintLabel.gameObject.SetActive(false);
    }

    void ShowMenu()
    {
        m_ShowObjectMenu = true;
        m_ObjectMenu.SetActive(true);
        if (!m_ObjectMenuAnimator.GetBool("Show"))
        {
            m_ObjectMenuAnimator.SetBool("Show", true);
        }
        AdjustARDebugMenuPosition();
        UpdateSelectedNameBadge();
    }

    /// <summary>
    /// Shows or hides the menu modal when the options button is clicked.
    /// </summary>
    public void ShowHideModal()
    {
        if (m_ModalMenu.activeSelf)
        {
            m_ShowOptionsModal = false;
            m_ModalMenu.SetActive(false);
        }
        else
        {
            m_ShowOptionsModal = true;
            m_ModalMenu.SetActive(true);
        }
    }

    /// <summary>
    /// Shows or hides the plane debug visuals.
    /// </summary>
    public void ShowHideDebugPlane()
    {
        if (m_DebugPlaneSlider.value == 1)
        {
            m_DebugPlaneSlider.value = 0;
            ChangePlaneVisibility(false);
        }
        else
        {
            m_DebugPlaneSlider.value = 1;
            ChangePlaneVisibility(true);
        }
    }

    /// <summary>
    /// Shows or hides the AR debug menu.
    /// </summary>
    public void ShowHideDebugMenu()
    {
        if (m_DebugMenu == null) return;

        if (m_DebugMenu.gameObject.activeSelf)
        {
            if (m_DebugMenuSlider != null)
                m_DebugMenuSlider.value = 0;
            m_DebugMenu.gameObject.SetActive(false);
        }
        else
        {
            if (m_DebugMenuSlider != null)
                m_DebugMenuSlider.value = 1;
            m_DebugMenu.gameObject.SetActive(true);
            AdjustARDebugMenuPosition();
        }
    }

    /// <summary>
    /// Clear all created objects in the scene.
    /// </summary>
    public void ClearAllObjects()
    {
        foreach (Transform child in m_ObjectSpawner.transform)
        {
            Destroy(child.gameObject);
        }
        ARObjectDimensions.DeselectAll();
        m_SelectedFurnitureName = null;
        UpdateSelectedNameBadge();
    }

    /// <summary>
    /// Triggers hide animation for menu.
    /// </summary>
    public void HideMenu()
    {
        m_ObjectMenuAnimator.SetBool("Show", false);
        m_ShowObjectMenu = false;

        // If the user closed the menu WITHOUT selecting an item, reset the spawn index to -1
        // so that floor taps continue to be blocked by ObjectSpawner.requireExplicitSelection.
        if (!m_ItemSelectedThisSession)
        {
            if (m_ObjectSpawner != null)
                m_ObjectSpawner.spawnOptionIndex = -1;
        }

        m_ItemSelectedThisSession = false;  // reset for next menu session
        AdjustARDebugMenuPosition();
        UpdateSelectedNameBadge();
    }

    void ChangePlaneVisibility(bool setVisible)
    {
        var count = featheredPlaneMeshVisualizerCompanions.Count;
        for (int i = 0; i < count; ++i)
        {
            featheredPlaneMeshVisualizerCompanions[i].visualizeSurfaces = setVisible;
        }
    }

    void HideTapOutsideUI(InputAction.CallbackContext context)
    {
        // Don't dismiss menus if pointer is over any UI element
        if (CheckPointerOverUI())
            return;

        if (context.valueType == typeof(Vector2))
        {
            Vector2 tapPos = context.ReadValue<Vector2>();
            if (EventSystem.current != null)
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current) { position = tapPos };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                if (results.Count > 0)
                    return; // Tap was on a UI element
            }
        }

        if (m_ShowObjectMenu)
            HideMenu();
        if (m_ShowOptionsModal && m_ModalMenu != null)
            m_ModalMenu.SetActive(false);
    }

    void DeleteFocusedObject()
    {
        var currentFocusedObject = m_InteractionGroup != null ? m_InteractionGroup.focusInteractable : null;
        if (currentFocusedObject != null)
        {
            Destroy(currentFocusedObject.transform.gameObject);
            ARObjectDimensions.DeselectAll();
            m_SelectedFurnitureName = null;
            UpdateSelectedNameBadge();
        }
        else if (ARObjectDimensions.currentSelected != null)
        {
            Destroy(ARObjectDimensions.currentSelected.gameObject);
            ARObjectDimensions.DeselectAll();
            m_SelectedFurnitureName = null;
            UpdateSelectedNameBadge();
        }
    }

    void InitializeDebugMenuOffsets()
    {
        if (m_CreateButton.TryGetComponent<RectTransform>(out var buttonRect))
            m_ObjectButtonOffset = new Vector2(0f, buttonRect.anchoredPosition.y + buttonRect.rect.height + 10f);
        else
            m_ObjectButtonOffset = new Vector2(0f, 200f);

        if (m_ObjectMenu.TryGetComponent<RectTransform>(out var menuRect))
            m_ObjectMenuOffset = new Vector2(0f, menuRect.anchoredPosition.y + menuRect.rect.height + 10f);
        else
            m_ObjectMenuOffset = new Vector2(0f, 345f);
    }

    void AdjustARDebugMenuPosition()
    {
        if (m_DebugMenu == null) return;

        float screenWidthInInches = Screen.width / Screen.dpi;

        if (screenWidthInInches < 5)
        {
            Vector2 menuOffset = m_ShowObjectMenu ? m_ObjectMenuOffset : m_ObjectButtonOffset;

            if (m_DebugMenu.toolbar.TryGetComponent<RectTransform>(out var rect))
            {
                rect.anchorMin = new Vector2(0.5f, 0);
                rect.anchorMax = new Vector2(0.5f, 0);
                rect.eulerAngles = new Vector3(rect.eulerAngles.x, rect.eulerAngles.y, 90);
                rect.anchoredPosition = new Vector2(0, 20) + menuOffset;
            }

            if (m_DebugMenu.displayInfoMenuButton.TryGetComponent<RectTransform>(out var infoMenuButtonRect))
                infoMenuButtonRect.localEulerAngles = new Vector3(infoMenuButtonRect.localEulerAngles.x, infoMenuButtonRect.localEulerAngles.y, -90);

            if (m_DebugMenu.displayConfigurationsMenuButton.TryGetComponent<RectTransform>(out var configurationsMenuButtonRect))
                configurationsMenuButtonRect.localEulerAngles = new Vector3(configurationsMenuButtonRect.localEulerAngles.x, configurationsMenuButtonRect.localEulerAngles.y, -90);

            if (m_DebugMenu.displayCameraConfigurationsMenuButton.TryGetComponent<RectTransform>(out var cameraConfigurationsMenuButtonRect))
                cameraConfigurationsMenuButtonRect.localEulerAngles = new Vector3(cameraConfigurationsMenuButtonRect.localEulerAngles.x, cameraConfigurationsMenuButtonRect.localEulerAngles.y, -90);

            if (m_DebugMenu.displayDebugOptionsMenuButton.TryGetComponent<RectTransform>(out var debugOptionsMenuButtonRect))
                debugOptionsMenuButtonRect.localEulerAngles = new Vector3(debugOptionsMenuButtonRect.localEulerAngles.x, debugOptionsMenuButtonRect.localEulerAngles.y, -90);

            if (m_DebugMenu.infoMenu.TryGetComponent<RectTransform>(out var infoMenuRect))
            {
                infoMenuRect.anchorMin = new Vector2(0.5f, 0);
                infoMenuRect.anchorMax = new Vector2(0.5f, 0);
                infoMenuRect.pivot = new Vector2(0.5f, 0);
                infoMenuRect.anchoredPosition = new Vector2(0, 150) + menuOffset;
            }

            if (m_DebugMenu.configurationMenu.TryGetComponent<RectTransform>(out var configurationsMenuRect))
            {
                configurationsMenuRect.anchorMin = new Vector2(0.5f, 0);
                configurationsMenuRect.anchorMax = new Vector2(0.5f, 0);
                configurationsMenuRect.pivot = new Vector2(0.5f, 0);
                configurationsMenuRect.anchoredPosition = new Vector2(0, 150) + menuOffset;
            }

            if (m_DebugMenu.cameraConfigurationMenu.TryGetComponent<RectTransform>(out var cameraConfigurationsMenuRect))
            {
                cameraConfigurationsMenuRect.anchorMin = new Vector2(0.5f, 0);
                cameraConfigurationsMenuRect.anchorMax = new Vector2(0.5f, 0);
                cameraConfigurationsMenuRect.pivot = new Vector2(0.5f, 0);
                cameraConfigurationsMenuRect.anchoredPosition = new Vector2(0, 150) + menuOffset;
            }

            if (m_DebugMenu.debugOptionsMenu.TryGetComponent<RectTransform>(out var debugOptionsMenuRect))
            {
                debugOptionsMenuRect.anchorMin = new Vector2(0.5f, 0);
                debugOptionsMenuRect.anchorMax = new Vector2(0.5f, 0);
                debugOptionsMenuRect.pivot = new Vector2(0.5f, 0);
                debugOptionsMenuRect.anchoredPosition = new Vector2(0, 150) + menuOffset;
            }
        }
    }

    void OnPlaneChanged(ARPlanesChangedEventArgs eventArgs)
    {
        if (eventArgs.added.Count > 0)
        {
            foreach (var plane in eventArgs.added)
            {
                if (plane.TryGetComponent<ARFeatheredPlaneMeshVisualizerCompanion>(out var visualizer))
                {
                    featheredPlaneMeshVisualizerCompanions.Add(visualizer);
                    visualizer.visualizeSurfaces = (m_DebugPlaneSlider.value != 0);
                }
            }
        }

        if (eventArgs.removed.Count > 0)
        {
            foreach (var plane in eventArgs.removed)
            {
                if (plane.TryGetComponent<ARFeatheredPlaneMeshVisualizerCompanion>(out var visualizer))
                    featheredPlaneMeshVisualizerCompanions.Remove(visualizer);
            }
        }

        // Fallback if the counts do not match after an update
        if (m_PlaneManager.trackables.count != featheredPlaneMeshVisualizerCompanions.Count)
        {
            featheredPlaneMeshVisualizerCompanions.Clear();
            foreach (var trackable in m_PlaneManager.trackables)
            {
                if (trackable.TryGetComponent<ARFeatheredPlaneMeshVisualizerCompanion>(out var visualizer))
                {
                    featheredPlaneMeshVisualizerCompanions.Add(visualizer);
                    visualizer.visualizeSurfaces = (m_DebugPlaneSlider.value != 0);
                }
            }
        }
    }
}
