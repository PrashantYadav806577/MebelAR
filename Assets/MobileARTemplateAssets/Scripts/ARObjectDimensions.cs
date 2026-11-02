using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Calculates and displays live physical dimensions (Width and Length) for placed furniture objects,
/// manages per-object lock/unlock state, enforces per-prefab minimum scale limits, and prevents
/// placement/movement overlaps with other furniture objects.
/// </summary>
public class ARObjectDimensions : MonoBehaviour
{
    public static ARObjectDimensions currentSelected { get; private set; }

    /// <summary>
    /// Static registry of all placed, active furniture objects in the scene.
    /// </summary>
    public static readonly List<ARObjectDimensions> allActiveFurniture = new List<ARObjectDimensions>();

    /// <summary>
    /// Event fired whenever any furniture object is locked or unlocked.
    /// </summary>
    public static event System.Action<ARObjectDimensions, bool> onLockStateChanged;

    /// <summary>
    /// Event fired whenever the currently selected furniture object changes.
    /// </summary>
    public static event System.Action<ARObjectDimensions> onSelectionChanged;

    [Header("Label Configuration")]
    [SerializeField]
    [Tooltip("Height offset in meters above the top-most surface of the object.")]
    float m_VerticalOffset = 0.08f;

    [SerializeField]
    [Tooltip("World scale of the floating label canvas.")]
    float m_CanvasScale = 0.0018f;

    // UI references (instantiated at runtime in world space)
    GameObject m_LabelCanvasGO;
    RectTransform m_LabelCanvasRect;
    TextMeshProUGUI m_DimensionText;

    // Geometry caching
    MeshFilter[] m_MeshFilters;
    SkinnedMeshRenderer[] m_SkinnedMeshes;
    Collider[] m_Colliders;

    // Tracking
    Transform m_RootTransform;
    Camera m_MainCamera;
    XRGrabInteractable m_Interactable;
    ARTransformer m_ARTransformer;
    FurnitureItem m_FurnitureItem;

    Transform rootTransform => m_RootTransform != null ? m_RootTransform : (m_RootTransform = transform);

    Vector3 m_LastScale = Vector3.zero;
    Vector3 m_LastPos = Vector3.zero;
    Quaternion m_LastRot = Quaternion.identity;

    // Lock state
    bool m_IsLocked = false;
    Vector3 m_LockedPosition;
    Quaternion m_LockedRotation;
    Vector3 m_LockedScale;

    // Scale limits
    float m_CurrentMinScale = 0.4f;
    float m_CurrentMaxScale = 2.0f;

    // Overlap prevention
    Vector3 m_LastValidPosition;
    bool m_HasValidPosition = false;

    // Live dimensions cache
    float m_CurrentWidth = 0f;
    float m_CurrentLength = 0f;

    /// <summary>
    /// Whether this furniture object is currently locked in place.
    /// When locked, position, rotation, and scale cannot be modified.
    /// </summary>
    public bool isLocked => m_IsLocked;

    /// <summary>
    /// Clean display name for this furniture piece.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (m_FurnitureItem != null)
            {
                string fn = m_FurnitureItem.DisplayName;
                if (!FurnitureItem.IsGenericDummyName(fn))
                    return fn;
            }

            var item = GetComponentInChildren<FurnitureItem>() ?? GetComponentInParent<FurnitureItem>();
            if (item != null)
            {
                string fn = item.DisplayName;
                if (!FurnitureItem.IsGenericDummyName(fn))
                    return fn;
            }

