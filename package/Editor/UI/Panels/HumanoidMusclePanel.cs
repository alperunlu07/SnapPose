using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// Shows all 95 Humanoid muscle values as sliders for the selected Humanoid rig.
    /// Reads from HumanPoseHandler and writes back on slider change.
    /// Only active when the target has a Humanoid Animator.
    /// </summary>
    public class HumanoidMusclePanel : VisualElement
    {
        readonly SnapPoseController _ctrl;

        // State
        HumanPoseHandler _poseHandler;
        HumanPose        _humanPose;
        bool             _handlerValid;

        // UI
        VisualElement _content;
        Label         _unavailableLabel;
        VisualElement _groupContainer;

        // Sliders keyed by muscle index
        readonly Dictionary<int, Slider> _sliders = new Dictionary<int, Slider>();
        readonly Dictionary<int, Label>  _valLabels = new Dictionary<int, Label>();

        // ── Muscle groups ──────────────────────────────────────────────────────
        // Unity's HumanTrait.MuscleName array is flat (0-94).
        // We group by common name prefixes for better readability.
        static readonly (string groupName, string[] prefixes)[] Groups =
        {
            ("Spine / Body",      new[] { "Spine", "Chest", "UpperChest", "Neck", "Shoulder" }),
            ("Head",              new[] { "Head", "Jaw", "Left Eye", "Right Eye" }),
            ("Left Arm",          new[] { "Left Upper Arm", "Left Lower Arm", "Left Hand" }),
            ("Right Arm",         new[] { "Right Upper Arm", "Right Lower Arm", "Right Hand" }),
            ("Left Leg",          new[] { "Left Upper Leg", "Left Lower Leg", "Left Foot", "Left Toes" }),
            ("Right Leg",         new[] { "Right Upper Leg", "Right Lower Leg", "Right Foot", "Right Toes" }),
            ("Left Fingers",      new[] { "Left Thumb", "Left Index", "Left Middle", "Left Ring", "Left Little" }),
            ("Right Fingers",     new[] { "Right Thumb", "Right Index", "Right Middle", "Right Ring", "Right Little" }),
        };

        // ── Constructor ───────────────────────────────────────────────────────
        public HumanoidMusclePanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnStateChanged += OnStateChanged;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _ctrl.OnStateChanged -= OnStateChanged;
                DisposeHandler();
            });

            BuildUI();
            OnStateChanged();
        }

        // ── UI ────────────────────────────────────────────────────────────────
        void BuildUI()
        {
            _unavailableLabel = new Label("Select a Humanoid rig to edit muscle values.");
            _unavailableLabel.AddToClassList("snap-label-secondary");
            _unavailableLabel.style.paddingTop    = 8;
            _unavailableLabel.style.paddingBottom = 8;
            _unavailableLabel.style.paddingLeft   = 8;
            _unavailableLabel.style.whiteSpace    = WhiteSpace.Normal;
            Add(_unavailableLabel);

            _content = new VisualElement();
            Add(_content);

            // Toolbar: Reset All + Apply buttons
            var toolbar = new VisualElement
                { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            var resetBtn = SnapPoseStyles.MakeSecondaryButton("↺ Reset All", ResetAllMuscles);
            resetBtn.style.flexGrow   = 1;
            resetBtn.style.marginRight = 4;
            toolbar.Add(resetBtn);

            var applyBtn = SnapPoseStyles.MakePrimaryButton("⚡ Apply Muscles", ApplyMusclesToRig);
            applyBtn.style.flexGrow = 1;
            toolbar.Add(applyBtn);
            _content.Add(toolbar);

            _groupContainer = new VisualElement();
            _content.Add(_groupContainer);
        }

        // ── State ─────────────────────────────────────────────────────────────
        void OnStateChanged()
        {
            bool isHumanoid = _ctrl.TargetObject != null &&
                              PoseSampler.DetectRigType(_ctrl.TargetObject) == RigType.Humanoid;

            _unavailableLabel.style.display = isHumanoid ? DisplayStyle.None  : DisplayStyle.Flex;
            _content.style.display          = isHumanoid ? DisplayStyle.Flex  : DisplayStyle.None;

            if (!isHumanoid)
            {
                DisposeHandler();
                return;
            }

            RebuildHandler();
            RebuildSliders();
        }

        void RebuildHandler()
        {
            DisposeHandler();
            var animator = _ctrl.TargetObject?.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            _poseHandler = new HumanPoseHandler(animator.avatar, _ctrl.TargetObject.transform);
            _poseHandler.GetHumanPose(ref _humanPose);
            _handlerValid = true;
        }

        void DisposeHandler()
        {
            if (_handlerValid)
            {
                _poseHandler.Dispose();
                _handlerValid = false;
            }
        }

        // ── Slider construction ───────────────────────────────────────────────
        void RebuildSliders()
        {
            _groupContainer.Clear();
            _sliders.Clear();
            _valLabels.Clear();

            if (!_handlerValid) return;

            int muscleCount = HumanTrait.MuscleCount;

            // Build index→group mapping
            var assigned = new bool[muscleCount];

            foreach (var (groupName, prefixes) in Groups)
            {
                // Collect muscle indices for this group
                var groupIndices = new List<int>();
                for (int i = 0; i < muscleCount; i++)
                {
                    if (assigned[i]) continue;
                    string name = HumanTrait.MuscleName[i];
                    foreach (var prefix in prefixes)
                    {
                        if (name.StartsWith(prefix))
                        {
                            groupIndices.Add(i);
                            assigned[i] = true;
                            break;
                        }
                    }
                }

                if (groupIndices.Count == 0) continue;

                var group = BuildMuscleGroup(groupName, groupIndices);
                _groupContainer.Add(group);
            }

            // Remaining (uncategorized)
            var remaining = new List<int>();
            for (int i = 0; i < muscleCount; i++)
                if (!assigned[i]) remaining.Add(i);

            if (remaining.Count > 0)
                _groupContainer.Add(BuildMuscleGroup("Other", remaining));
        }

        VisualElement BuildMuscleGroup(string groupName, List<int> muscleIndices)
        {
            var group = new VisualElement();
            group.AddToClassList("snap-muscle-group");

            // Collapsible header
            bool collapsed = false;
            var header = new VisualElement();
            header.AddToClassList("snap-muscle-group-header");

            var arrow = new Label("▼");
            arrow.AddToClassList("snap-label-secondary");
            arrow.style.marginRight = 6;
            arrow.style.fontSize    = 9;
            header.Add(arrow);

            var title = new Label(groupName);
            title.AddToClassList("snap-label-accent");
            title.style.fontSize = 10;
            header.Add(title);

            var countBadge = SnapPoseStyles.MakeBadge($"{muscleIndices.Count}", false);
            countBadge.style.marginLeft = 6;
            header.Add(countBadge);

            group.Add(header);

            // Slider container (toggleable)
            var sliderContainer = new VisualElement();
            sliderContainer.AddToClassList("snap-muscle-slider-container");
            group.Add(sliderContainer);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                collapsed = !collapsed;
                sliderContainer.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.text = collapsed ? "▶" : "▼";
            });

            // Build sliders
            foreach (var idx in muscleIndices)
            {
                var row = BuildMuscleRow(idx);
                sliderContainer.Add(row);
            }

            return group;
        }

        VisualElement BuildMuscleRow(int muscleIdx)
        {
            string muscleName = HumanTrait.MuscleName[muscleIdx];
            float  currentVal = _humanPose.muscles != null && muscleIdx < _humanPose.muscles.Length
                ? _humanPose.muscles[muscleIdx]
                : 0f;

            // Short name: strip group prefix for display
            string shortName = muscleName;
            foreach (var (_, prefixes) in Groups)
            {
                foreach (var prefix in prefixes)
                {
                    if (shortName.StartsWith(prefix + " "))
                    {
                        shortName = shortName.Substring(prefix.Length + 1);
                        break;
                    }
                }
            }

            var row = new VisualElement();
            row.AddToClassList("snap-muscle-row");

            var nameLbl = new Label(shortName);
            nameLbl.AddToClassList("snap-label-secondary");
            nameLbl.style.minWidth     = 90;
            nameLbl.style.maxWidth     = 90;
            nameLbl.style.overflow     = Overflow.Hidden;
            nameLbl.style.textOverflow = TextOverflow.Ellipsis;
            nameLbl.tooltip            = muscleName;
            row.Add(nameLbl);

            var slider = new Slider(-1f, 1f) { value = currentVal };
            slider.style.flexGrow = 1;
            _sliders[muscleIdx] = slider;

            var valLbl = new Label($"{currentVal:F2}");
            valLbl.AddToClassList("snap-label-accent");
            valLbl.style.minWidth  = 34;
            valLbl.style.unityTextAlign = TextAnchor.MiddleRight;
            _valLabels[muscleIdx] = valLbl;

            slider.RegisterValueChangedCallback(e =>
            {
                valLbl.text = $"{e.newValue:F2}";
                // Write to _humanPose immediately for live preview
                if (_humanPose.muscles != null && muscleIdx < _humanPose.muscles.Length)
                    _humanPose.muscles[muscleIdx] = e.newValue;
            });

            row.Add(slider);
            row.Add(valLbl);

            // Zero button
            var zeroBtn = SnapPoseStyles.MakeIconButton("0", () =>
            {
                slider.value = 0f;
            }, "Reset to zero");
            zeroBtn.style.flexShrink = 0;
            row.Add(zeroBtn);

            return row;
        }

        // ── Actions ───────────────────────────────────────────────────────────
        void ResetAllMuscles()
        {
            if (!_handlerValid) return;
            for (int i = 0; i < _humanPose.muscles.Length; i++)
            {
                _humanPose.muscles[i] = 0f;
                if (_sliders.TryGetValue(i, out var s))  s.SetValueWithoutNotify(0f);
                if (_valLabels.TryGetValue(i, out var l)) l.text = "0.00";
            }
        }

        void ApplyMusclesToRig()
        {
            if (!_handlerValid || _ctrl.TargetObject == null) return;

            var transforms = _ctrl.TargetObject.GetComponentsInChildren<Transform>(true);
            Undo.RecordObjects(transforms, "Apply Muscle Pose");

            _poseHandler.SetHumanPose(ref _humanPose);

            EditorUtility.SetDirty(_ctrl.TargetObject);
        }

        /// <summary>
        /// Reads the current rig transforms into _humanPose and syncs all sliders.
        /// Call this after external pose changes (e.g. after Apply Pose) to keep UI in sync.
        /// </summary>
        public void SyncFromRig()
        {
            if (!_handlerValid) return;
            _poseHandler.GetHumanPose(ref _humanPose);

            if (_humanPose.muscles == null) return;
            for (int i = 0; i < _humanPose.muscles.Length; i++)
            {
                float v = _humanPose.muscles[i];
                if (_sliders.TryGetValue(i, out var s))  s.SetValueWithoutNotify(v);
                if (_valLabels.TryGetValue(i, out var l)) l.text = $"{v:F2}";
            }
        }
    }
}
