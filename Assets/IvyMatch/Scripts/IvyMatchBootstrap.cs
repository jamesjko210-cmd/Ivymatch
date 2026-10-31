using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Builds the whole dashboard at runtime so no scene wiring is needed:
    /// camera, PanelSettings (with runtime theme), UIDocument (Dashboard.uxml) and DashboardController.
    /// Runs after any scene loads; does nothing if the scene already contains a DashboardController.
    /// All UI assets are loaded from Resources/UI.
    /// </summary>
    public static class IvyMatchBootstrap
    {
        public const string DashboardPath = "UI/Dashboard";
        public const string CardPath = "UI/SchoolCard";
        public const string ThemePath = "UI/IvyRuntimeTheme";
        public const string FontPath = "Fonts/NotoSansKR-Regular";

        static readonly string[] OsKoreanFonts =
        {
            "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic",
            "Noto Sans CJK KR", "NotoSansCJK-Regular", "Noto Sans KR", "NanumGothic", "Arial Unicode MS"
        };

        static PanelSettings panelSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Object.FindAnyObjectByType<DashboardController>() != null) return;
            Create();
        }

        public static DashboardController Create()
        {
            VisualTreeAsset dashboard = Resources.Load<VisualTreeAsset>(DashboardPath);
            VisualTreeAsset card = Resources.Load<VisualTreeAsset>(CardPath);
            if (dashboard == null || card == null)
            {
                Debug.LogError("[IvyMatch] Resources/" + DashboardPath + ".uxml or Resources/" + CardPath + ".uxml is missing.");
                return null;
            }

            if (Camera.main == null && Object.FindAnyObjectByType<Camera>() == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x0c, 0x0e, 0x12, 0xff);
                cam.cullingMask = 0;
            }

            var go = new GameObject("IvyMatch Dashboard");
            go.SetActive(false); // configure everything before OnEnable/Start run

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = GetPanelSettings();
            doc.visualTreeAsset = dashboard;

            var controller = go.AddComponent<DashboardController>();
            controller.Configure(doc, card);

            go.SetActive(true);
            return controller;
        }

        public static PanelSettings GetPanelSettings()
        {
            if (panelSettings != null) return panelSettings;

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "IvyMatch PanelSettings (runtime)";
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemePath);
            if (panelSettings.themeStyleSheet == null)
                Debug.LogWarning("[IvyMatch] Resources/" + ThemePath + ".tss missing; default controls will be unstyled.");
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(390, 844);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0f; // scale by width: phone layout stays readable in landscape editor windows
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = new Color32(0x0c, 0x0e, 0x12, 0xff);
            return panelSettings;
        }

        /// <summary>Korean-capable font: bundled Resources font first, then an OS font. Null = theme default.</summary>
        public static Font LoadKoreanFont()
        {
            Font f = Resources.Load<Font>(FontPath);
            if (f != null) return f;

            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string name in OsKoreanFonts)
            {
                if (System.Array.IndexOf(installed, name) < 0) continue;
                f = Font.CreateDynamicFontFromOSFont(name, 16);
                if (f != null) return f;
            }
            Debug.LogWarning("[IvyMatch] No Korean font found. Add Assets/IvyMatch/Resources/" + FontPath + ".ttf.");
            return null;
        }
    }
}
