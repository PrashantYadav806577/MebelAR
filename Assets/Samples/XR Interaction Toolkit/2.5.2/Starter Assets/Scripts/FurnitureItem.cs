using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureItem : MonoBehaviour
{
    public bool canSpawnOnFloor;
    public bool canSpawnOnWall;
    public List<string> rooms = new List<string>();

    [SerializeField]
    [Tooltip("Human-readable furniture name. If left blank, falls back to the cleaned GameObject name.")]
    string m_FurnitureName;

    /// <summary>
    /// Explicit human-readable furniture name.
    /// </summary>
    public string furnitureName
    {
        get => m_FurnitureName;
        set => m_FurnitureName = value;
    }

    [SerializeField]
    [Tooltip("Minimum allowable uniform scale for this furniture piece.")]
    float m_MinScale = 0.4f;

    /// <summary>
    /// Minimum allowable uniform scale for this furniture piece.
    /// </summary>
    public float minScale
    {
        get => m_MinScale;
        set => m_MinScale = value;
    }

    [SerializeField]
    [Tooltip("Maximum allowable uniform scale for this furniture piece.")]
    float m_MaxScale = 2.0f;

    /// <summary>
    /// Maximum allowable uniform scale for this furniture piece.
    /// </summary>
    public float maxScale
    {
        get => m_MaxScale;
        set => m_MaxScale = value;
    }

    /// <summary>
    /// Returns the display name for this furniture piece.
    /// </summary>
    public string DisplayName
    {
        get
        {
            // 1. If explicit furniture name was set on this component, use it
            if (!string.IsNullOrWhiteSpace(m_FurnitureName) && !IsGenericDummyName(m_FurnitureName))
                return m_FurnitureName.Trim();

            // 2. Check if this GameObject itself has a specific name (not generic "Furniture Item")
            string selfName = CleanName(gameObject.name);
            if (!IsGenericDummyName(selfName))
                return selfName;

            // 3. Check parent GameObject name
            if (transform.parent != null)
            {
                string parentName = CleanName(transform.parent.name);
                if (!IsGenericDummyName(parentName))
                    return parentName;
            }

            // 4. Check root GameObject name (e.g. "Wooden Chair(Clone)" -> "Wooden Chair")
            if (transform.root != null)
            {
                string rootName = CleanName(transform.root.name);
                if (!IsGenericDummyName(rootName))
                    return rootName;
            }

            return "Furniture";
        }
    }

    /// <summary>
    /// Checks whether a name is a generic template placeholder like "Furniture Item" or "Base Interactable Furniture".
    /// </summary>
    public static bool IsGenericDummyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        string lower = name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return lower == "furnitureitem" ||
               lower == "furniture" ||
               lower == "baseinteractablefurniture" ||
               lower == "basefloorfurniture" ||
               lower == "basewallfurniture";
    }

    /// <summary>
    /// Cleans Unity \"(Clone)\", trailing numbers, or generic prefixes from the object name and formats it cleanly.
    /// </summary>
    public static string CleanName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "Furniture";
        string clean = rawName.Replace("(Clone)", "").Trim();
        int parenIdx = clean.IndexOf('(');
        if (parenIdx > 0)
            clean = clean.Substring(0, parenIdx).Trim();

        // Convert snake_case or kebab-case
        clean = clean.Replace('_', ' ').Replace('-', ' ').Trim();

        // If PascalCase or camelCase without spaces (e.g. "WoodenChair"), insert spaces
        if (!clean.Contains(" "))
        {
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"(\B[A-Z])", " $1").Trim();
        }

        while (clean.Contains("  "))
            clean = clean.Replace("  ", " ");

        return clean;
    }

    /// <summary>
    /// Returns sensible minimum scale for a given prefab name to prevent miniaturization.
    /// </summary>
    public static float GetDefaultMinScaleForName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0.4f;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("bed") || lower.Contains("sofa") || lower.Contains("table") || lower.Contains("bath") || lower.Contains("threadmill"))
            return 0.55f;
        if (lower.Contains("chair") || lower.Contains("toilet") || lower.Contains("shelf"))
            return 0.45f;
        if (lower.Contains("globe") || lower.Contains("fan") || lower.Contains("pot") || lower.Contains("telescope"))
            return 0.35f;
        return 0.4f;
    }
}
