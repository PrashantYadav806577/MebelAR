using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public static class FeatureValidationTest
{
    [MenuItem("MebelAR/Run Feature Validation")]
    public static void RunValidation()
    {
        Debug.Log("================ STARTING FEATURE VALIDATION ================");
        
        // 1. Load SampleScene
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("TEST FAILED: SampleScene.unity could not be opened.");
            return;
        }
        Debug.Log("TEST PASSED: SampleScene.unity loaded successfully.");

        // 2. Find Canvas and ARTemplateMenuManager
        var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        Assert.IsNotNull(canvas, "Canvas not found in scene!");
        Debug.Log($"TEST PASSED: Found Canvas: {canvas.name}");

        var menuManager = UnityEngine.Object.FindObjectOfType<ARTemplateMenuManager>(true);
        Assert.IsNotNull(menuManager, "ARTemplateMenuManager not found in scene!");
        Debug.Log($"TEST PASSED: Found ARTemplateMenuManager on {menuManager.gameObject.name}");

        // Enable menu manager for testing if disabled
        menuManager.enabled = true;

        // 3. Test EnsureFeaturesUI()
        MethodInfo ensureMethod = typeof(ARTemplateMenuManager).GetMethod("EnsureFeaturesUI", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(ensureMethod, "EnsureFeaturesUI method not found!");
        ensureMethod.Invoke(menuManager, null);

        // Verify Badge and Lock Button in hierarchy and check rectangular shape
        var badgeTrans = canvas.transform.Find("SelectedFurnitureBadge");
        Assert.IsNotNull(badgeTrans, "SelectedFurnitureBadge not found as child of Canvas!");
        var badgeLabel = badgeTrans.GetComponentInChildren<TextMeshProUGUI>();
        Assert.IsNotNull(badgeLabel, "TextMeshProUGUI label not found on SelectedFurnitureBadge!");
        var badgeImg = badgeTrans.GetComponent<Image>();
        Assert.IsNotNull(badgeImg, "Image component missing on SelectedFurnitureBadge!");
        Assert.IsNull(badgeImg.sprite, "Badge should have null sprite for clean rectangular shape (not oval/pill)!");
        var badgeRect = badgeTrans.GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(0f, 1f), badgeRect.anchorMin, "Badge anchorMin should be top-left (0, 1)");
        Assert.AreEqual(new Vector2(0f, 1f), badgeRect.anchorMax, "Badge anchorMax should be top-left (0, 1)");
        Debug.Log($"TEST 1 PASSED: SelectedFurnitureBadge verified as clean rectangle at Top-Left: anchors=({badgeRect.anchorMin.x},{badgeRect.anchorMin.y}), pos={badgeRect.anchoredPosition}, size={badgeRect.sizeDelta}");

        var lockTrans = canvas.transform.Find("Lock Button");
        Assert.IsNotNull(lockTrans, "Lock Button not found as child of Canvas!");
        var lockBtn = lockTrans.GetComponent<Button>();
        Assert.IsNotNull(lockBtn, "Button component missing on Lock Button!");
        var lockImg = lockTrans.GetComponent<Image>();
        Assert.IsNotNull(lockImg, "Image component missing on Lock Button!");
        Assert.IsNull(lockImg.sprite, "Lock Button should have null sprite for clean rectangular shape (not oval/pill)!");
        var lockLabel = lockTrans.GetComponentInChildren<TextMeshProUGUI>();
        Assert.IsNotNull(lockLabel, "TextMeshProUGUI missing on Lock Button!");
        var lockRect = lockTrans.GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(0f, 0f), lockRect.anchorMin, "Lock Button anchorMin should be bottom-left (0, 0)");
        Assert.AreEqual(new Vector2(0f, 0f), lockRect.anchorMax, "Lock Button anchorMax should be bottom-left (0, 0)");

        // Verify checkbox box and visual check/uncheck indicator
        var checkboxBox = lockTrans.Find("CheckboxBox");
        Assert.IsNotNull(checkboxBox, "CheckboxBox child missing on Lock Button!");
        var checkIndicator = checkboxBox.Find("CheckIndicator");
        Assert.IsNotNull(checkIndicator, "CheckIndicator child missing inside CheckboxBox!");
        Debug.Log($"TEST 2 PASSED: Lock Button verified as clean rectangle with check indicator at Bottom-Left: anchors=({lockRect.anchorMin.x},{lockRect.anchorMin.y}), pos={lockRect.anchoredPosition}, size={lockRect.sizeDelta}");

        // 4. Test Catalog Item Selection on Multiple Different Items
        var spawnerField = typeof(ARTemplateMenuManager).GetField("m_ObjectSpawner", BindingFlags.Instance | BindingFlags.NonPublic);
        var spawner = spawnerField.GetValue(menuManager) as ObjectSpawner;
        Assert.IsNotNull(spawner, "m_ObjectSpawner not assigned on ARTemplateMenuManager!");
        Assert.IsTrue(spawner.objectPrefabs.Count >= 2, "Need at least 2 prefabs in ObjectSpawner!");

        // 4a. Select first catalog item (e.g. Wooden Chair)
        menuManager.SetObjectToSpawn(0);
        string name0 = badgeLabel.text;
        Assert.AreNotEqual("Furniture Item", name0, "Badge should never display generic 'Furniture Item'!");
        Assert.AreNotEqual("Furniture", name0, "Badge should never display generic 'Furniture'!");
        Assert.IsTrue(badgeTrans.gameObject.activeSelf, "Badge should be visible when catalog item is selected");
        Debug.Log($"TEST 3a PASSED: Catalog item 0 selected -> Actual name: '{name0}' (never 'Furniture Item')");

        // 4b. Select second catalog item (e.g. Wooden Table)
        menuManager.SetObjectToSpawn(1);
        string name1 = badgeLabel.text;
        Assert.AreNotEqual("Furniture Item", name1, "Badge should never display generic 'Furniture Item'!");
        Assert.AreNotEqual(name0, name1, "Selecting different catalog item must update the name!");
        Debug.Log($"TEST 3b PASSED: Catalog item 1 selected -> Actual name: '{name1}' (differs from '{name0}')");

        // 5. Test Placed Object Selection With Nested Prefab Structure (real prefab hierarchy test)
        // Root object: "Wooden Chair(Clone)", Child: "Furniture Item" (with unassigned m_FurnitureName)
        var testObj1 = new GameObject("Wooden Chair(Clone)");
        var childItem1 = new GameObject("Furniture Item");
        childItem1.transform.SetParent(testObj1.transform);
        var fItem1 = childItem1.AddComponent<FurnitureItem>();
        var dims1 = testObj1.AddComponent<ARObjectDimensions>();
        var grab1 = testObj1.AddComponent<XRGrabInteractable>();

        // Verify DisplayName resolves to "Wooden Chair" and NOT "Furniture Item"
        Assert.AreEqual("Wooden Chair", dims1.DisplayName, "Placed object DisplayName must resolve to root name 'Wooden Chair', NOT child 'Furniture Item'!");

        // Select placed object
        ARObjectDimensions.SelectObject(dims1);
        MethodInfo updateBadge = typeof(ARTemplateMenuManager).GetMethod("UpdateSelectedNameBadge", BindingFlags.Instance | BindingFlags.NonPublic);
        updateBadge.Invoke(menuManager, null);

        Assert.AreEqual("Wooden Chair", badgeLabel.text, "Badge should show selected object's actual name 'Wooden Chair'");
        Assert.IsTrue(badgeTrans.gameObject.activeSelf, "Badge should be visible for placed object");
        Debug.Log($"TEST 4 PASSED: Placed nested object selected -> Name badge shows: '{badgeLabel.text}'");

        // 6. Test Lock/Unlock State, Check Indicator, and Manipulation Blocking
        MethodInfo onLockClick = typeof(ARTemplateMenuManager).GetMethod("OnLockButtonClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo updateLockVisual = typeof(ARTemplateMenuManager).GetMethod("UpdateLockButtonVisual", BindingFlags.Instance | BindingFlags.NonPublic);
        updateLockVisual.Invoke(menuManager, null);

        // Initially UNLOCKED:
        Assert.IsFalse(dims1.isLocked, "Object should be unlocked initially");
        Assert.AreEqual("Lock", lockLabel.text, "Lock button label should say 'Lock' when unlocked");
        Assert.IsFalse(checkIndicator.gameObject.activeSelf, "Check indicator must be UNCHECKED (inactive) when unlocked");
        Assert.IsTrue(grab1.trackPosition, "Position tracking must be enabled when unlocked");
        Assert.IsTrue(grab1.trackRotation, "Rotation tracking must be enabled when unlocked");
        Assert.IsTrue(grab1.trackScale, "Scale tracking must be enabled when unlocked");
        Debug.Log("TEST 5a PASSED: Unlocked state -> Check indicator is UNCHECKED, button says 'Lock', manipulations enabled.");

        // Click LOCK:
        onLockClick.Invoke(menuManager, null);
        Assert.IsTrue(dims1.isLocked, "Object should be locked after clicking Lock");
        Assert.AreEqual("Unlock", lockLabel.text, "Lock button label should say 'Unlock' when locked");
        Assert.IsTrue(checkIndicator.gameObject.activeSelf, "Check indicator must be CHECKED (active) when locked");
        Assert.IsFalse(grab1.trackPosition, "Position tracking must be DISABLED when locked");
        Assert.IsFalse(grab1.trackRotation, "Rotation tracking must be DISABLED when locked");
        Assert.IsFalse(grab1.trackScale, "Scale tracking must be DISABLED when locked");
        Debug.Log("TEST 5b PASSED: Click Lock -> Check indicator is CHECKED, button says 'Unlock', manipulations blocked.");

        // Click UNLOCK:
        onLockClick.Invoke(menuManager, null);
        Assert.IsFalse(dims1.isLocked, "Object should be unlocked after clicking Unlock");
        Assert.AreEqual("Lock", lockLabel.text, "Lock button label should say 'Lock' when unlocked");
        Assert.IsFalse(checkIndicator.gameObject.activeSelf, "Check indicator must be UNCHECKED (inactive) when unlocked");
        Assert.IsTrue(grab1.trackPosition, "Position tracking must be RESTORED when unlocked");
        Assert.IsTrue(grab1.trackRotation, "Rotation tracking must be RESTORED when unlocked");
        Assert.IsTrue(grab1.trackScale, "Scale tracking must be RESTORED when unlocked");
        Debug.Log("TEST 5c PASSED: Click Unlock -> Check indicator is UNCHECKED, button says 'Lock', manipulations restored.");

        // 7. Test Switching Selection to Another Object ("Master Sofa(Clone)")
        var testObj2 = new GameObject("Master Sofa(Clone)");
        var childItem2 = new GameObject("Furniture Item");
        childItem2.transform.SetParent(testObj2.transform);
        var fItem2 = childItem2.AddComponent<FurnitureItem>();
        var dims2 = testObj2.AddComponent<ARObjectDimensions>();
        var grab2 = testObj2.AddComponent<XRGrabInteractable>();
        dims2.SetLocked(true); // 2nd object starts locked

        Assert.AreEqual("Master Sofa", dims2.DisplayName, "2nd object DisplayName must resolve to 'Master Sofa'!");

        ARObjectDimensions.SelectObject(dims2);
        updateBadge.Invoke(menuManager, null);
        updateLockVisual.Invoke(menuManager, null);

        Assert.AreEqual("Master Sofa", badgeLabel.text, "Badge should update to second object name 'Master Sofa'");
        Assert.AreEqual("Unlock", lockLabel.text, "Lock button label should reflect 2nd object state ('Unlock')");
        Assert.IsTrue(checkIndicator.gameObject.activeSelf, "Check indicator must be CHECKED for locked 2nd object");
        Debug.Log("TEST 6 PASSED: Select second object ('Master Sofa') -> Badge updated to 'Master Sofa', Check indicator and text reflect 2nd object state.");

        // Cleanup test objects
        UnityEngine.Object.DestroyImmediate(testObj1);
        UnityEngine.Object.DestroyImmediate(testObj2);

        // 8. Test Welcome Screen Layout, Non-Overlapping Elements, Logo, and StartCoaching
        Debug.Log("--- Testing Welcome Screen Layout & Behavior ---");
        var gm = UnityEngine.Object.FindObjectOfType<GoalManager>(true);
        Assert.IsNotNull(gm, "GoalManager not found in scene!");
        var greetingPrompt = gm.greetingPrompt;
        Assert.IsNotNull(greetingPrompt, "GoalManager.greetingPrompt is null!");
        Assert.IsTrue(greetingPrompt.activeSelf, "Greeting Prompt should be active by default!");

        var gpRect = greetingPrompt.GetComponent<RectTransform>();
        Assert.IsNotNull(gpRect, "Greeting Prompt missing RectTransform!");
        Assert.AreEqual(Vector2.zero, gpRect.anchorMin, "Greeting Prompt anchorMin should be (0, 0) full screen");
        Assert.AreEqual(Vector2.one, gpRect.anchorMax, "Greeting Prompt anchorMax should be (1, 1) full screen");

        var ctaTrans = greetingPrompt.transform.Find("GreetingCTA");
        Assert.IsNotNull(ctaTrans, "GreetingCTA not found inside Greeting Prompt!");
        var ctaRect = ctaTrans.GetComponent<RectTransform>();
        Assert.AreEqual(Vector3.one, ctaRect.localScale, "GreetingCTA scale must be (1, 1, 1)");
        Assert.GreaterOrEqual(ctaRect.sizeDelta.x, 500f, "Card width should be >= 500 for comfortable mobile reading");
        Assert.GreaterOrEqual(ctaRect.sizeDelta.y, 600f, "Card height should be >= 600");

        // Verify Logo
        var logoTrans = ctaTrans.Find("Logo");
        if (logoTrans == null) logoTrans = ctaTrans.Find("Image");
        Assert.IsNotNull(logoTrans, "Logo GameObject not found in GreetingCTA!");
        var logoImg = logoTrans.GetComponent<Image>();
        Assert.IsNotNull(logoImg, "Image component missing on Logo!");
        Assert.IsTrue(logoImg.preserveAspect, "Logo must have preserveAspect == true to prevent stretching!");
        Assert.IsNotNull(logoImg.sprite, "Logo sprite should not be null!");
        Assert.IsTrue(logoImg.sprite.name.Contains("MebelAR") || logoImg.sprite.name.Contains("TopLogo"), $"Logo sprite should be MebelAR logo, got: {logoImg.sprite.name}");

        // Verify Heading
        var headingTrans = ctaTrans.Find("Welcome Heading");
        if (headingTrans == null) headingTrans = ctaTrans.Find("Text Greeting");
        Assert.IsNotNull(headingTrans, "Heading GameObject not found in GreetingCTA!");
        var headingTMP = headingTrans.GetComponent<TextMeshProUGUI>();
        Assert.IsNotNull(headingTMP, "TextMeshProUGUI missing on Welcome Heading!");
        Assert.IsTrue(headingTMP.text.Contains("Welcome to MebelAR"), $"Heading must contain 'Welcome to MebelAR', got: {headingTMP.text}");

        // Verify Get Started Button
        var btnTrans = ctaTrans.Find("Get Started Button");
        if (btnTrans == null) btnTrans = ctaTrans.Find("Button");
        if (btnTrans == null) btnTrans = ctaTrans.Find("Continue Button");
        Assert.IsNotNull(btnTrans, "Get Started Button not found in GreetingCTA!");
        var btnComp = btnTrans.GetComponent<Button>();
        Assert.IsNotNull(btnComp, "Button component missing on Get Started Button!");
        var btnRect = btnTrans.GetComponent<RectTransform>();
        Assert.GreaterOrEqual(btnRect.sizeDelta.x, 240f, "Button width must be >= 240 for comfortable mobile tap target");
        Assert.GreaterOrEqual(btnRect.sizeDelta.y, 48f, "Button height must be >= 48 for comfortable mobile tap target");

        var btnTMP = btnTrans.GetComponentInChildren<TextMeshProUGUI>();
        Assert.IsNotNull(btnTMP, "TextMeshProUGUI label missing on Get Started Button!");
        Assert.AreEqual("Get Started", btnTMP.text, "Button text should say 'Get Started'");

        // Verify Non-Overlapping Vertical Layout Order:
        // Heading (top) > Logo (middle) > Button (bottom)
        var headingRect = headingTrans.GetComponent<RectTransform>();
        var logoRect = logoTrans.GetComponent<RectTransform>();

        float headingBottom = headingRect.anchoredPosition.y - (headingRect.sizeDelta.y * 0.5f);
        float logoTop = logoRect.anchoredPosition.y + (logoRect.sizeDelta.y * 0.5f);
        float logoBottom = logoRect.anchoredPosition.y - (logoRect.sizeDelta.y * 0.5f);
        float btnTop = btnRect.anchoredPosition.y + (btnRect.sizeDelta.y * 0.5f);

        Assert.Greater(headingRect.anchoredPosition.y, logoRect.anchoredPosition.y, "Heading must be positioned above Logo!");
        Assert.Greater(headingBottom, logoTop, $"Heading bottom ({headingBottom}) must be above Logo top ({logoTop}) - NO OVERLAP!");
        Assert.Greater(logoRect.anchoredPosition.y, btnRect.anchoredPosition.y, "Logo must be positioned above Button!");
        Assert.Greater(logoBottom, btnTop, $"Logo bottom ({logoBottom}) must be above Button top ({btnTop}) - NO OVERLAP!");
        Debug.Log($"TEST 7 PASSED: Non-overlapping vertical layout verified: HeadingBottom={headingBottom} > LogoTop={logoTop} > LogoBottom={logoBottom} > BtnTop={btnTop}");

        // Verify StartCoaching Click Event
        btnComp.onClick.Invoke();
        Assert.IsFalse(greetingPrompt.activeSelf, "Clicking Get Started button must deactivate greetingPrompt via StartCoaching!");
        Debug.Log("TEST 8 PASSED: Get Started button click event verified: StartCoaching() dismissed greeting prompt.");

        // Restore greeting prompt active for scene
        greetingPrompt.SetActive(true);

        Debug.Log("================ ALL FEATURE TESTS COMPLETED SUCCESSFULLY ================");
    }
}
