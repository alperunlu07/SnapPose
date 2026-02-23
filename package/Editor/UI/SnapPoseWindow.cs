using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SnapPose.Editor.UI;

namespace SnapPose.Editor
{
    public class SnapPoseWindow : EditorWindow
    {
        // ── State ─────────────────────────────────────────────────────────────
        SnapPoseController _ctrl;
        WorkspacePanel     _workspacePanel;
        InspectorPanel     _inspectorPanel;

        // ── Window setup ──────────────────────────────────────────────────────
        [MenuItem("Tools/SnapPose %#p", priority = 100)]
        public static void Open()
        {
            var win = GetWindow<SnapPoseWindow>("SnapPose");
            win.minSize = new Vector2(620, 500);
            win.Show();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        void OnEnable()
        {
            _ctrl = new SnapPoseController();
            BuildUI();
        }

        void OnDisable()
        {
            _ctrl?.StopPreview();
        }

        void CreateGUI()
        {
            // If OnEnable hasn't run yet (can happen on domain reload)
            if (_ctrl == null) _ctrl = new SnapPoseController();
            BuildUI();
        }

        // ── UI construction ───────────────────────────────────────────────────
        void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            // Inject stylesheet
            var sheet = new StyleSheet();
            root.styleSheets.Add(LoadOrCreateStyleSheet());

            // Root layout
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow      = 1;

            // ── Title Bar ────────────────────────────────────────────────────
            var titleBar = BuildTitleBar();
            titleBar.style.flexShrink = 0; // Never shrink - always visible
            root.Add(titleBar);

            // ── Main split layout ─────────────────────────────────────────────
            var split = new TwoPaneSplitView(0, 220f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.style.flexShrink = 1; // Allow shrinking when needed
            split.style.minHeight = 0;  // Can shrink below content height

            _workspacePanel = new WorkspacePanel(_ctrl);
            _workspacePanel.AddToClassList("snap-workspace");

            _inspectorPanel = new InspectorPanel(_ctrl);
            _inspectorPanel.AddToClassList("snap-inspector");

            split.Add(_workspacePanel);
            split.Add(_inspectorPanel);

            root.Add(split);

            // ── Status bar ────────────────────────────────────────────────────
            var statusBar = BuildStatusBar();
            statusBar.style.flexShrink = 0; // Never shrink - always visible
            root.Add(statusBar);

            // Refresh state
            _ctrl.OnStateChanged  += () => Repaint();
            _ctrl.OnPreviewChanged += () => Repaint();
        }

        VisualElement BuildTitleBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection   = FlexDirection.Row,
                    alignItems      = Align.Center,
                    backgroundColor = new Color(0.13f, 0.13f, 0.13f),
                    paddingLeft     = 12,
                    paddingRight    = 8,
                    paddingTop      = 6,
                    paddingBottom   = 6,
                    borderBottomWidth = 2,
                    borderBottomColor = SnapPoseStyles.Accent
                }
            };

            // Logo mark
            var logo = new Label("◈");
            logo.style.fontSize        = 20;
            logo.style.color           = SnapPoseStyles.Accent;
            logo.style.marginRight     = 8;
            logo.style.unityTextAlign  = TextAnchor.MiddleLeft;
            bar.Add(logo);

            // Title
            var title = new Label("Snap");
            title.style.fontSize        = 16;
            title.style.color           = SnapPoseStyles.TextPrimary;
            title.style.unityFontStyleAndWeight  = FontStyle.Bold;
            bar.Add(title);

            var titleAccent = new Label("Pose");
            titleAccent.style.fontSize        = 16;
            titleAccent.style.color           = SnapPoseStyles.Accent;
            titleAccent.style.unityFontStyleAndWeight  = FontStyle.Bold;
            titleAccent.style.marginRight     = 12;
            bar.Add(titleAccent);

            var version = new Label("v1.0");
            version.AddToClassList("snap-label-secondary");
            version.style.fontSize = 9;
            bar.Add(version);

            // Spacer
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            // Docs button
            var docsBtn = SnapPoseStyles.MakeSecondaryButton("? Docs", () =>
                Application.OpenURL("https://github.com/alperunlu07/SnapPose"));
            docsBtn.style.marginLeft = 4;
            bar.Add(docsBtn);

            // Reset button
            var resetBtn = SnapPoseStyles.MakeSecondaryButton("↺ Reset", () =>
            {
                _ctrl.StopPreview();
                _ctrl = new SnapPoseController();
                BuildUI();
            });
            resetBtn.style.marginLeft = 4;
            bar.Add(resetBtn);

            return bar;
        }

        VisualElement BuildStatusBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection     = FlexDirection.Row,
                    alignItems        = Align.Center,
                    backgroundColor   = new Color(0.12f, 0.12f, 0.12f),
                    paddingLeft       = 8,
                    paddingRight      = 8,
                    paddingTop        = 3,
                    paddingBottom     = 3,
                    borderTopWidth    = 1,
                    borderTopColor    = new Color(0.08f, 0.08f, 0.08f)
                }
            };

            var statusLbl = new Label("Ready");
            statusLbl.AddToClassList("snap-label-secondary");
            statusLbl.style.fontSize = 9;

            _ctrl.OnStateChanged += () =>
            {
                if (_ctrl.IsPreviewActive)
                {
                    statusLbl.text  = $"⏺  PREVIEW ACTIVE  —  Frame {_ctrl.CurrentFrame} / {_ctrl.TotalFrames}";
                    statusLbl.style.color = SnapPoseStyles.Accent;
                }
                else
                {
                    statusLbl.text  = _ctrl.TargetObject != null
                        ? $"Target: {_ctrl.TargetObject.name}"
                        : "Select a rigged object from the hierarchy or drag onto workspace.";
                    statusLbl.style.color = SnapPoseStyles.TextSecondary;
                }
            };

            _ctrl.OnPreviewChanged += () =>
            {
                if (_ctrl.IsPreviewActive)
                {
                    statusLbl.text  = $"⏺  PREVIEW ACTIVE  —  Frame {_ctrl.CurrentFrame} / {_ctrl.TotalFrames}";
                    statusLbl.style.color = SnapPoseStyles.Accent;
                }
            };

            bar.Add(statusLbl);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            var undoHint = new Label("Ctrl+Z to undo permanent changes");
            undoHint.AddToClassList("snap-label-secondary");
            undoHint.style.fontSize = 9;
            bar.Add(undoHint);

            return bar;
        }

        // ── StyleSheet helper ─────────────────────────────────────────────────
        StyleSheet LoadOrCreateStyleSheet()
        {
            // Try to load from disk first
            const string path = "Assets/SnapPose/Editor/UI/SnapPoseStyles.uss";
            var existing = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (existing != null) return existing;

            // Create a transient stylesheet from the USS string
            var transient = ScriptableObject.CreateInstance<StyleSheet>();
            // In lieu of USS file, we apply styles inline via the USS class
            // Save USS to disk so it loads properly
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(path, SnapPoseStyles.USS);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        }
    }
}
