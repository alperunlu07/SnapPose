using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor
{
    public static class SnapPoseStyles
    {
        // ── Colour Palette ────────────────────────────────────────────────────
        public static readonly Color Accent        = new Color(0.31f, 0.76f, 0.97f, 1f);   // #4FC3F7 cyan
        public static readonly Color AccentDark    = new Color(0.20f, 0.55f, 0.75f, 1f);
        public static readonly Color AccentHover   = new Color(0.50f, 0.85f, 1.00f, 1f);
        public static readonly Color BgPrimary     = new Color(0.18f, 0.18f, 0.18f, 1f);   // #2D2D2D
        public static readonly Color BgSecondary   = new Color(0.22f, 0.22f, 0.22f, 1f);   // #383838
        public static readonly Color BgTertiary    = new Color(0.26f, 0.26f, 0.26f, 1f);   // #424242
        public static readonly Color BgCard        = new Color(0.16f, 0.16f, 0.16f, 1f);
        public static readonly Color BorderColor   = new Color(0.12f, 0.12f, 0.12f, 1f);
        public static readonly Color TextPrimary   = new Color(0.90f, 0.90f, 0.90f, 1f);
        public static readonly Color TextSecondary = new Color(0.60f, 0.60f, 0.60f, 1f);
        public static readonly Color TextDisabled  = new Color(0.40f, 0.40f, 0.40f, 1f);
        public static readonly Color WarningColor  = new Color(1.00f, 0.76f, 0.03f, 1f);   // amber
        public static readonly Color DangerColor   = new Color(0.95f, 0.35f, 0.35f, 1f);   // red
        public static readonly Color SuccessColor  = new Color(0.40f, 0.80f, 0.40f, 1f);   // green

        // ── USS Stylesheet ────────────────────────────────────────────────────
        public const string USS = @"
/* ── Root ─────────────────────────────────────────────────────────────── */
.snap-root {
    flex-direction: row;
    flex-grow: 1;
    background-color: rgb(46, 46, 46);
}

/* ── Panels ─────────────────────────────────────────────────────────────── */
.snap-workspace {
    min-width: 200px;
    max-width: 260px;
    background-color: rgb(40, 40, 40);
    border-right-width: 1px;
    border-right-color: rgb(30, 30, 30);
}

.snap-inspector {
    flex-grow: 1;
    background-color: rgb(46, 46, 46);
}

/* ── Header ────────────────────────────────────────────────────────────── */
.snap-section-header {
    flex-direction: row;
    align-items: center;
    padding: 6px 10px;
    background-color: rgb(35, 35, 35);
    border-bottom-width: 1px;
    border-bottom-color: rgb(25, 25, 25);
    margin-bottom: 2px;
}

.snap-section-header-accent {
    width: 3px;
    height: 14px;
    background-color: rgb(79, 195, 247);
    border-radius: 2px;
    margin-right: 7px;
}

.snap-section-title {
    font-size: 11px;
    -unity-font-style: bold;
    color: rgb(200, 200, 200);
    flex-grow: 1;
}

/* ── Card ───────────────────────────────────────────────────────────────── */
.snap-card {
    background-color: rgb(41, 41, 41);
    border-radius: 4px;
    border-width: 1px;
    border-color: rgb(30, 30, 30);
    margin: 4px 6px;
    padding: 8px;
}

/* ── Buttons ────────────────────────────────────────────────────────────── */
.snap-btn-primary {
    background-color: rgb(79, 195, 247);
    color: rgb(15, 15, 15);
    border-radius: 4px;
    border-width: 0px;
    padding: 7px 14px;
    -unity-font-style: bold;
    font-size: 12px;
    cursor: link;
}

.snap-btn-primary:hover {
    background-color: rgb(128, 216, 255);
}

.snap-btn-primary:active {
    background-color: rgb(51, 168, 222);
}

.snap-btn-secondary {
    background-color: rgb(60, 60, 60);
    color: rgb(200, 200, 200);
    border-radius: 4px;
    border-width: 1px;
    border-color: rgb(80, 80, 80);
    padding: 5px 12px;
    font-size: 11px;
    cursor: link;
}

.snap-btn-secondary:hover {
    background-color: rgb(75, 75, 75);
}

.snap-btn-danger {
    background-color: rgb(80, 30, 30);
    color: rgb(240, 100, 100);
    border-radius: 4px;
    border-width: 1px;
    border-color: rgb(120, 40, 40);
    padding: 5px 12px;
    font-size: 11px;
    cursor: link;
}

.snap-btn-danger:hover {
    background-color: rgb(100, 40, 40);
}

.snap-btn-icon {
    background-color: rgba(0,0,0,0);
    border-width: 0px;
    padding: 2px 4px;
    cursor: link;
    color: rgb(150, 150, 150);
    font-size: 14px;
}

.snap-btn-icon:hover {
    color: rgb(79, 195, 247);
}

/* ── Labels ─────────────────────────────────────────────────────────────── */
.snap-label-primary {
    color: rgb(210, 210, 210);
    font-size: 11px;
}

.snap-label-secondary {
    color: rgb(140, 140, 140);
    font-size: 10px;
}

.snap-label-accent {
    color: rgb(79, 195, 247);
    font-size: 11px;
    -unity-font-style: bold;
}

.snap-label-warning {
    color: rgb(255, 193, 7);
    font-size: 10px;
}

.snap-label-danger {
    color: rgb(242, 89, 89);
    font-size: 10px;
}

/* ── Separator ──────────────────────────────────────────────────────────── */
.snap-separator {
    height: 1px;
    background-color: rgb(30, 30, 30);
    margin: 6px 0;
}

/* ── Object Item (Workspace) ─────────────────────────────────────────────── */
.snap-object-item {
    flex-direction: row;
    align-items: center;
    padding: 5px 8px;
    border-radius: 3px;
    margin: 1px 4px;
    cursor: link;
}

.snap-object-item:hover {
    background-color: rgb(60, 60, 60);
}

.snap-object-item--selected {
    background-color: rgb(28, 70, 90);
    border-width: 1px;
    border-color: rgb(79, 195, 247);
}

.snap-object-dot {
    width: 7px;
    height: 7px;
    border-radius: 4px;
    background-color: rgb(79, 195, 247);
    margin-right: 7px;
}

.snap-object-dot--generic {
    background-color: rgb(120, 190, 80);
}

.snap-object-dot--humanoid {
    background-color: rgb(79, 195, 247);
}

/* ── Timeline ───────────────────────────────────────────────────────────── */
.snap-timeline-track {
    height: 40px;
    background-color: rgb(30, 30, 30);
    border-radius: 4px;
    margin: 4px 0;
}

.snap-timeline-head {
    position: absolute;
    width: 2px;
    background-color: rgb(79, 195, 247);
    top: 0;
    bottom: 0;
}

.snap-frame-badge {
    background-color: rgb(79, 195, 247);
    color: rgb(10, 10, 10);
    border-radius: 10px;
    padding: 2px 8px;
    -unity-font-style: bold;
    font-size: 11px;
}

/* ── Pose Layer ─────────────────────────────────────────────────────────── */
.snap-layer-item {
    background-color: rgb(38, 38, 38);
    border-radius: 4px;
    border-width: 1px;
    border-color: rgb(28, 28, 28);
    margin: 3px 6px;
    padding: 6px 8px;
}

.snap-layer-item:hover {
    border-color: rgb(79, 195, 247);
}

/* ── Diff View ──────────────────────────────────────────────────────────── */
.snap-diff-row {
    flex-direction: row;
    align-items: center;
    padding: 4px 8px;
    border-radius: 3px;
    margin: 1px 4px;
}

.snap-diff-row--high {
    background-color: rgba(242, 89, 89, 0.08);
    border-left-width: 2px;
    border-left-color: rgb(242, 89, 89);
}

.snap-diff-row--medium {
    background-color: rgba(255, 193, 7, 0.06);
    border-left-width: 2px;
    border-left-color: rgb(255, 193, 7);
}

.snap-diff-row--low {
    border-left-width: 2px;
    border-left-color: rgb(60, 60, 60);
}

/* ── History ────────────────────────────────────────────────────────────── */
.snap-history-item {
    flex-direction: row;
    align-items: center;
    padding: 5px 8px;
    margin: 2px 4px;
    border-radius: 3px;
    background-color: rgb(38, 38, 38);
}

.snap-history-item:hover {
    background-color: rgb(50, 50, 50);
}

/* ── Play Button ────────────────────────────────────────────────────────── */
.snap-btn-play {
    background-color: rgb(30, 30, 30);
    color: rgb(79, 195, 247);
    border-radius: 14px;
    border-width: 1px;
    border-color: rgb(79, 195, 247);
    padding: 3px 14px;
    font-size: 13px;
    cursor: link;
    -unity-font-style: bold;
    min-width: 40px;
}

.snap-btn-play:hover {
    background-color: rgb(20, 55, 75);
}

.snap-btn-play:active {
    background-color: rgb(10, 40, 60);
}

/* ── Humanoid Muscle Inspector ──────────────────────────────────────────── */
.snap-muscle-group {
    margin-bottom: 4px;
    border-radius: 3px;
    border-width: 1px;
    border-color: rgb(30, 30, 30);
    background-color: rgb(36, 36, 36);
}

.snap-muscle-group-header {
    flex-direction: row;
    align-items: center;
    padding: 5px 8px;
    background-color: rgb(32, 32, 32);
    border-radius: 3px;
    cursor: link;
}

.snap-muscle-group-header:hover {
    background-color: rgb(42, 42, 42);
}

.snap-muscle-slider-container {
    padding: 4px 6px;
}

.snap-muscle-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: 2px;
}

/* ── Apply Mode Badge ───────────────────────────────────────────────────── */
.snap-mode-badge {
    font-size: 9px;
    -unity-font-style: bold;
    border-radius: 8px;
    padding: 2px 8px;
    margin-left: 8px;
    flex-shrink: 0;
}

.snap-mode-badge--permanent {
    background-color: rgb(20, 55, 75);
    color: rgb(79, 195, 247);
}

.snap-mode-badge--preview {
    background-color: rgb(45, 25, 75);
    color: rgb(180, 130, 255);
}

/* ── Bone Mask Editor ───────────────────────────────────────────────────── */
.snap-mask-list-container {
    margin-top: 6px;
    padding: 6px;
    background-color: rgb(35, 35, 35);
    border-radius: 4px;
    border-width: 1px;
    border-color: rgb(28, 28, 28);
}

.snap-mask-scroll {
    border-radius: 3px;
    background-color: rgb(28, 28, 28);
}

.snap-mask-bone-row {
    flex-direction: row;
    align-items: center;
    padding: 2px 6px;
    border-radius: 2px;
    cursor: link;
}

.snap-mask-bone-row:hover {
    background-color: rgb(50, 50, 50);
}

.snap-mask-summary {
    font-size: 9px;
    margin-left: 6px;
    padding: 1px 6px;
    border-radius: 8px;
    background-color: rgb(30, 30, 30);
    -unity-font-style: bold;
}

/* ── Misc ────────────────────────────────────────────────────────────────── */
.snap-badge {
    background-color: rgb(30, 30, 30);
    border-radius: 10px;
    padding: 2px 7px;
    font-size: 10px;
    color: rgb(140, 140, 140);
    margin-right: 3px;
}

.snap-badge--accent {
    background-color: rgb(20, 55, 75);
    color: rgb(79, 195, 247);
}

.snap-scrollview {
    flex-grow: 1;
}

.unity-scroll-view__content-container {
    padding: 0;
}

.snap-rig-badge {
    font-size: 9px;
    border-radius: 3px;
    padding: 1px 5px;
    margin-left: 5px;
}

.snap-rig-badge--generic {
    background-color: rgb(40, 65, 30);
    color: rgb(120, 190, 80);
}

.snap-rig-badge--humanoid {
    background-color: rgb(20, 55, 75);
    color: rgb(79, 195, 247);
}
";

        // ── Helpers ───────────────────────────────────────────────────────────
        public static VisualElement MakeSectionHeader(string title, string iconChar = null)
        {
            var row = new VisualElement();
            row.AddToClassList("snap-section-header");

            var accent = new VisualElement();
            accent.AddToClassList("snap-section-header-accent");
            row.Add(accent);

            var lbl = new Label(iconChar != null ? $"{iconChar}  {title}" : title);
            lbl.AddToClassList("snap-section-title");
            row.Add(lbl);

            return row;
        }

        public static VisualElement MakeSeparator()
        {
            var sep = new VisualElement();
            sep.AddToClassList("snap-separator");
            return sep;
        }

        public static Button MakePrimaryButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("snap-btn-primary");
            return btn;
        }

        public static Button MakeSecondaryButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("snap-btn-secondary");
            return btn;
        }

        public static Button MakeDangerButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("snap-btn-danger");
            return btn;
        }

        public static Button MakeIconButton(string icon, System.Action onClick, string tooltip = "")
        {
            var btn = new Button(onClick) { text = icon, tooltip = tooltip };
            btn.AddToClassList("snap-btn-icon");
            return btn;
        }

        public static Label MakeBadge(string text, bool accent = false)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("snap-badge");
            if (accent) lbl.AddToClassList("snap-badge--accent");
            return lbl;
        }

        public static VisualElement MakeCard()
        {
            var card = new VisualElement();
            card.AddToClassList("snap-card");
            return card;
        }
    }
}
