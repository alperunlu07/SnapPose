using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// Right-hand inspector panel. Contains source (clip/timeline), apply controls,
    /// pose stack, diff view, and history.
    /// </summary>
    public class InspectorPanel : VisualElement
    {
        readonly SnapPoseController _ctrl;

        // Sub-panels
        TimelineScrubber      _timeline;
        PoseStackPanel        _stackPanel;
        DiffViewPanel         _diffPanel;
        HistoryPanel          _historyPanel;
        HumanoidMusclePanel   _musclePanel;

        // Controls
        ObjectField      _clipField;
        Slider           _blendSlider;
        Label            _blendLabel;
        Label            _targetLabel;
        RadioButton      _radioPermanent;
        RadioButton      _radioPreview;
        Button           _previewBtn;
        Button           _applyBtn;
        Button           _sampleBtn;
        Button           _saveBtn;
        Label            _modeBadge;

        // Mirror panel
        MirrorSetupPanel _mirrorPanel;

        // Apply state
        ApplyMode          _applyMode = ApplyMode.Permanent;
        BoneMask           _boneMask  = new BoneMask();
        BoneMaskEditorPanel _maskEditor;

        public InspectorPanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnStateChanged   += OnStateChanged;
            _ctrl.OnPreviewChanged += OnPreviewChanged;

            style.flexGrow      = 1;
            style.flexShrink    = 1;
            style.flexDirection = FlexDirection.Column;
            style.minHeight     = 0;   // allow panel to shrink, giving space to action bar

            BuildUI();
            OnStateChanged();
        }

        void BuildUI()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow    = 1;
            scroll.style.flexShrink  = 1;
            scroll.style.minHeight   = 0;   // allow shrink below content height
            Add(scroll);

            var inner = new VisualElement();
            // No flexGrow here — let content determine height naturally
            scroll.Add(inner);

            // ── Target Info ────────────────────────────────────────────────
            inner.Add(SnapPoseStyles.MakeSectionHeader("TARGET", "◎"));
            var targetCard = SnapPoseStyles.MakeCard();
            _targetLabel = new Label("No object selected");
            _targetLabel.AddToClassList("snap-label-secondary");
            _targetLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _targetLabel.style.paddingTop   = 8;
            _targetLabel.style.paddingBottom = 8;
            _targetLabel.style.paddingLeft   = 8;
            _targetLabel.style.paddingRight  = 8;
            targetCard.Add(_targetLabel);
            inner.Add(targetCard);

            // ── Source ─────────────────────────────────────────────────────
            inner.Add(SnapPoseStyles.MakeSectionHeader("SOURCE", "🎬"));
            var sourceCard = SnapPoseStyles.MakeCard();

            _clipField = new ObjectField("Clip")
            {
                objectType        = typeof(AnimationClip),
                allowSceneObjects = false
            };
            _clipField.RegisterValueChangedCallback(e =>
            {
                _ctrl.SetClip(e.newValue as AnimationClip);
                _timeline.SetClip(e.newValue as AnimationClip);
            });
            sourceCard.Add(_clipField);
            inner.Add(sourceCard);

            // ── Timeline ───────────────────────────────────────────────────
            var timelineCard = SnapPoseStyles.MakeCard();
            _timeline = new TimelineScrubber();
            _timeline.BindController(_ctrl);           // playback polling via UIToolkit scheduler
            _timeline.OnFrameChanged += frame =>
            {
                _ctrl.SetFrame(frame);
                RefreshPreviewButton();
            };
            _timeline.OnTimeChanged      += _ => { };
            _timeline.OnPlayPauseClicked += () =>
            {
                if (_ctrl.IsPlaying) _ctrl.StopPlayback();
                else                 _ctrl.StartPlayback(_boneMask);
            };
            timelineCard.Add(_timeline);

            // Preview button row
            var previewRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };
            _previewBtn = SnapPoseStyles.MakePrimaryButton("▶ Start Preview", TogglePreview);
            _previewBtn.style.flexGrow = 1;
            previewRow.Add(_previewBtn);

            // Speed dropdown next to preview button
            var speedDrop = new UnityEngine.UIElements.DropdownField(
                new System.Collections.Generic.List<string> { "0.25×", "0.5×", "1×", "2×" }, 2);
            speedDrop.style.width    = 58;
            speedDrop.style.flexShrink = 0;
            speedDrop.tooltip = "Playback speed";
            speedDrop.RegisterValueChangedCallback(e =>
            {
                _ctrl.PlaybackSpeed = e.newValue switch
                {
                    "0.25×" => 0.25f,
                    "0.5×"  => 0.5f,
                    "2×"    => 2.0f,
                    _       => 1.0f
                };
            });
            previewRow.Add(speedDrop);
            timelineCard.Add(previewRow);
            inner.Add(timelineCard);

            // ── Blend ──────────────────────────────────────────────────────
            inner.Add(SnapPoseStyles.MakeSectionHeader("BLEND", "⊂"));
            var blendCard = SnapPoseStyles.MakeCard();

            var blendRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var blendLbl = new Label("Weight");
            blendLbl.AddToClassList("snap-label-secondary");
            blendLbl.style.minWidth = 44;
            blendRow.Add(blendLbl);

            _blendSlider = new Slider(0f, 1f) { value = 1f };
            _blendSlider.style.flexGrow = 1;
            blendRow.Add(_blendSlider);

            _blendLabel = new Label("100%");
            _blendLabel.AddToClassList("snap-label-accent");
            _blendLabel.style.minWidth = 36;
            _blendSlider.RegisterValueChangedCallback(e =>
                _blendLabel.text = $"{(int)(e.newValue * 100)}%");
            blendRow.Add(_blendLabel);
            blendCard.Add(blendRow);
            inner.Add(blendCard);

            // ── Bone Mask ──────────────────────────────────────────────────
            var boneMaskSection = new CollapsibleSection("BONE MASK", "🦴", defaultExpanded: true);
            _maskEditor = new BoneMaskEditorPanel(_ctrl, _boneMask);
            var maskCard = SnapPoseStyles.MakeCard();
            maskCard.Add(_maskEditor);
            boneMaskSection.AddContent(maskCard);
            inner.Add(boneMaskSection);

            // ── Mirror ─────────────────────────────────────────────────────
            var mirrorSection = new CollapsibleSection("MIRROR", "⟺", defaultExpanded: false);
            _mirrorPanel = new MirrorSetupPanel(_ctrl);
            var mirrorCard = SnapPoseStyles.MakeCard();
            mirrorCard.Add(_mirrorPanel);
            mirrorSection.AddContent(mirrorCard);
            inner.Add(mirrorSection);

            // ── Pose Stack ─────────────────────────────────────────────────
            var poseStackSection = new CollapsibleSection("POSE STACK", "⧉", defaultExpanded: true);
            _stackPanel = new PoseStackPanel(_ctrl);
            poseStackSection.AddContent(_stackPanel);
            inner.Add(poseStackSection);

            // ── Diff View ──────────────────────────────────────────────────
            var diffViewSection = new CollapsibleSection("DIFF VIEW", "⊕", defaultExpanded: false);
            _diffPanel = new DiffViewPanel(_ctrl);
            diffViewSection.AddContent(_diffPanel);
            inner.Add(diffViewSection);

            // ── History ────────────────────────────────────────────────────
            var historySection = new CollapsibleSection("HISTORY", "📜", defaultExpanded: false);
            _historyPanel = new HistoryPanel(_ctrl);
            historySection.AddContent(_historyPanel);
            inner.Add(historySection);

            // ── Humanoid Muscle Inspector ───────────────────────────────────
            var muscleSection = new CollapsibleSection("MUSCLE INSPECTOR", "🫀", defaultExpanded: false);
            _musclePanel = new HumanoidMusclePanel(_ctrl);
            var muscleCard = SnapPoseStyles.MakeCard();
            muscleCard.Add(_musclePanel);
            muscleSection.AddContent(muscleCard);
            inner.Add(muscleSection);

            // ── Action Bar (pinned at bottom, outside scroll) ──────────────
            var actionBar = new VisualElement
            {
                style =
                {
                    flexDirection   = FlexDirection.Column,
                    flexShrink      = 0,          // never shrink — always fully visible
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f),
                    paddingTop      = 8,
                    paddingBottom   = 8,
                    paddingLeft     = 8,
                    paddingRight    = 8,
                    borderTopWidth  = 1,
                    borderTopColor  = new Color(0.1f, 0.1f, 0.1f)
                }
            };

            // ── Apply mode toggle ──────────────────────────────────────────────
            var modeRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };

            _radioPermanent = new RadioButton("Permanent") { value = true };
            _radioPreview   = new RadioButton("Preview Only");

            _radioPermanent.RegisterValueChangedCallback(e =>
            {
                if (!e.newValue) return;
                _applyMode = ApplyMode.Permanent;
                RefreshApplyButton();
            });
            _radioPreview.RegisterValueChangedCallback(e =>
            {
                if (!e.newValue) return;
                _applyMode = ApplyMode.Preview;
                RefreshApplyButton();
            });

            modeRow.Add(_radioPermanent);
            modeRow.Add(_radioPreview);

            // Mode badge — shows current mode prominently
            _modeBadge = new Label("PERMANENT");
            _modeBadge.AddToClassList("snap-mode-badge");
            _modeBadge.AddToClassList("snap-mode-badge--permanent");
            modeRow.Add(_modeBadge);

            actionBar.Add(modeRow);

            // Mode description hint
            var modeHint = new Label();
            modeHint.AddToClassList("snap-label-secondary");
            modeHint.style.marginBottom  = 6;
            modeHint.style.whiteSpace    = WhiteSpace.Normal;
            modeHint.style.fontSize      = 9;
            // Update hint when mode changes
            void UpdateModeHint()
            {
                if (_applyMode == ApplyMode.Permanent)
                    modeHint.text = "Writes transforms permanently. Ctrl+Z to undo.";
                else
                    modeHint.text = "Pose shown in Scene view only — no permanent changes. Stop Preview to revert.";
            }
            _radioPermanent.RegisterValueChangedCallback(_ => UpdateModeHint());
            _radioPreview.RegisterValueChangedCallback(_ => UpdateModeHint());
            UpdateModeHint();
            actionBar.Add(modeHint);

            // ── Buttons row ────────────────────────────────────────────────────
            var btnRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row } };

            _sampleBtn = SnapPoseStyles.MakeSecondaryButton("📷 Sample", () =>
            {
                var pose = _ctrl.SampleCurrentPose(_boneMask);
                if (pose != null)
                {
                    _ctrl.ComputeAndPublishDiff(pose, _boneMask);
                    _ctrl.AddLayer(pose);
                }
            });
            _sampleBtn.tooltip = "Sample current frame and add to Pose Stack";
            _sampleBtn.style.flexGrow = 1;
            btnRow.Add(_sampleBtn);

            _saveBtn = SnapPoseStyles.MakeSecondaryButton("💾 Save Pose", () =>
            {
                var pose = _ctrl.SampleCurrentPose(_boneMask);
                if (pose != null) _ctrl.SavePoseToLibrary(pose);
            });
            _saveBtn.style.flexGrow = 1;
            btnRow.Add(_saveBtn);

            actionBar.Add(btnRow);

            _applyBtn = SnapPoseStyles.MakePrimaryButton("⚡ APPLY POSE", () =>
            {
                _ctrl.ApplyCurrentPose(_boneMask, _blendSlider.value, _applyMode);
            });
            _applyBtn.style.marginTop = 6;
            actionBar.Add(_applyBtn);

            // Diff pre-check
            var diffCheckBtn = SnapPoseStyles.MakeSecondaryButton("⊕ Preview Changes (Diff)", () =>
            {
                var pose = _ctrl.SampleCurrentPose(_boneMask);
                if (pose != null) _ctrl.ComputeAndPublishDiff(pose, _boneMask);
            });
            diffCheckBtn.style.marginTop = 4;
            actionBar.Add(diffCheckBtn);

            Add(actionBar);
        }

        // ── Event Handlers ────────────────────────────────────────────────────
        void OnStateChanged()
        {
            var hasTarget = _ctrl.TargetObject != null;
            var hasClip   = _ctrl.SelectedClip != null;

            if (hasTarget)
            {
                var rig = PoseSampler.DetectRigType(_ctrl.TargetObject);
                _targetLabel.text  = $"🎯  {_ctrl.TargetObject.name}   [{rig}]";
                _targetLabel.style.color = SnapPoseStyles.Accent;
            }
            else
            {
                _targetLabel.text  = "Select an object from the Workspace panel";
                _targetLabel.style.color = SnapPoseStyles.TextDisabled;
            }

            _applyBtn.SetEnabled(hasTarget && hasClip);
            _sampleBtn.SetEnabled(hasTarget && hasClip);
            _saveBtn.SetEnabled(hasTarget && hasClip);
            _previewBtn.SetEnabled(hasTarget && hasClip);

            RefreshApplyButton();
        }

        void RefreshApplyButton()
        {
            if (_applyMode == ApplyMode.Preview)
            {
                _applyBtn.text = "👁 PREVIEW POSE";
                _applyBtn.tooltip = "Enters AnimationMode — pose is shown in Scene view only. Stop Preview to revert.";
                _applyBtn.style.backgroundColor = new StyleColor(new Color(0.55f, 0.35f, 0.80f)); // purple

                _modeBadge.text = "PREVIEW ONLY";
                _modeBadge.RemoveFromClassList("snap-mode-badge--permanent");
                _modeBadge.AddToClassList("snap-mode-badge--preview");
            }
            else
            {
                _applyBtn.text = "⚡ APPLY POSE";
                _applyBtn.tooltip = "Writes pose transforms permanently. Supports Ctrl+Z undo.";
                _applyBtn.style.backgroundColor = new StyleColor(SnapPoseStyles.Accent);

                _modeBadge.text = "PERMANENT";
                _modeBadge.RemoveFromClassList("snap-mode-badge--preview");
                _modeBadge.AddToClassList("snap-mode-badge--permanent");
            }
        }

        void OnPreviewChanged()
        {
            // Scrubber handles its own playback polling via BindController / UIToolkit scheduler.
            // We only need to refresh the preview button state here.
            RefreshPreviewButton();
        }

        void RefreshPreviewButton()
        {
            if (_ctrl.IsPreviewActive)
            {
                _previewBtn.text    = "■ Stop Preview";
                _previewBtn.tooltip = "Stop AnimationMode preview and revert to original pose";
                _previewBtn.style.backgroundColor = new StyleColor(SnapPoseStyles.DangerColor * 0.7f);
            }
            else
            {
                _previewBtn.text    = "▶ Start Preview";
                _previewBtn.tooltip = "Enter AnimationMode — scrub or play the animation live in Scene view";
                _previewBtn.style.backgroundColor = new StyleColor(SnapPoseStyles.Accent);
            }
        }

        void TogglePreview()
        {
            if (_ctrl.IsPreviewActive) _ctrl.StopPreview();
            else _ctrl.StartPreview(_boneMask);
        }
    }
}
