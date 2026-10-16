using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Calculates and displays live physical dimensions (Width and Length) for placed furniture objects.
///
/// Features:
/// 1. Rotation-Invariant: Calculates dimensions along the object's local horizontal axes (Local X and Local Z),
///    so rotating the object never distorts or changes its Width/Length.
/// 2. Live Scaling / Zooming: As the user pinches to resize the furniture, dimensions update in real-time.
/// 3. Accurate Geometry Bounds: Evaluates all child MeshFilters and SkinnedMeshRenderers (with Collider fallback).
/// 4. Camera-Facing Billboard: A clean, sleek world-space label hovers above the top surface of the furniture,
///    facing the main camera.
/// 5. Selection Lifecycle: Automatically appears when placed or selected/focused, and hides when deselected or deleted.
/// 6. Multi-Object Support: Each object has its own linked dimensions; only the active/focused object displays its label.
/// </summary>
public class ARObjectDimensions : MonoBehaviour
{
    public static ARObjectDimensions currentSelected { get; private set; }

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

    Vector3 m_LastScale = Vector3.zero;
    Vector3 m_LastPos = Vector3.zero;
    Quaternion m_LastRot = Quaternion.identity;

    bool m_IsActiveObject = false;

    // ───────────────────────────── Unity Lifecycle ─────────────────────────────

    void Awake()
    {
        m_RootTransform = transform;
        m_MainCamera = Camera.main;

        // Cache child renderers and colliders
        CacheGeometryComponents();

        // Find XRGrabInteractable for focus/selection events
        m_Interactable = GetComponentInChildren<XRGrabInteractable>();
        if (m_Interactable != null)
        {
            m_Interactable.firstFocusEntered.AddListener(OnFirstFocusEntered);
            m_Interactable.lastFocusExited.AddListener(OnLastFocusExited);
            m_Interactable.firstSelectEntered.AddListener(OnFirstSelectEntered);
            m_Interactable.lastSelectExited.AddListener(OnLastSelectExited);
        }

        // Build the floating world-space UI label
        CreateWorldSpaceLabel();
    }

    void Start()
    {
        if (m_MainCamera == null)
            m_MainCamera = Camera.main;

        // Calculate initial dimensions immediately
        UpdateDimensions(force: true);
    }

    void OnDestroy()
    {
        if (m_Interactable != null)
        {
            m_Interactable.firstFocusEntered.RemoveListener(OnFirstFocusEntered);
            m_Interactable.lastFocusExited.RemoveListener(OnLastFocusExited);
            m_Interactable.firstSelectEntered.RemoveListener(OnFirstSelectEntered);
            m_Interactable.lastSelectExited.RemoveListener(OnLastSelectExited);
        }

        if (currentSelected == this)
            currentSelected = null;

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
        bool transformChanged = (m_RootTransform.hasChanged ||
                                 m_RootTransform.position != m_LastPos ||
                                 m_RootTransform.rotation != m_LastRot ||
                                 m_RootTransform.localScale != m_LastScale);

        if (transformChanged)
        {
            UpdateDimensions(force: false);
            m_RootTransform.hasChanged = false;
            m_LastPos = m_RootTransform.position;
            m_LastRot = m_RootTransform.rotation;
            m_LastScale = m_RootTransform.localScale;
        }

        // Billboard effect: rotate label canvas to directly face the camera
        if (m_LabelCanvasGO != null && m_MainCamera != null)
        {
            m_LabelCanvasGO.transform.rotation = m_MainCamera.transform.rotation;
        }
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

        Vector3 rootPos = m_RootTransform.position;
        Vector3 rightAxis = m_RootTransform.right;
        Vector3 upAxis = m_RootTransform.up;
        Vector3 forwardAxis = m_RootTransform.forward;

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

                // 8 corners of child's local mesh bounds
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
            localSize = m_RootTransform.lossyScale;
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
            float widthMeters = localSize.x;
            float lengthMeters = localSize.z;

            string widthFormatted = FormatMeasurement(widthMeters);
            string lengthFormatted = FormatMeasurement(lengthMeters);

            if (m_DimensionText != null)
            {
                m_DimensionText.text = $"Width: {widthFormatted} | Length: {lengthFormatted}";
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
        m_LabelCanvasGO.transform.SetParent(null, false); // Keep at world root to prevent parent scaling distortion

        Canvas canvas = m_LabelCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        m_LabelCanvasRect = m_LabelCanvasGO.GetComponent<RectTransform>();
        m_LabelCanvasRect.sizeDelta = new Vector2(380f, 56f);
        m_LabelCanvasRect.localScale = Vector3.one * m_CanvasScale;

        // Background pill
        Image bgImage = m_LabelCanvasGO.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        bgImage.raycastTarget = false; // Never block AR camera raycasts or interactions

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
        m_DimensionText.fontSize = 22;
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
        currentSelected = obj;
        if (obj != null)
        {
            obj.UpdateDimensions(force: true);
            if (obj.m_LabelCanvasGO != null)
                obj.m_LabelCanvasGO.SetActive(true);
        }
    }

    public static void DeselectAll()
    {
        if (currentSelected != null && currentSelected.m_LabelCanvasGO != null)
            currentSelected.m_LabelCanvasGO.SetActive(false);
        currentSelected = null;
    }

    public static void UpdateFocusedInteractable(IXRFocusInteractable focusedInteractable)
    {
        if (focusedInteractable == null)
        {
            DeselectAll();
            return;
        }

        var interactableComp = focusedInteractable as MonoBehaviour;
        if (interactableComp != null)
        {
            var dims = interactableComp.GetComponentInParent<ARObjectDimensions>() ??
                       interactableComp.GetComponentInChildren<ARObjectDimensions>();
            if (dims != null)
            {
                SelectObject(dims);
            }
        }
    }

    void OnFirstFocusEntered(FocusEnterEventArgs args) => SelectObject(this);
    void OnLastFocusExited(FocusExitEventArgs args)
    {
        if (currentSelected == this && (m_Interactable == null || !m_Interactable.isSelected))
            DeselectAll();
    }

    void OnFirstSelectEntered(SelectEnterEventArgs args) => SelectObject(this);
    void OnLastSelectExited(SelectExitEventArgs args)
    {
        if (currentSelected == this && (m_Interactable == null || !m_Interactable.isFocused))
            DeselectAll();
    }
}