            return FurnitureItem.CleanName(gameObject.name);
        }
    }

    /// <summary>
    /// Current physical width in meters.
    /// </summary>
    public float CurrentWidth => m_CurrentWidth;

    /// <summary>
    /// Current physical length in meters.
    /// </summary>
    public float CurrentLength => m_CurrentLength;

    /// <summary>
    /// Formatted width string (e.g. "85 cm (0.85 m)").
    /// </summary>
    public string FormattedWidth => FormatMeasurement(m_CurrentWidth);

    /// <summary>
    /// Formatted length string (e.g. "1.20 m (120 cm)").
    /// </summary>
    public string FormattedLength => FormatMeasurement(m_CurrentLength);

    // ───────────────────────────── Unity Lifecycle ─────────────────────────────

    void Awake()
    {
        m_RootTransform = transform;
        m_MainCamera = Camera.main;

        // Cache child renderers and colliders
        CacheGeometryComponents();

        // Find FurnitureItem and ARTransformer
        m_FurnitureItem = GetComponentInChildren<FurnitureItem>() ?? GetComponentInParent<FurnitureItem>();
        m_ARTransformer = GetComponentInChildren<ARTransformer>() ?? GetComponentInParent<ARTransformer>();

        // Find XRGrabInteractable for focus/selection events
        m_Interactable = GetComponentInChildren<XRGrabInteractable>() ?? GetComponentInParent<XRGrabInteractable>();
        if (m_Interactable != null)
        {
            m_Interactable.firstFocusEntered.AddListener(OnFirstFocusEntered);
            m_Interactable.lastFocusExited.AddListener(OnLastFocusExited);
            m_Interactable.firstSelectEntered.AddListener(OnFirstSelectEntered);
            m_Interactable.lastSelectExited.AddListener(OnLastSelectExited);
        }

        // Apply scale limits
        ApplyScaleLimits();

        // Build the floating world-space UI label
        CreateWorldSpaceLabel();
    }

    void OnEnable()
    {
        if (!allActiveFurniture.Contains(this))
            allActiveFurniture.Add(this);

        m_LastValidPosition = rootTransform.position;
        m_HasValidPosition = true;
    }

    void OnDisable()
    {
        allActiveFurniture.Remove(this);
    }

    void Start()
    {
        if (m_MainCamera == null)
            m_MainCamera = Camera.main;

        // Ensure scale limits are configured
        ApplyScaleLimits();

        // Calculate initial dimensions immediately
        UpdateDimensions(force: true);

        m_LastValidPosition = rootTransform.position;
        m_HasValidPosition = true;
    }

    void OnDestroy()
    {
        allActiveFurniture.Remove(this);

        if (m_Interactable != null)
        {
            m_Interactable.firstFocusEntered.RemoveListener(OnFirstFocusEntered);
            m_Interactable.lastFocusExited.RemoveListener(OnLastFocusExited);
            m_Interactable.firstSelectEntered.RemoveListener(OnFirstSelectEntered);
            m_Interactable.lastSelectExited.RemoveListener(OnLastSelectExited);
        }

        if (currentSelected == this)
        {
            currentSelected = null;
            onSelectionChanged?.Invoke(null);
        }

        if (m_LabelCanvasGO != null)
            Destroy(m_LabelCanvasGO);
    }

    void LateUpdate()
    {
        if (m_MainCamera == null)
        {
            m_MainCamera = Camera.main;
            if (m_MainCamera == null) return;
        }

        // Enforce lock state
        if (m_IsLocked)
        {
            rootTransform.position = m_LockedPosition;
            rootTransform.rotation = m_LockedRotation;
            rootTransform.localScale = m_LockedScale;
        }
        else
        {
            // Clamp scale so it cannot drop below per-prefab minimum
            if (rootTransform.localScale.x < m_CurrentMinScale ||
                rootTransform.localScale.y < m_CurrentMinScale ||
                rootTransform.localScale.z < m_CurrentMinScale)
            {
                float clamped = Mathf.Max(rootTransform.localScale.x, m_CurrentMinScale);
                rootTransform.localScale = new Vector3(clamped, clamped, clamped);
            }

            // Check overlap while moving
            bool isBeingManipulated = m_Interactable != null && (m_Interactable.isSelected || m_Interactable.isFocused);
            if (isBeingManipulated)
            {
                if (CheckFurnitureOverlap(rootTransform.position))
                {
                    // Revert to last valid non-overlapping position
                    if (m_HasValidPosition)
                        rootTransform.position = m_LastValidPosition;
                }
                else
                {
                    m_LastValidPosition = rootTransform.position;
                    m_HasValidPosition = true;
                }
            }
            else
            {
                m_LastValidPosition = rootTransform.position;
                m_HasValidPosition = true;
            }
        }

        // Determine if this object should currently show its dimensions
        bool shouldShow = (currentSelected == this) ||
                          (m_Interactable != null && (m_Interactable.isFocused || m_Interactable.isSelected));

        if (!shouldShow)
        {
            if (m_LabelCanvasGO != null && m_LabelCanvasGO.activeSelf)
                m_LabelCanvasGO.SetActive(false);
            return;
        }

        // Object is selected/focused: ensure label is active
        if (m_LabelCanvasGO != null && !m_LabelCanvasGO.activeSelf)
            m_LabelCanvasGO.SetActive(true);

        // Check if transform changed to update dimensions and position
        bool transformChanged = (rootTransform.hasChanged ||
                                 rootTransform.position != m_LastPos ||
                                 rootTransform.rotation != m_LastRot ||
                                 rootTransform.localScale != m_LastScale);

        if (transformChanged)
        {
            UpdateDimensions(force: false);
            rootTransform.hasChanged = false;
            m_LastPos = rootTransform.position;
            m_LastRot = rootTransform.rotation;
            m_LastScale = rootTransform.localScale;
        }

        // Billboard effect: rotate label canvas to directly face the camera
        if (m_LabelCanvasGO != null && m_MainCamera != null)
        {
            m_LabelCanvasGO.transform.rotation = m_MainCamera.transform.rotation;
        }
    }

    // ───────────────────────────── Lock / Unlock Management ─────────────────────────────

    /// <summary>
    /// Locks or unlocks this furniture object.
    /// </summary>
    public void SetLocked(bool locked)
    {
        m_IsLocked = locked;

        if (m_IsLocked)
        {
            m_LockedPosition = rootTransform.position;
            m_LockedRotation = rootTransform.rotation;
            m_LockedScale = rootTransform.localScale;
        }

        if (m_Interactable != null)
        {
            m_Interactable.trackPosition = !m_IsLocked;
            m_Interactable.trackRotation = !m_IsLocked;
            m_Interactable.trackScale = !m_IsLocked;
        }

        UpdateDimensions(force: true);
        onLockStateChanged?.Invoke(this, m_IsLocked);
    }

    /// <summary>
    /// Toggles the lock state.
    /// </summary>
    public void ToggleLock()
    {
        SetLocked(!m_IsLocked);
    }

    // ───────────────────────────── Minimum Scale Limits ─────────────────────────────

    /// <summary>
    /// Applies per-prefab scale limits based on FurnitureItem or object type.
    /// </summary>
    public void ApplyScaleLimits()
    {
        if (m_FurnitureItem == null)
            m_FurnitureItem = GetComponentInChildren<FurnitureItem>() ?? GetComponentInParent<FurnitureItem>();

        if (m_FurnitureItem != null && m_FurnitureItem.minScale > 0.05f)
        {
            m_CurrentMinScale = m_FurnitureItem.minScale;
            m_CurrentMaxScale = m_FurnitureItem.maxScale;
        }
        else
        {
            m_CurrentMinScale = FurnitureItem.GetDefaultMinScaleForName(gameObject.name);
            m_CurrentMaxScale = 2.0f;
        }

        if (m_ARTransformer == null)
            m_ARTransformer = GetComponentInChildren<ARTransformer>() ?? GetComponentInParent<ARTransformer>();

        if (m_ARTransformer != null)
        {
            m_ARTransformer.minScale = m_CurrentMinScale;
            m_ARTransformer.maxScale = m_CurrentMaxScale;
        }
    }

    // ───────────────────────────── Overlap Prevention ─────────────────────────────

    /// <summary>
    /// Calculates the horizontal footprint radius in meters.
    /// </summary>
    public float GetFootprintRadius()
    {
        if (CalculateLocalBounds(out Vector3 localSize, out _))
        {
            float maxDim = Mathf.Max(localSize.x, localSize.z);
            return Mathf.Max(0.2f, maxDim * 0.45f);
        }
        return 0.35f;
    }

    /// <summary>
    /// Checks whether the given world position overlaps with any other active furniture.
    /// </summary>
    bool CheckFurnitureOverlap(Vector3 testPos)
    {
        float myRadius = GetFootprintRadius();

        for (int i = 0; i < allActiveFurniture.Count; i++)
        {
            var other = allActiveFurniture[i];
            if (other == null || other == this) continue;

            Vector3 otherPos = other.rootTransform.position;
            float dx = testPos.x - otherPos.x;
            float dz = testPos.z - otherPos.z;
            float distSq = dx * dx + dz * dz;

            float combinedRadius = (myRadius + other.GetFootprintRadius()) * 0.8f;
            if (distSq < combinedRadius * combinedRadius)
            {
                if (Mathf.Abs(testPos.y - otherPos.y) < 1.0f)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Static validator passed to ObjectSpawner.placementValidator before spawning new objects.
    /// </summary>
    public static bool ValidateSpawnCandidate(GameObject prefab, Vector3 spawnPoint)
    {
        float candidateRadius = 0.35f;
        if (prefab != null)
        {
            string clean = FurnitureItem.CleanName(prefab.name);
            float minS = FurnitureItem.GetDefaultMinScaleForName(clean);
            candidateRadius = Mathf.Max(0.25f, minS * 0.7f);
        }

        for (int i = 0; i < allActiveFurniture.Count; i++)
        {
            var other = allActiveFurniture[i];
            if (other == null) continue;

            Vector3 otherPos = other.rootTransform.position;
            float dx = spawnPoint.x - otherPos.x;
            float dz = spawnPoint.z - otherPos.z;
            float distSq = dx * dx + dz * dz;

            float combinedRadius = (candidateRadius + other.GetFootprintRadius()) * 0.82f;
            if (distSq < combinedRadius * combinedRadius)
            {
                if (Mathf.Abs(spawnPoint.y - otherPos.y) < 1.0f)
                {
                    return false; // Block spawn due to overlap
                }
            }
        }
        return true;
    }

    // ───────────────────────────── Geometry & Dimension Calculation ─────────────────────────────

    void CacheGeometryComponents()
    {
        m_MeshFilters = GetComponentsInChildren<MeshFilter>(true);
        m_SkinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        m_Colliders = GetComponentsInChildren<Collider>(true);
    }

    /// <summary>
    /// Calculates the object's physical dimensions along its local horizontal axes (Local X = Width, Local Z = Length).
    /// Projects all child mesh corners onto the root transform's local axes to guarantee rotational invariance.
    /// </summary>
    public bool CalculateLocalBounds(out Vector3 localSize, out Vector3 worldTopCenter)
    {
        bool hasGeometry = false;
        Vector3 minProj = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 maxProj = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        Vector3 rootPos = rootTransform.position;
        Vector3 rightAxis = rootTransform.right;
        Vector3 upAxis = rootTransform.up;
        Vector3 forwardAxis = rootTransform.forward;

        // Ensure geometry is cached
        if (m_MeshFilters == null || m_SkinnedMeshes == null || m_Colliders == null)
            CacheGeometryComponents();

        // 1. Process MeshFilters
        if (m_MeshFilters != null && m_MeshFilters.Length > 0)
        {
            foreach (var mf in m_MeshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && !mr.enabled) continue;

                Transform childT = mf.transform;
                Bounds b = mf.sharedMesh.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                Vector3[] corners = new Vector3[8]
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3( e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y,  e.z),
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 worldPt = childT.TransformPoint(corners[i]);
                    Vector3 offset = worldPt - rootPos;

                    float x = Vector3.Dot(offset, rightAxis);
                    float y = Vector3.Dot(offset, upAxis);
                    float z = Vector3.Dot(offset, forwardAxis);

                    minProj.x = Mathf.Min(minProj.x, x);
                    maxProj.x = Mathf.Max(maxProj.x, x);
                    minProj.y = Mathf.Min(minProj.y, y);
                    maxProj.y = Mathf.Max(maxProj.y, y);
                    minProj.z = Mathf.Min(minProj.z, z);
                    maxProj.z = Mathf.Max(maxProj.z, z);
                    hasGeometry = true;
                }
            }
        }

        // 2. Process SkinnedMeshRenderers
        if (m_SkinnedMeshes != null && m_SkinnedMeshes.Length > 0)
        {
            foreach (var smr in m_SkinnedMeshes)
            {
                if (smr == null || smr.sharedMesh == null || !smr.enabled) continue;

                Transform childT = smr.transform;
                Bounds b = smr.sharedMesh.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                Vector3[] corners = new Vector3[8]
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3( e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y,  e.z),
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 worldPt = childT.TransformPoint(corners[i]);
                    Vector3 offset = worldPt - rootPos;

                    float x = Vector3.Dot(offset, rightAxis);
                    float y = Vector3.Dot(offset, upAxis);
                    float z = Vector3.Dot(offset, forwardAxis);

                    minProj.x = Mathf.Min(minProj.x, x);
                    maxProj.x = Mathf.Max(maxProj.x, x);
                    minProj.y = Mathf.Min(minProj.y, y);
                    maxProj.y = Mathf.Max(maxProj.y, y);
                    minProj.z = Mathf.Min(minProj.z, z);
                    maxProj.z = Mathf.Max(maxProj.z, z);
                    hasGeometry = true;
                }
            }
        }

        // 3. Fallback to Colliders if meshes not found
        if (!hasGeometry && m_Colliders != null)
        {
            foreach (var col in m_Colliders)
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                Bounds b = col.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                Vector3[] corners = new Vector3[8]
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3( e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y,  e.z),
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = corners[i] - rootPos;
                    float x = Vector3.Dot(offset, rightAxis);
                    float y = Vector3.Dot(offset, upAxis);
                    float z = Vector3.Dot(offset, forwardAxis);

                    minProj.x = Mathf.Min(minProj.x, x);
                    maxProj.x = Mathf.Max(maxProj.x, x);
                    minProj.y = Mathf.Min(minProj.y, y);
                    maxProj.y = Mathf.Max(maxProj.y, y);
                    minProj.z = Mathf.Min(minProj.z, z);
                    maxProj.z = Mathf.Max(maxProj.z, z);
                    hasGeometry = true;
                }
            }
        }

        if (!hasGeometry)
        {
            localSize = rootTransform.lossyScale;
            worldTopCenter = rootPos + Vector3.up * 0.5f;
            return false;
        }

        localSize = new Vector3(
            Mathf.Max(0.01f, maxProj.x - minProj.x),
            Mathf.Max(0.01f, maxProj.y - minProj.y),
            Mathf.Max(0.01f, maxProj.z - minProj.z)
        );

        float midX = (minProj.x + maxProj.x) * 0.5f;
        float midZ = (minProj.z + maxProj.z) * 0.5f;
        float topY = maxProj.y;

        worldTopCenter = rootPos + rightAxis * midX + forwardAxis * midZ + upAxis * topY;
        return true;
    }

    /// <summary>
    /// Recalculates dimensions and updates the label text and world position.
    /// </summary>
    public void UpdateDimensions(bool force = false)
    {
        if (CalculateLocalBounds(out Vector3 localSize, out Vector3 worldTopCenter))
        {
            m_CurrentWidth = localSize.x;
            m_CurrentLength = localSize.z;

            string widthFormatted = FormatMeasurement(m_CurrentWidth);
            string lengthFormatted = FormatMeasurement(m_CurrentLength);
            string lockSuffix = m_IsLocked ? " <color=#FFB74D>[LOCKED]</color>" : "";

            if (m_DimensionText != null)
            {
                m_DimensionText.text = $"Width: {widthFormatted} | Length: {lengthFormatted}{lockSuffix}";
            }

            if (m_LabelCanvasGO != null)
            {
                m_LabelCanvasGO.transform.position = worldTopCenter + Vector3.up * m_VerticalOffset;
            }
        }
    }

    /// <summary>
    /// Formats a meter distance into readable meters and centimeters.
    /// e.g. 0.45m -> "45 cm (0.45 m)" or 1.25m -> "1.25 m (125 cm)".
    /// </summary>
    public static string FormatMeasurement(float meters)
    {
        float cm = meters * 100f;
        if (meters >= 1.0f)
            return $"{meters:F2} m ({cm:F0} cm)";
        else
            return $"{cm:F0} cm ({meters:F2} m)";
    }

    // ───────────────────────────── World Space Label Creation ─────────────────────────────

    void CreateWorldSpaceLabel()
    {
        if (m_LabelCanvasGO != null) return;

        m_LabelCanvasGO = new GameObject($"{gameObject.name}_DimensionLabel");
        m_LabelCanvasGO.transform.SetParent(null, false);

        Canvas canvas = m_LabelCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        m_LabelCanvasRect = m_LabelCanvasGO.GetComponent<RectTransform>();
        m_LabelCanvasRect.sizeDelta = new Vector2(420f, 58f);
        m_LabelCanvasRect.localScale = Vector3.one * m_CanvasScale;

        // Background pill
        Image bgImage = m_LabelCanvasGO.AddComponent<Image>();
        bgImage.color = new Color(0.09f, 0.14f, 0.23f, 0.90f); // #17243A deep navy tint
        bgImage.raycastTarget = false;

        // TextMeshProUGUI child
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(m_LabelCanvasGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        m_DimensionText = textGO.AddComponent<TextMeshProUGUI>();
        m_DimensionText.alignment = TextAlignmentOptions.Center;
        m_DimensionText.fontSize = 21;
        m_DimensionText.fontStyle = FontStyles.Bold;
        m_DimensionText.color = Color.white;
        m_DimensionText.raycastTarget = false;
        m_DimensionText.text = "Width: 0 cm | Length: 0 cm";

        // Initial state: hidden until selected/placed
        m_LabelCanvasGO.SetActive(false);
    }

    // ───────────────────────────── Selection Management ─────────────────────────────

    public static void SelectObject(ARObjectDimensions obj)
    {
        if (currentSelected != obj)
        {
            if (currentSelected != null && currentSelected.m_LabelCanvasGO != null)
                currentSelected.m_LabelCanvasGO.SetActive(false);

            currentSelected = obj;
            if (obj != null)
            {
                obj.UpdateDimensions(force: true);
                if (obj.m_LabelCanvasGO != null)
                    obj.m_LabelCanvasGO.SetActive(true);
            }
            onSelectionChanged?.Invoke(obj);
        }
    }

    public static void DeselectAll()
    {
        if (currentSelected != null)
        {
            if (currentSelected.m_LabelCanvasGO != null)
                currentSelected.m_LabelCanvasGO.SetActive(false);
            currentSelected = null;
            onSelectionChanged?.Invoke(null);
        }
    }

    public static void UpdateFocusedInteractable(IXRFocusInteractable focusedInteractable)
    {
        if (focusedInteractable != null)
        {
            var interactableComp = focusedInteractable as MonoBehaviour;
            if (interactableComp != null)
            {
                var dims = interactableComp.GetComponentInParent<ARObjectDimensions>() ??
                           interactableComp.GetComponentInChildren<ARObjectDimensions>();
                if (dims != null && dims != currentSelected)
                {
                    SelectObject(dims);
                }
            }
        }
    }

    void OnFirstFocusEntered(FocusEnterEventArgs args) => SelectObject(this);
    void OnLastFocusExited(FocusExitEventArgs args) { }

    void OnFirstSelectEntered(SelectEnterEventArgs args) => SelectObject(this);
    void OnLastSelectExited(SelectExitEventArgs args) { }
}
