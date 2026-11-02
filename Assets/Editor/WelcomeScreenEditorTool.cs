using System;
using MobileARTemplateAssets.Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WelcomeScreenEditorTool
{
    private const string SCENE_PATH = "Assets/Scenes/SampleScene.unity";
    private const string PREFAB_PATH = "Assets/MobileARTemplateAssets/UI/Prefabs/GreetingCTA.prefab";
    private const string LOGO_PATH = "Assets/MobileARTemplateAssets/UI/Sprites/ReferenceAssets/MebelAR_TopLogo.png";
    private const string FONT_PATH = "Assets/MobileARTemplateAssets/UI/Fonts/Inter-Regular_SDF_Underlay.asset";

    [MenuItem("MebelAR/Setup Welcome UI")]
    public static void SetupWelcomeUI()
    {
        Debug.Log(">>> [WelcomeScreenEditorTool] Starting Welcome UI Setup...");

        // 1. Load assets
        Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LOGO_PATH);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

        if (logoSprite == null)
        {
            Debug.LogError($"Could not find logo sprite at: {LOGO_PATH}");
            return;
        }

        // 2. Open Scene
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"Could not open scene at: {SCENE_PATH}");
            return;
        }

        // 3. Find GoalManager and Greeting Prompt
        var goalManager = UnityEngine.Object.FindObjectOfType<GoalManager>(true);
        if (goalManager == null)
        {
            Debug.LogError("GoalManager not found in scene!");
            return;
        }

        GameObject greetingPrompt = goalManager.greetingPrompt;
        if (greetingPrompt == null)
        {
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Transform gpTrans = canvas.transform.Find("Greeting Prompt");
                if (gpTrans != null) greetingPrompt = gpTrans.gameObject;
            }
        }

        if (greetingPrompt == null)
        {
            Debug.LogError("Greeting Prompt GameObject not found!");
            return;
        }

        // 4. Configure Greeting Prompt (Full screen scrim)
        RectTransform gpRect = greetingPrompt.GetComponent<RectTransform>();
        if (gpRect != null)
        {
            gpRect.anchorMin = Vector2.zero;
            gpRect.anchorMax = Vector2.one;
            gpRect.offsetMin = Vector2.zero;
            gpRect.offsetMax = Vector2.zero;
            gpRect.pivot = new Vector2(0.5f, 0.5f);
            gpRect.localScale = Vector3.one;
        }

        // Add scrim Image to Greeting Prompt if missing
        Image gpScrim = greetingPrompt.GetComponent<Image>();
        if (gpScrim == null)
        {
            gpScrim = greetingPrompt.AddComponent<Image>();
        }
        gpScrim.sprite = null;
        gpScrim.color = new Color(0.06f, 0.09f, 0.16f, 0.90f); // Deep translucent navy
        gpScrim.raycastTarget = true;

        // Add WelcomeScreenLayout component
        var layoutComp = greetingPrompt.GetComponent<WelcomeScreenLayout>();
        if (layoutComp == null)
        {
            layoutComp = greetingPrompt.AddComponent<WelcomeScreenLayout>();
        }

        // 5. Look for GreetingCTA child
        Transform ctaTrans = greetingPrompt.transform.Find("GreetingCTA");
        if (ctaTrans == null && greetingPrompt.transform.childCount > 0)
        {
            ctaTrans = greetingPrompt.transform.GetChild(0);
        }

        GameObject ctaGO;
        if (ctaTrans != null)
        {
            ctaGO = ctaTrans.gameObject;
        }
        else
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            ctaGO = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, greetingPrompt.transform);
            ctaGO.name = "GreetingCTA";
            ctaTrans = ctaGO.transform;
        }

        // Configure GreetingCTA RectTransform (Centered card)
        RectTransform ctaRect = ctaGO.GetComponent<RectTransform>();
        ctaRect.anchorMin = new Vector2(0.5f, 0.5f);
        ctaRect.anchorMax = new Vector2(0.5f, 0.5f);
        ctaRect.pivot = new Vector2(0.5f, 0.5f);
        ctaRect.anchoredPosition = Vector2.zero;
        ctaRect.sizeDelta = new Vector2(580f, 640f);
        ctaRect.localScale = Vector3.one;

        // Card background Image
        Image ctaBg = ctaGO.GetComponent<Image>();
        if (ctaBg == null) ctaBg = ctaGO.AddComponent<Image>();
        ctaBg.sprite = null;
        ctaBg.color = new Color(0.09f, 0.14f, 0.23f, 0.98f); // Solid Navy card #17243A
        ctaBg.raycastTarget = true;

        Outline ctaOutline = ctaGO.GetComponent<Outline>();
        if (ctaOutline == null) ctaOutline = ctaGO.AddComponent<Outline>();
        ctaOutline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.70f);
        ctaOutline.effectDistance = new Vector2(1f, -1f);

        // VerticalLayoutGroup on Card
        VerticalLayoutGroup vlg = ctaGO.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = ctaGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 44, 44);
        vlg.spacing = 22f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // Remove redundant duplicate backplates if they exist as children
        Transform backBottom = ctaTrans.Find("Backplate_Bottom");
        if (backBottom != null) UnityEngine.Object.DestroyImmediate(backBottom.gameObject);
        Transform backTop = ctaTrans.Find("Backplate_Top");
        if (backTop != null) UnityEngine.Object.DestroyImmediate(backTop.gameObject);

        // 6. Element 1: Welcome Heading
        Transform headingTrans = ctaTrans.Find("Welcome Heading");
        if (headingTrans == null) headingTrans = ctaTrans.Find("Text Greeting");
        GameObject headingGO;
        if (headingTrans != null)
        {
            headingGO = headingTrans.gameObject;
            headingGO.name = "Welcome Heading";
        }
        else
        {
            headingGO = new GameObject("Welcome Heading");
            headingGO.transform.SetParent(ctaTrans, false);
            headingTrans = headingGO.transform;
        }
        headingTrans.SetSiblingIndex(0);

        RectTransform headingRect = headingGO.GetComponent<RectTransform>();
        if (headingRect == null) headingRect = headingGO.AddComponent<RectTransform>();
        headingRect.sizeDelta = new Vector2(508f, 44f);

        TextMeshProUGUI headingTMP = headingGO.GetComponent<TextMeshProUGUI>();
        if (headingTMP == null) headingTMP = headingGO.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) headingTMP.font = fontAsset;
        headingTMP.text = "Welcome to MebelAR";
        headingTMP.fontSize = 32f;
        headingTMP.fontStyle = FontStyles.Bold;
        headingTMP.color = Color.white;
        headingTMP.alignment = TextAlignmentOptions.Center;
        headingTMP.enableWordWrapping = false;
        headingTMP.raycastTarget = false;

        LayoutElement headingLE = headingGO.GetComponent<LayoutElement>();
        if (headingLE == null) headingLE = headingGO.AddComponent<LayoutElement>();
        headingLE.minHeight = 44f;
        headingLE.preferredHeight = 44f;
        headingLE.flexibleWidth = 1f;

        // 7. Element 2: Welcome Subtitle
        Transform subTrans = ctaTrans.Find("Welcome Subtitle");
        GameObject subGO;
        if (subTrans != null)
        {
            subGO = subTrans.gameObject;
        }
        else
        {
            subGO = new GameObject("Welcome Subtitle");
            subGO.transform.SetParent(ctaTrans, false);
            subTrans = subGO.transform;
        }
        subTrans.SetSiblingIndex(1);

        RectTransform subRect = subGO.GetComponent<RectTransform>();
        if (subRect == null) subRect = subGO.AddComponent<RectTransform>();
        subRect.sizeDelta = new Vector2(508f, 52f);

        TextMeshProUGUI subTMP = subGO.GetComponent<TextMeshProUGUI>();
        if (subTMP == null) subTMP = subGO.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) subTMP.font = fontAsset;
        subTMP.text = "Visualize and arrange furniture in your space with augmented reality";
        subTMP.fontSize = 18f;
        subTMP.fontStyle = FontStyles.Normal;
        subTMP.color = new Color(0.80f, 0.84f, 0.88f, 1f); // Soft slate #CBD5E1
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.enableWordWrapping = true;
        subTMP.lineSpacing = 10f;
        subTMP.raycastTarget = false;

        LayoutElement subLE = subGO.GetComponent<LayoutElement>();
        if (subLE == null) subLE = subGO.AddComponent<LayoutElement>();
        subLE.minHeight = 52f;
        subLE.preferredHeight = 52f;
        subLE.flexibleWidth = 1f;

        // 8. Element 3: MebelAR Logo
        Transform logoTrans = ctaTrans.Find("Logo");
        if (logoTrans == null) logoTrans = ctaTrans.Find("Image");
        GameObject logoGO;
        if (logoTrans != null)
        {
            logoGO = logoTrans.gameObject;
            logoGO.name = "Logo";
        }
        else
        {
            logoGO = new GameObject("Logo");
            logoGO.transform.SetParent(ctaTrans, false);
            logoTrans = logoGO.transform;
        }
        logoTrans.SetSiblingIndex(2);

        RectTransform logoRect = logoGO.GetComponent<RectTransform>();
        if (logoRect == null) logoRect = logoGO.AddComponent<RectTransform>();
        // Aspect ratio 3.36: 360 x 107
        logoRect.sizeDelta = new Vector2(360f, 107f);

        Image logoImg = logoGO.GetComponent<Image>();
        if (logoImg == null) logoImg = logoGO.AddComponent<Image>();
        logoImg.sprite = logoSprite;
        logoImg.preserveAspect = true;
        logoImg.color = Color.white;
        logoImg.raycastTarget = false;

        LayoutElement logoLE = logoGO.GetComponent<LayoutElement>();
        if (logoLE == null) logoLE = logoGO.AddComponent<LayoutElement>();
        logoLE.minHeight = 107f;
        logoLE.preferredHeight = 107f;
        logoLE.minWidth = 360f;
        logoLE.preferredWidth = 360f;

        // 9. Element 4: Get Started Button
        Transform btnTrans = ctaTrans.Find("Get Started Button");
        if (btnTrans == null) btnTrans = ctaTrans.Find("Button");
        if (btnTrans == null) btnTrans = ctaTrans.Find("Continue Button");
        GameObject btnGO;
        if (btnTrans != null)
        {
            btnGO = btnTrans.gameObject;
            btnGO.name = "Get Started Button";
        }
        else
        {
            btnGO = new GameObject("Get Started Button");
            btnGO.transform.SetParent(ctaTrans, false);
            btnTrans = btnGO.transform;
        }
        btnTrans.SetSiblingIndex(3);

        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        if (btnRect == null) btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(340f, 60f);

        Image btnImg = btnGO.GetComponent<Image>();
        if (btnImg == null) btnImg = btnGO.AddComponent<Image>();
        btnImg.sprite = null;
        btnImg.color = new Color(0.08f, 0.58f, 0.53f, 1f); // Vibrant Teal #14B8A6
        btnImg.raycastTarget = true;

        Outline btnOutline = btnGO.GetComponent<Outline>();
        if (btnOutline == null) btnOutline = btnGO.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.20f, 0.75f, 0.68f, 0.60f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnGO.GetComponent<Button>();
        if (btn == null) btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // Wire UnityEvent to GoalManager.StartCoaching
        btn.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(goalManager.StartCoaching));

        LayoutElement btnLE = btnGO.GetComponent<LayoutElement>();
        if (btnLE == null) btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.minHeight = 60f;
        btnLE.preferredHeight = 60f;
        btnLE.minWidth = 340f;
        btnLE.preferredWidth = 340f;

        // Button child text
        Transform btnTextTrans = btnTrans.Find("Text");
        GameObject btnTextGO;
        if (btnTextTrans != null)
        {
            btnTextGO = btnTextTrans.gameObject;
        }
        else
        {
            btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnTrans, false);
            btnTextTrans = btnTextGO.transform;
        }

        RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
        if (btnTextRect == null) btnTextRect = btnTextGO.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTMP = btnTextGO.GetComponent<TextMeshProUGUI>();
        if (btnTMP == null) btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) btnTMP.font = fontAsset;
        btnTMP.text = "Get Started";
        btnTMP.fontSize = 22f;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.color = Color.white;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.raycastTarget = false;

        // 10. Update WelcomeScreenLayout references
        layoutComp.FindReferencesIfNull();
        layoutComp.ConfigureLayout();
        layoutComp.ApplySafeArea();
        layoutComp.WireButtonEvent();

        // Ensure Greeting Prompt is active by default
        greetingPrompt.SetActive(true);

        // 11. Save Prefab and Scene
        PrefabUtility.SaveAsPrefabAssetAndConnect(ctaGO, PREFAB_PATH, InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(">>> [WelcomeScreenEditorTool] Welcome UI Setup Completed Successfully!");
    }
}

