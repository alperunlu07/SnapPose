using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// A self-contained VisualElement that edits a BoneMask in place.
    /// Shows a mode dropdown; when Include/Exclude is selected, reveals a
    /// searchable bone list with checkboxes populated from the current target.
    /// </summary>
    public class BoneMaskEditorPanel : VisualElement
    {
        // ── State ─────────────────────────────────────────────────────────────
        readonly SnapPoseController _ctrl;
        BoneMask                    _mask;

        // UI references
        DropdownField   _modeDropdown;
        VisualElement   _listContainer;
        TextField       _searchField;
        VisualElement   _boneListView;
        Label           _emptyLabel;
        Label           _summaryLabel;

        // Bone display cache
        List<string> _allBonePaths = new List<string>();
        string       _filterText   = "";

        // Collapse/expand state for hierarchy
        HashSet<string> _expandedBones = new HashSet<string>();

        // ── Constructor ───────────────────────────────────────────────────────
        public BoneMaskEditorPanel(SnapPoseController ctrl, BoneMask mask)
        {
            _ctrl = ctrl;
            _mask = mask;

            _ctrl.OnStateChanged += OnTargetChanged;

            // Unsubscribe when element is removed from the panel hierarchy
            RegisterCallback<DetachFromPanelEvent>(_ => _ctrl.OnStateChanged -= OnTargetChanged);

            BuildUI();
            RefreshBoneList();
        }

        /// <summary>Swap the mask this panel is editing (e.g. when layer changes).</summary>
        public void SetMask(BoneMask mask)
        {
            _mask = mask;
            SyncModeDropdown();
            RefreshBoneList();
        }

        // ── UI Construction ───────────────────────────────────────────────────
        void BuildUI()
        {
            // Mode row
            var modeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            _modeDropdown = new DropdownField(
                new List<string> { "All Bones", "Include List", "Exclude List" },
                (int)_mask.mode);
            _modeDropdown.style.flexGrow = 1;
            _modeDropdown.RegisterValueChangedCallback(e =>
            {
                _mask.mode = (BoneMask.MaskMode)_modeDropdown.index;
                UpdateListVisibility();
            });
            modeRow.Add(_modeDropdown);

            // Summary badge: "3 bones"
            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("snap-mask-summary");
            modeRow.Add(_summaryLabel);

            Add(modeRow);

            // ── Bone list (hidden when AllBones) ──────────────────────────────
            _listContainer = new VisualElement();
            _listContainer.AddToClassList("snap-mask-list-container");
            Add(_listContainer);

            // Search row
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };

            const string placeholder = "Filter bones…";
            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            // Placeholder simulation: show grey hint when empty and unfocused
            _searchField.SetValueWithoutNotify(placeholder);
            _searchField.style.color = new StyleColor(SnapPoseStyles.TextDisabled);
            _searchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (_searchField.value == placeholder)
                {
                    _searchField.SetValueWithoutNotify("");
                    _searchField.style.color = new StyleColor(SnapPoseStyles.TextPrimary);
                }
            });
            _searchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(_searchField.value))
                {
                    _searchField.SetValueWithoutNotify(placeholder);
                    _searchField.style.color = new StyleColor(SnapPoseStyles.TextDisabled);
                }
            });
            _searchField.RegisterValueChangedCallback(e =>
            {
                _filterText = e.newValue == placeholder ? "" : e.newValue;
                RebuildBoneRows();
            });
            searchRow.Add(_searchField);

            var clearSearchBtn = SnapPoseStyles.MakeIconButton("✕", () =>
            {
                _searchField.SetValueWithoutNotify(placeholder);
                _searchField.style.color = new StyleColor(SnapPoseStyles.TextDisabled);
                _filterText = "";
                RebuildBoneRows();
            }, "Clear filter");
            searchRow.Add(clearSearchBtn);
            _listContainer.Add(searchRow);

            // Quick-action row
            var quickRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };

            var selectAllBtn = SnapPoseStyles.MakeSecondaryButton("Select All", () =>
            {
                foreach (var path in _allBonePaths)
                    if (!_mask.bonePaths.Contains(path))
                        _mask.bonePaths.Add(path);
                RebuildBoneRows();
                UpdateSummary();
            });
            selectAllBtn.style.flexGrow = 1;
            selectAllBtn.style.marginRight = 4;
            quickRow.Add(selectAllBtn);

            var clearAllBtn = SnapPoseStyles.MakeSecondaryButton("Clear All", () =>
            {
                _mask.bonePaths.Clear();
                RebuildBoneRows();
                UpdateSummary();
            });
            clearAllBtn.style.flexGrow = 1;
            quickRow.Add(clearAllBtn);
            _listContainer.Add(quickRow);

            // Hint label for features
            var hintLabel = new Label("💡 Tips: Click ▶ to expand/collapse • Alt+Click ▶ for recursive • Alt+Click ☐ to apply to children");
            hintLabel.AddToClassList("snap-label-secondary");
            hintLabel.style.fontSize = 9;
            hintLabel.style.paddingTop = 2;
            hintLabel.style.paddingBottom = 4;
            hintLabel.style.paddingLeft = 4;
            hintLabel.style.whiteSpace = WhiteSpace.Normal;
            hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.5f)); // Subtle yellow tint
            _listContainer.Add(hintLabel);

            // Scrollable bone list
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 180;
            scroll.style.minHeight = 60;
            scroll.AddToClassList("snap-mask-scroll");
            _listContainer.Add(scroll);

            _boneListView = new VisualElement();
            scroll.Add(_boneListView);

            _emptyLabel = new Label("No target selected — add a character to the Workspace first.");
            _emptyLabel.AddToClassList("snap-label-secondary");
            _emptyLabel.style.paddingTop    = 8;
            _emptyLabel.style.paddingBottom = 8;
            _emptyLabel.style.paddingLeft   = 8;
            _emptyLabel.style.paddingRight  = 8;
            _emptyLabel.style.whiteSpace    = WhiteSpace.Normal;
            _listContainer.Add(_emptyLabel);

            UpdateListVisibility();
        }

        // ── State sync ────────────────────────────────────────────────────────
        void OnTargetChanged() => RefreshBoneList();

        void SyncModeDropdown()
        {
            _modeDropdown.SetValueWithoutNotify(
                _mask.mode == BoneMask.MaskMode.AllBones    ? "All Bones"    :
                _mask.mode == BoneMask.MaskMode.IncludeList ? "Include List" :
                                                              "Exclude List");
            UpdateListVisibility();
        }

        void UpdateListVisibility()
        {
            bool showList = _mask.mode != BoneMask.MaskMode.AllBones;
            _listContainer.style.display = showList ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateSummary();
        }

        // ── Bone list population ──────────────────────────────────────────────
        void RefreshBoneList()
        {
            _allBonePaths.Clear();
            _expandedBones.Clear(); // Reset expansion state

            if (_ctrl.TargetObject != null)
            {
                _allBonePaths = PoseSampler.GetAllBonePaths(_ctrl.TargetObject);

                // Expand all bones by default for initial load
                foreach (var path in _allBonePaths)
                {
                    if (HasChildren(path))
                        _expandedBones.Add(path);
                }
            }

            RebuildBoneRows();
            UpdateSummary();
        }

        void RebuildBoneRows()
        {
            _boneListView.Clear();

            bool hasTarget = _ctrl.TargetObject != null && _allBonePaths.Count > 0;
            _emptyLabel.style.display = hasTarget ? DisplayStyle.None : DisplayStyle.Flex;

            if (!hasTarget) return;

            // Filter
            string filter = _filterText.Trim().ToLowerInvariant();

            foreach (var path in _allBonePaths)
            {
                // Check if this bone should be visible based on parent expansion state
                if (!string.IsNullOrEmpty(filter))
                {
                    // When filtering, show all matching bones (ignore collapse state)
                }
                else if (!IsBoneVisible(path))
                {
                    // Parent is collapsed, skip this bone
                    continue;
                }

                // Short display name
                var displayName = path.Contains("/")
                    ? path.Substring(path.LastIndexOf('/') + 1)
                    : path;
                if (string.IsNullOrEmpty(displayName)) displayName = "(root)";

                if (!string.IsNullOrEmpty(filter) &&
                    !displayName.ToLowerInvariant().Contains(filter) &&
                    !path.ToLowerInvariant().Contains(filter))
                    continue;

                bool isChecked = _mask.bonePaths.Contains(path);
                var row = MakeBoneRow(path, displayName, isChecked);
                _boneListView.Add(row);
            }
        }

        VisualElement MakeBoneRow(string path, string displayName, bool isChecked)
        {
            var row = new VisualElement();
            row.AddToClassList("snap-mask-bone-row");

            // Depth indent — count "/" in path
            int depth = 0;
            foreach (char c in path) if (c == '/') depth++;
            if (depth > 0)
            {
                var indent = new VisualElement
                {
                    style = { width = depth * 12, flexShrink = 0 }
                };
                row.Add(indent);
            }

            // Collapse/Expand arrow (for bones with children)
            bool hasChildren = HasChildren(path);
            if (hasChildren)
            {
                bool isExpanded = _expandedBones.Contains(path);
                var foldout = new Label(isExpanded ? "▼" : "▶");
                foldout.style.width = 14;
                foldout.style.minWidth = 14;
                foldout.style.fontSize = 9;
                foldout.style.color = new StyleColor(SnapPoseStyles.TextSecondary);
                foldout.style.unityTextAlign = TextAnchor.MiddleCenter;
                foldout.tooltip = "Click to expand/collapse • Alt+Click for recursive";

                // Click handler for collapse/expand
                foldout.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();

                    if (evt.altKey)
                    {
                        // Alt+Click: Recursive collapse/expand
                        bool newExpandedState = !_expandedBones.Contains(path);
                        SetExpandedRecursive(path, newExpandedState);
                    }
                    else
                    {
                        // Normal click: Toggle this bone only
                        if (_expandedBones.Contains(path))
                            _expandedBones.Remove(path);
                        else
                            _expandedBones.Add(path);
                    }

                    RebuildBoneRows();
                });

                row.Add(foldout);
            }
            else
            {
                // Spacer for bones without children (to align with parent bones that have arrows)
                var spacer = new VisualElement { style = { width = 14, minWidth = 14, flexShrink = 0 } };
                row.Add(spacer);
            }

            // Checkbox
            var toggle = new Toggle { value = isChecked };
            toggle.style.marginRight = 4;
            toggle.tooltip = "Alt+Click to apply to all children";

            // Track if this is an Alt+click operation to prevent recursion
            bool isProcessingAltClick = false;

            toggle.RegisterValueChangedCallback(e =>
            {
                if (isProcessingAltClick) return; // Prevent recursive updates during Alt+click

                bool newValue = e.newValue;

                // Apply to current bone
                if (newValue)
                {
                    if (!_mask.bonePaths.Contains(path))
                        _mask.bonePaths.Add(path);
                }
                else
                {
                    _mask.bonePaths.Remove(path);
                }

                UpdateSummary();
            });

            // Alt+Click handler for applying to all children
            toggle.RegisterCallback<ClickEvent>(evt =>
            {
                if (!evt.altKey) return; // Only process if Alt is held
                evt.StopPropagation();

                bool targetValue = toggle.value;
                isProcessingAltClick = true;

                // Get all child bones
                var children = GetAllChildBones(path);

                // Apply the same state to all children
                foreach (var childPath in children)
                {
                    if (targetValue)
                    {
                        if (!_mask.bonePaths.Contains(childPath))
                            _mask.bonePaths.Add(childPath);
                    }
                    else
                    {
                        _mask.bonePaths.Remove(childPath);
                    }
                }

                isProcessingAltClick = false;

                // Rebuild the UI to reflect changes
                RebuildBoneRows();
                UpdateSummary();
            });

            row.Add(toggle);

            // Bone name label
            var lbl = new Label(displayName);
            lbl.AddToClassList(isChecked ? "snap-label-accent" : "snap-label-primary");
            lbl.tooltip = path;
            lbl.style.flexGrow = 1;
            lbl.style.overflow     = Overflow.Hidden;
            lbl.style.textOverflow = TextOverflow.Ellipsis;

            // Keep label style in sync with toggle
            toggle.RegisterValueChangedCallback(e =>
                lbl.style.color = e.newValue
                    ? new StyleColor(SnapPoseStyles.Accent)
                    : new StyleColor(SnapPoseStyles.TextPrimary));

            row.Add(lbl);

            // Ping bone on row click (not on toggle or foldout)
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == toggle || evt.target is Label l && l == lbl && evt.clickCount == 2)
                    return;
                if (_ctrl.TargetObject == null) return;
                var t = _ctrl.TargetObject.transform.Find(path);
                if (t != null) EditorGUIUtility.PingObject(t.gameObject);
            });

            return row;
        }

        void UpdateSummary()
        {
            if (_mask.mode == BoneMask.MaskMode.AllBones)
            {
                _summaryLabel.text = "";
                _summaryLabel.style.display = DisplayStyle.None;
                return;
            }

            _summaryLabel.style.display = DisplayStyle.Flex;
            int count = _mask.bonePaths.Count;
            string modeShort = _mask.mode == BoneMask.MaskMode.IncludeList ? "incl" : "excl";
            _summaryLabel.text = $"{count} {modeShort}";
            _summaryLabel.style.color = count > 0
                ? new StyleColor(SnapPoseStyles.Accent)
                : new StyleColor(SnapPoseStyles.TextDisabled);
        }

        /// <summary>
        /// Gets all child bone paths for a given parent bone path (all descendants).
        /// </summary>
        List<string> GetAllChildBones(string parentPath)
        {
            var children = new List<string>();

            // A bone is a child if its path starts with "parentPath/"
            string prefix = string.IsNullOrEmpty(parentPath) ? "" : parentPath + "/";

            foreach (var bonePath in _allBonePaths)
            {
                // Skip the parent itself
                if (bonePath == parentPath) continue;

                // Check if this bone is a descendant
                if (string.IsNullOrEmpty(parentPath))
                {
                    // If parent is root (empty string), all bones are children
                    children.Add(bonePath);
                }
                else if (bonePath.StartsWith(prefix))
                {
                    // This bone is a child or descendant
                    children.Add(bonePath);
                }
            }

            return children;
        }

        /// <summary>
        /// Checks if a bone has any direct children.
        /// </summary>
        bool HasChildren(string parentPath)
        {
            string prefix = string.IsNullOrEmpty(parentPath) ? "" : parentPath + "/";

            foreach (var bonePath in _allBonePaths)
            {
                if (bonePath == parentPath) continue;

                if (string.IsNullOrEmpty(parentPath))
                {
                    // Root level - check if there are any bones with no "/"
                    if (!bonePath.Contains("/"))
                        return true;
                }
                else if (bonePath.StartsWith(prefix))
                {
                    // Check if this is a direct child (not a grandchild)
                    string remainder = bonePath.Substring(prefix.Length);
                    if (!remainder.Contains("/"))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a bone should be visible based on parent expansion states.
        /// </summary>
        bool IsBoneVisible(string bonePath)
        {
            // Root bones are always visible
            if (string.IsNullOrEmpty(bonePath) || !bonePath.Contains("/"))
                return true;

            // Check each parent level
            var parts = bonePath.Split('/');
            var currentPath = "";

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (i > 0) currentPath += "/";
                currentPath += parts[i];

                // If any parent is collapsed, this bone is not visible
                if (!_expandedBones.Contains(currentPath))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Recursively expands or collapses a bone and all its descendants.
        /// </summary>
        void SetExpandedRecursive(string parentPath, bool expanded)
        {
            // Set state for parent
            if (expanded)
                _expandedBones.Add(parentPath);
            else
                _expandedBones.Remove(parentPath);

            // Get all descendants and set their state too
            var children = GetAllChildBones(parentPath);
            foreach (var childPath in children)
            {
                if (HasChildren(childPath))
                {
                    if (expanded)
                        _expandedBones.Add(childPath);
                    else
                        _expandedBones.Remove(childPath);
                }
            }
        }
    }
}
