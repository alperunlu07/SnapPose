using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    /// <summary>
    /// Scrubable timeline control. Shows tick marks for each frame,
    /// a draggable playhead, and frame number overlay.
    /// </summary>
    public class TimelineScrubber : VisualElement
    {
        public event Action<float> OnTimeChanged;   // normalized 0..1
        public event Action<int>   OnFrameChanged;
        public event Action        OnPlayPauseClicked;

        private int   _totalFrames = 1;
        private float _frameRate   = 30f;
        private float _currentTime = 0f;  // seconds
        private float _duration    = 1f;
        private bool  _isDragging  = false;
        private bool  _isPlaying   = false;

        // Controller reference for polling during playback
        private SnapPoseController _ctrl;

        // Sub-elements
        private VisualElement _track;
        private VisualElement _head;
        private Label         _frameLabel;
        private VisualElement _tickContainer;
        private Button        _playPauseBtn;

        // UIToolkit scheduled item for playback polling
        private IVisualElementScheduledItem _playbackSchedule;

        public TimelineScrubber()
        {
            style.flexDirection = FlexDirection.Column;
            style.marginTop     = 4;
            style.marginBottom  = 4;

            BuildUI();
            RegisterCallbacks();
        }

        /// <summary>
        /// Bind a controller so the scrubber can poll CurrentTime during playback
        /// via UIToolkit's own scheduler instead of relying on external SyncTime calls.
        /// </summary>
        public void BindController(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnPreviewChanged += OnControllerPreviewChanged;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_ctrl != null) _ctrl.OnPreviewChanged -= OnControllerPreviewChanged;
                _playbackSchedule?.Pause();
            });
        }

        void OnControllerPreviewChanged()
        {
            bool playing = _ctrl != null && _ctrl.IsPlaying;
            SetPlayingState(playing);

            if (playing)
            {
                // Start UIToolkit schedule loop if not already running
                if (_playbackSchedule == null)
                {
                    _playbackSchedule = schedule.Execute(PollPlayback).Every(16); // ~60fps
                }
                else
                {
                    _playbackSchedule.Resume();
                }
            }
            else
            {
                _playbackSchedule?.Pause();
                // Sync one last time to show final stopped position
                if (_ctrl != null) SyncTime(_ctrl.CurrentTime);
            }
        }

        void PollPlayback()
        {
            if (_ctrl == null || !_ctrl.IsPlaying)
            {
                _playbackSchedule?.Pause();
                return;
            }
            SyncTime(_ctrl.CurrentTime);
            MarkDirtyRepaint();
        }

        void BuildUI()
        {
            // Top row: label + frame badge
            var topRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };

            var titleLbl = new Label("Timeline");
            titleLbl.AddToClassList("snap-label-secondary");
            titleLbl.style.flexGrow = 1;

            _frameLabel = new Label("Frame 0 / 0");
            _frameLabel.AddToClassList("snap-frame-badge");

            topRow.Add(titleLbl);
            topRow.Add(_frameLabel);
            Add(topRow);

            // Track container
            _track = new VisualElement();
            _track.AddToClassList("snap-timeline-track");
            _track.style.position = Position.Relative;
            _track.style.overflow = Overflow.Hidden;

            // Tick marks
            _tickContainer = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    flexDirection = FlexDirection.Row
                }
            };
            _track.Add(_tickContainer);

            // Scrub fill (left of head)
            var fill = new VisualElement
            {
                name = "fill",
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    backgroundColor = new Color(0.31f, 0.76f, 0.97f, 0.12f)
                }
            };
            _track.Add(fill);

            // Playhead
            _head = new VisualElement();
            _head.AddToClassList("snap-timeline-head");
            _head.style.position = Position.Absolute;
            _head.style.top      = 0;
            _head.style.bottom   = 0;
            _head.style.width    = 2;
            _head.style.backgroundColor = SnapPoseStyles.Accent;

            // Diamond at top of head
            var diamond = new VisualElement
            {
                style =
                {
                    position        = Position.Absolute,
                    top             = -1,
                    left            = -5,
                    width           = 10,
                    height          = 10,
                    backgroundColor = SnapPoseStyles.Accent,
                    rotate          = new Rotate(45f)
                }
            };
            _head.Add(diamond);
            _track.Add(_head);

            Add(_track);

            // Transport row
            var transport = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };

            var btnFirst = SnapPoseStyles.MakeIconButton("⏮", () => SetFrame(0),                  "First Frame");
            var btnPrev  = SnapPoseStyles.MakeIconButton("◀",  () => SetFrame(CurrentFrame - 1),  "Prev Frame");

            // ── Play / Pause ──────────────────────────────────────────────────
            _playPauseBtn = new Button(() => OnPlayPauseClicked?.Invoke())
            {
                text    = "▶",
                tooltip = "Play / Pause animation (requires Preview active)"
            };
            _playPauseBtn.AddToClassList("snap-btn-play");
            // ──────────────────────────────────────────────────────────────────

            var btnNext  = SnapPoseStyles.MakeIconButton("▶",  () => SetFrame(CurrentFrame + 1),  "Next Frame");
            var btnLast  = SnapPoseStyles.MakeIconButton("⏭", () => SetFrame(_totalFrames),       "Last Frame");

            var spacerL = new VisualElement { style = { flexGrow = 1 } };
            var spacerR = new VisualElement { style = { flexGrow = 1 } };

            transport.Add(btnFirst);
            transport.Add(btnPrev);
            transport.Add(spacerL);
            transport.Add(_playPauseBtn);
            transport.Add(spacerR);
            transport.Add(btnNext);
            transport.Add(btnLast);
            Add(transport);
        }

        void RegisterCallbacks()
        {
            _track.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _track.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _track.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _track.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            RegisterCallback<GeometryChangedEvent>(_ => RefreshHead());
        }

        // ── Public API ────────────────────────────────────────────────────────
        public int CurrentFrame => Mathf.RoundToInt(_currentTime * _frameRate);

        /// <summary>
        /// Called by InspectorPanel on every OnPreviewChanged to keep the
        /// play/pause button icon and playhead in sync with controller state.
        /// </summary>
        public void SetPlayingState(bool isPlaying)
        {
            _isPlaying = isPlaying;
            if (_playPauseBtn != null)
            {
                _playPauseBtn.text    = isPlaying ? "⏸" : "▶";
                _playPauseBtn.tooltip = isPlaying ? "Pause" : "Play animation";
                _playPauseBtn.style.color = new StyleColor(isPlaying
                    ? SnapPoseStyles.WarningColor
                    : SnapPoseStyles.Accent);
            }
        }

        /// <summary>Sync playhead to an absolute time without firing events (used during playback).</summary>
        public void SyncTime(float seconds)
        {
            _currentTime = Mathf.Clamp(seconds, 0f, _duration);
            RefreshHead();
        }

        public void SetClip(UnityEngine.AnimationClip clip)
        {
            if (clip == null) { SetClipParams(30f, 0f); return; }
            SetClipParams(clip.frameRate, clip.length);
        }

        public void SetClipParams(float frameRate, float duration)
        {
            _frameRate   = Mathf.Max(1f, frameRate);
            _duration    = Mathf.Max(0.001f, duration);
            _totalFrames = Mathf.RoundToInt(_duration * _frameRate);
            _currentTime = 0f;
            RebuildTicks();
            RefreshHead();
        }

        public void SetTime(float seconds)
        {
            _currentTime = Mathf.Clamp(seconds, 0f, _duration);
            RefreshHead();
        }

        public void SetFrame(int frame)
        {
            frame = Mathf.Clamp(frame, 0, _totalFrames);
            SetTime(frame / _frameRate);
            OnFrameChanged?.Invoke(CurrentFrame);
            OnTimeChanged?.Invoke(_currentTime / _duration);
        }

        // ── Pointer handling ──────────────────────────────────────────────────
        void OnPointerDown(PointerDownEvent e)
        {
            // Scrubbing while playing → pause first so user can pick a frame
            if (_isPlaying) OnPlayPauseClicked?.Invoke();

            _isDragging = true;
            _track.CapturePointer(e.pointerId);
            ScrubToLocalX(e.localPosition.x);
            e.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (!_isDragging) return;
            ScrubToLocalX(e.localPosition.x);
            e.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent e)
        {
            _isDragging = false;
            _track.ReleasePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnPointerLeave(PointerLeaveEvent e)
        {
            if (_isDragging) { _isDragging = false; _track.ReleasePointer(0); }
        }

        void ScrubToLocalX(float x)
        {
            float w = _track.resolvedStyle.width;
            if (w <= 0f) return;
            float norm = Mathf.Clamp01(x / w);
            _currentTime = norm * _duration;
            int frame = CurrentFrame;
            RefreshHead();
            OnFrameChanged?.Invoke(frame);
            OnTimeChanged?.Invoke(norm);
        }

        // ── Visual update ─────────────────────────────────────────────────────
        void RefreshHead()
        {
            float w    = _track.resolvedStyle.width;
            if (w <= 0f || float.IsNaN(w)) return;
            float norm = _duration > 0f ? _currentTime / _duration : 0f;
            float xPos = norm * w;

            _head.style.left = xPos;

            // Update fill
            var fill = _track.Q("fill");
            if (fill != null) fill.style.width = xPos;

            // Update label
            _frameLabel.text = $"Frame {CurrentFrame} / {_totalFrames}";
        }

        void RebuildTicks()
        {
            _tickContainer.Clear();
            if (_totalFrames <= 0) return;

            // Show at most 60 ticks to avoid crowding
            int step = Mathf.Max(1, _totalFrames / 60);
            float pct = 100f / _totalFrames;

            for (int i = 0; i <= _totalFrames; i += step)
            {
                bool isMajor = i % (step * 5) == 0 || i == 0 || i == _totalFrames;
                var tick = new VisualElement
                {
                    style =
                    {
                        position       = Position.Absolute,
                        left           = Length.Percent(i * pct),
                        top            = isMajor ? 4  : 12,
                        bottom         = isMajor ? 4  : 12,
                        width          = 1,
                        backgroundColor = isMajor
                            ? new Color(0.5f, 0.5f, 0.5f, 0.5f)
                            : new Color(0.4f, 0.4f, 0.4f, 0.25f)
                    }
                };
                _tickContainer.Add(tick);

                if (isMajor && _totalFrames <= 120)
                {
                    var lbl = new Label(i.ToString())
                    {
                        style =
                        {
                            position  = Position.Absolute,
                            left      = Length.Percent(i * pct),
                            bottom    = 2,
                            fontSize  = 8,
                            color     = new Color(0.5f, 0.5f, 0.5f, 0.7f)
                        }
                    };
                    _tickContainer.Add(lbl);
                }
            }
        }
    }
}
