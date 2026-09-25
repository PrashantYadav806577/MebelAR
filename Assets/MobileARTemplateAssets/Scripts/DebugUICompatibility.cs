using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.XR.ARFoundation
{
    /// <summary>
    /// Compatibility definition of DebugSlider for Unity 2022.1 / AR Foundation 4.2.x.
    /// </summary>
    public class DebugSlider : Slider
    {
    }

    /// <summary>
    /// Compatibility definition of ARDebugMenu for Unity 2022.1 / AR Foundation 4.2.x.
    /// </summary>
    public class ARDebugMenu : MonoBehaviour
    {
        [SerializeField]
        Transform m_Toolbar;
        public Transform toolbar => m_Toolbar;

        [SerializeField]
        Transform m_DisplayInfoMenuButton;
        public Transform displayInfoMenuButton => m_DisplayInfoMenuButton;

        [SerializeField]
        Transform m_DisplayConfigurationsMenuButton;
        public Transform displayConfigurationsMenuButton => m_DisplayConfigurationsMenuButton;

        [SerializeField]
        Transform m_DisplayCameraConfigurationsMenuButton;
        public Transform displayCameraConfigurationsMenuButton => m_DisplayCameraConfigurationsMenuButton;

        [SerializeField]
        Transform m_DisplayDebugOptionsMenuButton;
        public Transform displayDebugOptionsMenuButton => m_DisplayDebugOptionsMenuButton;

        [SerializeField]
        Transform m_InfoMenu;
        public Transform infoMenu => m_InfoMenu;

        [SerializeField]
        Transform m_ConfigurationMenu;
        public Transform configurationMenu => m_ConfigurationMenu;

        [SerializeField]
        Transform m_CameraConfigurationMenu;
        public Transform cameraConfigurationMenu => m_CameraConfigurationMenu;

        [SerializeField]
        Transform m_DebugOptionsMenu;
        public Transform debugOptionsMenu => m_DebugOptionsMenu;
    }
}
