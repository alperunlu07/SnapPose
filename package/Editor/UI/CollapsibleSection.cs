using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// A collapsible section with a clickable header and expandable content area.
    /// Useful for organizing large inspector panels into foldable sections.
    /// </summary>
    public class CollapsibleSection : VisualElement
    {
        // ── State ─────────────────────────────────────────────────────────────
        bool _isExpanded = true;

        // ── UI Elements ───────────────────────────────────────────────────────
        VisualElement _header;
        Label         _toggleIcon;
        Label         _titleLabel;
        VisualElement _contentContainer;

        // ── Constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Creates a new collapsible section.
        /// </summary>
        /// <param name="title">Section title text</param>
        /// <param name="icon">Optional icon to display before title (e.g., "🦴", "⟺")</param>
        /// <param name="defaultExpanded">Whether the section starts expanded (default: true)</param>
        public CollapsibleSection(string title, string icon = "", bool defaultExpanded = true)
        {
            _isExpanded = defaultExpanded;
            BuildUI(title, icon);
        }

        // ── UI Construction ───────────────────────────────────────────────────
        void BuildUI(string title, string icon)
        {
            // Root container styling
            style.marginBottom = 8;

            // ── Header ────────────────────────────────────────────────────────
            _header = new VisualElement
            {
                style =
                {
                    flexDirection     = FlexDirection.Row,
                    alignItems        = Align.Center,
                    backgroundColor   = new Color(0.18f, 0.18f, 0.18f),
                    paddingTop        = 6,
                    paddingBottom     = 6,
                    paddingLeft       = 8,
                    paddingRight      = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.12f, 0.12f, 0.12f)//,
                    //cursor            = new Cursor() { defaultCursorId = (int)MouseCursor.Link }
                }
            };
            _header.AddManipulator(new Clickable(ToggleExpanded));
            Add(_header);

            // Toggle icon (▶/▼)
            _toggleIcon = new Label(_isExpanded ? "▼" : "▶");
            _toggleIcon.style.fontSize   = 10;
            _toggleIcon.style.color      = new StyleColor(SnapPoseStyles.TextSecondary);
            _toggleIcon.style.marginRight = 6;
            _toggleIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toggleIcon.style.width      = 12;
            _header.Add(_toggleIcon);

            // Section icon (optional)
            if (!string.IsNullOrEmpty(icon))
            {
                var iconLabel = new Label(icon);
                iconLabel.style.fontSize     = 12;
                iconLabel.style.color        = new StyleColor(SnapPoseStyles.Accent);
                iconLabel.style.marginRight  = 6;
                iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _header.Add(iconLabel);
            }

            // Title
            _titleLabel = new Label(title);
            _titleLabel.style.fontSize  = 11;
            _titleLabel.style.color     = new StyleColor(SnapPoseStyles.TextPrimary);
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.flexGrow  = 1;
            _header.Add(_titleLabel);

            // Hover effect
            _header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _header.style.backgroundColor = new Color(0.20f, 0.20f, 0.20f);
                _toggleIcon.style.color = new StyleColor(SnapPoseStyles.Accent);
            });
            _header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                _toggleIcon.style.color = new StyleColor(SnapPoseStyles.TextSecondary);
            });

            // ── Content Container ─────────────────────────────────────────────
            _contentContainer = new VisualElement();
            _contentContainer.style.overflow = Overflow.Hidden;
            Add(_contentContainer);

            // Set initial state
            UpdateExpandedState(animated: false);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Adds content to the collapsible section.
        /// Content will be shown/hidden when the section is expanded/collapsed.
        /// </summary>
        public void AddContent(VisualElement element)
        {
            _contentContainer.Add(element);
        }

        /// <summary>
        /// Removes all content from the section.
        /// </summary>
        public void ClearContent()
        {
            _contentContainer.Clear();
        }

        /// <summary>
        /// Gets or sets whether the section is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    UpdateExpandedState(animated: true);
                }
            }
        }

        /// <summary>
        /// Updates the section title.
        /// </summary>
        public void SetTitle(string title)
        {
            _titleLabel.text = title;
        }

        // ── Collapse/Expand Logic ────────────────────────────────────────────
        void ToggleExpanded()
        {
            _isExpanded = !_isExpanded;
            UpdateExpandedState(animated: true);
        }

        void UpdateExpandedState(bool animated)
        {
            // Update toggle icon
            _toggleIcon.text = _isExpanded ? "▼" : "▶";

            // Show/hide content
            if (_isExpanded)
            {
                _contentContainer.style.display = DisplayStyle.Flex;
                _contentContainer.style.opacity = 1f;
                _contentContainer.style.maxHeight = StyleKeyword.None;
            }
            else
            {
                _contentContainer.style.display = DisplayStyle.None;
                _contentContainer.style.opacity = 0f;
                _contentContainer.style.maxHeight = 0;
            }

            // Optional: Add smooth animation in the future
            // For now, instant show/hide is cleaner and more responsive
        }
    }
}
