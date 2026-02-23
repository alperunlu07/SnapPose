using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UICursor = UnityEngine.UIElements.Cursor;

namespace SnapPose.Editor.UI
{
    public class DiffViewPanel : VisualElement
    {
        readonly SnapPoseController _ctrl;
        VisualElement               _content;
        Label                       _summaryLabel;

        public DiffViewPanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnDiffComputed += ShowDiff;

            BuildUI();
        }

        void BuildUI()
        {
            // Section header now provided by CollapsibleSection wrapper

            _summaryLabel = new Label("Compute a diff to see bone changes.");
            _summaryLabel.AddToClassList("snap-label-secondary");
            _summaryLabel.style.paddingTop = 6;
            _summaryLabel.style.paddingBottom = 6;
            _summaryLabel.style.paddingLeft = 8;
            _summaryLabel.style.paddingRight = 8;
            Add(_summaryLabel);

            _content = new VisualElement();
            Add(_content);
        }

        public void ShowDiff(List<PoseApplicator.BoneDiff> diffs)
        {
            _content.Clear();

            if (diffs == null || diffs.Count == 0)
            {
                _summaryLabel.text = "✓ No meaningful changes detected.";
                _summaryLabel.style.color = SnapPoseStyles.SuccessColor;
                return;
            }

            _summaryLabel.text  = $"{diffs.Count} bones will change";
            _summaryLabel.style.color = SnapPoseStyles.TextSecondary;

            // Compute max magnitude for bar scaling
            float maxMag = 0f;
            foreach (var d in diffs) if (d.magnitude > maxMag) maxMag = d.magnitude;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 220;

            foreach (var diff in diffs)
            {
                bool isHigh   = diff.magnitude > maxMag * 0.6f;
                bool isMedium = diff.magnitude > maxMag * 0.2f;

                var row = new VisualElement();
                row.AddToClassList("snap-diff-row");
                row.AddToClassList(isHigh   ? "snap-diff-row--high"   :
                                   isMedium ? "snap-diff-row--medium" :
                                              "snap-diff-row--low");

                // Bone name (short)
                var boneName = diff.bonePath.Contains("/")
                    ? diff.bonePath.Substring(diff.bonePath.LastIndexOf('/') + 1)
                    : diff.bonePath;
                if (string.IsNullOrEmpty(boneName)) boneName = "(root)";

                var nameCol = new VisualElement { style = { minWidth = 100, maxWidth = 100 } };
                var nameLbl = new Label(boneName);
                nameLbl.AddToClassList(isHigh ? "snap-label-danger" : "snap-label-primary");
                nameLbl.style.overflow         = Overflow.Hidden;
                nameLbl.style.textOverflow     = TextOverflow.Ellipsis;
                nameLbl.tooltip = diff.bonePath;
                nameCol.Add(nameLbl);
                row.Add(nameCol);

                // Delta columns
                var dataCol = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };

                // Position
                if (diff.positionDelta.magnitude > 0.0001f)
                {
                    var posRow = MakeDeltaRow("Pos",
                        FormatVec(diff.positionDelta),
                        diff.positionDelta.magnitude, maxMag, SnapPoseStyles.Accent);
                    dataCol.Add(posRow);
                }

                // Rotation
                if (diff.rotationDelta.magnitude > 0.01f)
                {
                    var rotRow = MakeDeltaRow("Rot",
                        $"{diff.rotationDelta.x:F1}° {diff.rotationDelta.y:F1}° {diff.rotationDelta.z:F1}°",
                        diff.rotationDelta.magnitude * 0.01f, maxMag, SnapPoseStyles.WarningColor);
                    dataCol.Add(rotRow);
                }

                row.Add(dataCol);

                // Warning badge for large changes
                if (isHigh)
                {
                    var warn = new Label("⚠");
                    warn.AddToClassList("snap-label-warning");
                    warn.tooltip = "Large change — verify correct frame";
                    row.Add(warn);
                }

                // Click → ping bone in hierarchy
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_ctrl.TargetObject == null) return;
                    var t = _ctrl.TargetObject.transform.Find(diff.bonePath);
                    if (t != null) EditorGUIUtility.PingObject(t.gameObject);
                });
                //row.style.cursor = new StyleEnum<UICursor>(UICursor);
                row.AddManipulator(new Clickable(() =>
                {
                    if (_ctrl.TargetObject == null) return;
                    var t = _ctrl.TargetObject.transform.Find(diff.bonePath);
                    if (t != null) { Selection.activeGameObject = t.gameObject; EditorGUIUtility.PingObject(t.gameObject); }
                }));

                scroll.Add(row);
            }

            _content.Add(scroll);

            // Highlight button
            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 4, paddingBottom = 4, paddingLeft = 4, paddingRight = 4 } };
            var highlightBtn = SnapPoseStyles.MakeSecondaryButton("⬡ Select Changed Bones", () =>
            {
                if (_ctrl.TargetObject == null) return;
                var objs = new List<UnityEngine.Object>();
                foreach (var d in diffs)
                {
                    var t = _ctrl.TargetObject.transform.Find(d.bonePath);
                    if (t != null) objs.Add(t.gameObject);
                }
                Selection.objects = objs.ToArray();
            });
            highlightBtn.style.flexGrow = 1;
            row2.Add(highlightBtn);
            _content.Add(row2);
        }

        VisualElement MakeDeltaRow(string label, string value, float mag, float maxMag, Color barColor)
        {
            var row = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

            var lbl = new Label(label);
            lbl.AddToClassList("snap-label-secondary");
            lbl.style.minWidth = 25;
            row.Add(lbl);

            // Progress bar
            float pct = maxMag > 0f ? Mathf.Clamp01(mag / maxMag) : 0f;
            var barBg = new VisualElement
            {
                style =
                {
                    flexGrow        = 1,
                    height          = 5,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                    borderBottomLeftRadius = new StyleLength(3f),
                    borderTopLeftRadius    = new StyleLength(3f),
                    marginRight     = 6,
                    overflow        = Overflow.Hidden
                }
            };
            var barFill = new VisualElement
            {
                style =
                {
                    width           = Length.Percent(pct * 100f),
                    height          = 5,
                    backgroundColor = barColor
                }
            };
            barBg.Add(barFill);
            row.Add(barBg);

            var valLbl = new Label(value);
            valLbl.AddToClassList("snap-label-secondary");
            valLbl.style.minWidth = 80;
            valLbl.style.fontSize = 9;
            row.Add(valLbl);

            return row;
        }

        static string FormatVec(Vector3 v)
            => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
