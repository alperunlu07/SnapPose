using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// Replaces the old simple Mirror section in InspectorPanel.
    /// Shows: axis selector, auto-detect button, editable pair list, Apply Mirror button.
    /// </summary>
    public class MirrorSetupPanel : VisualElement
    {
        readonly SnapPoseController _ctrl;

        // State
        BoneMirrorMap           _map  = new BoneMirrorMap();
        PoseApplicator.MirrorAxis _axis = PoseApplicator.MirrorAxis.X;

        // UI
        VisualElement _pairList;
        Label         _statusLabel;
        DropdownField _axisDropdown;
        Button        _applyBtn;
        Slider        _blendSlider;
        Label         _blendLabel;

        public MirrorSetupPanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnStateChanged += OnStateChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => _ctrl.OnStateChanged -= OnStateChanged);
            BuildUI();
        }

        // ── UI ────────────────────────────────────────────────────────────────
        void BuildUI()
        {
            // ── Controls row ─────────────────────────────────────────────────
            var controlRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };

            var axisLbl = new Label("Axis");
            axisLbl.AddToClassList("snap-label-secondary");
            axisLbl.style.minWidth = 30;
            controlRow.Add(axisLbl);

            _axisDropdown = new DropdownField(new List<string> { "X", "Y", "Z" }, 0);
            _axisDropdown.style.width    = 60;
            _axisDropdown.style.flexShrink = 0;
            _axisDropdown.RegisterValueChangedCallback(e =>
                _axis = (PoseApplicator.MirrorAxis)_axisDropdown.index);
            controlRow.Add(_axisDropdown);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            controlRow.Add(spacer);

            var detectBtn = SnapPoseStyles.MakeSecondaryButton("⟳ Auto-Detect Pairs", AutoDetect);
            controlRow.Add(detectBtn);
            Add(controlRow);

            // ── Blend row ─────────────────────────────────────────────────────
            var blendRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };

            var blendLbl = new Label("Blend");
            blendLbl.AddToClassList("snap-label-secondary");
            blendLbl.style.minWidth = 36;
            blendRow.Add(blendLbl);

            _blendSlider = new Slider(0f, 1f) { value = 1f };
            _blendSlider.style.flexGrow = 1;
            blendRow.Add(_blendSlider);

            _blendLabel = new Label("100%");
            _blendLabel.AddToClassList("snap-label-accent");
            _blendLabel.style.minWidth = 34;
            _blendSlider.RegisterValueChangedCallback(e =>
                _blendLabel.text = $"{(int)(e.newValue * 100)}%");
            blendRow.Add(_blendLabel);
            Add(blendRow);

            // ── Status / pair list ────────────────────────────────────────────
            _statusLabel = new Label("Click 'Auto-Detect Pairs' to find L/R bone pairs.");
            _statusLabel.AddToClassList("snap-label-secondary");
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 4;
            Add(_statusLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 160;
            scroll.style.minHeight = 40;
            scroll.AddToClassList("snap-mask-scroll");
            Add(scroll);

            _pairList = new VisualElement();
            scroll.Add(_pairList);

            // ── Apply button ──────────────────────────────────────────────────
            _applyBtn = SnapPoseStyles.MakePrimaryButton("⟺ Apply Mirror", ApplyMirror);
            _applyBtn.style.marginTop = 6;
            Add(_applyBtn);

            RefreshApplyButton();
        }

        // ── Auto-detect ───────────────────────────────────────────────────────
        void AutoDetect()
        {
            if (_ctrl.TargetObject == null)
            {
                _statusLabel.text  = "No target — select a character first.";
                _statusLabel.style.color = new StyleColor(SnapPoseStyles.WarningColor);
                return;
            }

            _map = MirrorPairDetector.Detect(_ctrl.TargetObject);

            if (_map.pairs.Count == 0)
            {
                _statusLabel.text  = "No L/R pairs found. Check naming (Left/Right, _L/_R, L_/R_).";
                _statusLabel.style.color = new StyleColor(SnapPoseStyles.WarningColor);
            }
            else
            {
                _statusLabel.text  = $"Found {_map.pairs.Count} bone pair(s). Toggle to enable/disable individual pairs.";
                _statusLabel.style.color = new StyleColor(SnapPoseStyles.SuccessColor);
            }

            RebuildPairList();
            RefreshApplyButton();
        }

        // ── Pair list UI ──────────────────────────────────────────────────────
        void RebuildPairList()
        {
            _pairList.Clear();

            if (_map.pairs.Count == 0) return;

            // Header row
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };
            void AddHeaderCell(string txt, int minW)
            {
                var l = new Label(txt);
                l.AddToClassList("snap-label-secondary");
                l.style.minWidth = minW;
                l.style.flexGrow = 1;
                header.Add(l);
            }
            AddHeaderCell("", 20);           // toggle column
            AddHeaderCell("Source (L)", 80);
            AddHeaderCell("→", 14);
            AddHeaderCell("Target (R)", 80);
            _pairList.Add(header);
            _pairList.Add(SnapPoseStyles.MakeSeparator());

            foreach (var pair in _map.pairs)
            {
                var row = BuildPairRow(pair);
                _pairList.Add(row);
            }
        }

        VisualElement BuildPairRow(BoneMirrorMap.BonePair pair)
        {
            var row = new VisualElement();
            row.AddToClassList("snap-mask-bone-row");

            // Enable toggle
            var toggle = new Toggle { value = pair.enabled };
            toggle.style.marginRight = 4;
            toggle.style.flexShrink  = 0;
            toggle.RegisterValueChangedCallback(e => pair.enabled = e.newValue);
            row.Add(toggle);

            // Source field
            var srcField = new TextField { value = pair.source };
            srcField.style.flexGrow  = 1;
            srcField.style.minWidth  = 60;
            srcField.RegisterValueChangedCallback(e => pair.source = e.newValue);
            row.Add(srcField);

            var arrow = new Label("→");
            arrow.AddToClassList("snap-label-secondary");
            arrow.style.marginLeft  = 4;
            arrow.style.marginRight = 4;
            arrow.style.flexShrink  = 0;
            row.Add(arrow);

            // Target field
            var tgtField = new TextField { value = pair.target };
            tgtField.style.flexGrow = 1;
            tgtField.style.minWidth = 60;
            tgtField.RegisterValueChangedCallback(e => pair.target = e.newValue);
            row.Add(tgtField);

            // Swap button
            var swapBtn = SnapPoseStyles.MakeIconButton("⇄", () =>
            {
                var tmp    = pair.source;
                pair.source = pair.target;
                pair.target = tmp;
                srcField.SetValueWithoutNotify(pair.source);
                tgtField.SetValueWithoutNotify(pair.target);
            }, "Swap source and target");
            swapBtn.style.flexShrink = 0;
            row.Add(swapBtn);

            // Ping source in hierarchy on double-click
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount != 2 || _ctrl.TargetObject == null) return;
                var t = _ctrl.TargetObject.transform.Find(pair.source);
                if (t != null) EditorGUIUtility.PingObject(t.gameObject);
            });

            return row;
        }

        // ── Apply ─────────────────────────────────────────────────────────────
        void ApplyMirror()
        {
            if (_ctrl.LastSampledPose == null || _ctrl.TargetObject == null) return;

            PoseApplicator.MirrorPoseWithMap(
                _ctrl.TargetObject,
                _ctrl.LastSampledPose,
                _map,
                _axis,
                _blendSlider.value,
                ApplyMode.Permanent);
        }

        // ── State ─────────────────────────────────────────────────────────────
        void OnStateChanged()
        {
            RefreshApplyButton();

            // If target changed, clear stale pairs
            if (_ctrl.TargetObject == null)
            {
                _map = new BoneMirrorMap();
                _pairList.Clear();
                _statusLabel.text  = "Click 'Auto-Detect Pairs' to find L/R bone pairs.";
                _statusLabel.style.color = new StyleColor(SnapPoseStyles.TextSecondary);
            }
        }

        void RefreshApplyButton()
        {
            bool canApply = _ctrl.TargetObject != null &&
                            _ctrl.LastSampledPose != null &&
                            _map.pairs.Count > 0;
            _applyBtn.SetEnabled(canApply);

            if (_ctrl.LastSampledPose == null && _ctrl.TargetObject != null)
            {
                _applyBtn.tooltip = "Sample a pose first (📷 Sample button)";
            }
            else
            {
                _applyBtn.tooltip = "";
            }
        }
    }
}
