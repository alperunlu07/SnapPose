using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SnapPose.Editor
{
    /// <summary>
    /// Central state manager shared between all UI panels.
    /// </summary>
    public class SnapPoseController
    {
        // ── Events ────────────────────────────────────────────────────────────
        public event Action OnStateChanged;
        public event Action OnPreviewChanged;
        public event Action<List<PoseApplicator.BoneDiff>> OnDiffComputed;

        // ── State ─────────────────────────────────────────────────────────────
        public GameObject    TargetObject    { get; private set; }
        public AnimationClip SelectedClip   { get; private set; }
        public float         CurrentTime    { get; private set; }
        public bool          IsPreviewActive { get; private set; }
        public bool          IsPlaying      { get; private set; }
        public float         PlaybackSpeed  { get; set; } = 1f;
        public PoseData      LastSampledPose { get; private set; }
        public PoseLibrary   ActiveLibrary  { get; set; }

        // Per-object inspector state — saved and restored when switching workspace objects
        public BoneMask  ActiveMask        { get; set; } = new BoneMask();
        public float     ActiveBlendWeight { get; set; } = 1f;
        public ApplyMode ActiveApplyMode   { get; set; } = ApplyMode.Permanent;

        double _lastEditorTime;

        // Mask + rest-pose snapshot for masked playback/preview
        BoneMask                              _previewMask;
        Dictionary<string, BoneTransformData> _restPose;

        public List<PoseLayerConfig> PoseStack { get; } = new List<PoseLayerConfig>();
        public List<HistoryEntry>    History   { get; } = new List<HistoryEntry>();

        public class HistoryEntry
        {
            public string      label;
            public string      targetName;
            public PoseData    poseSnapshot;
            public ApplyMode   mode;
            public System.DateTime timestamp;
        }

        sealed class PerObjectState
        {
            public AnimationClip         Clip;
            public float                 Time;
            public float                 Speed;
            public BoneMask              Mask;
            public float                 BlendWeight;
            public ApplyMode             ApplyMode;
            public List<PoseLayerConfig> Stack;
            public List<HistoryEntry>    History;
            public bool                  IsPlaying;
        }

        readonly Dictionary<int, PerObjectState> _objectStates = new Dictionary<int, PerObjectState>();

        // ── Global multi-object playback ──────────────────────────────────────
        // All objects currently playing, independent of which one is "selected".
        // A single EditorApplication.update loop samples all of them each frame.
        class GlobalPlayEntry
        {
            public GameObject    Go;
            public AnimationClip Clip;
            public float         Time;
            public float         Speed;
        }

        readonly List<GlobalPlayEntry> _globalPlayEntries  = new List<GlobalPlayEntry>();
        bool                           _globalUpdateRunning = false;

        // ── Target ───────────────────────────────────────────────────────────
        public void SetTarget(GameObject go)
        {
            // Capture BEFORE any change so IsPlaying=true is preserved in the snapshot
            var stateToSave = TargetObject != null ? CaptureState() : null;

            // Keep outgoing object in the global playback list (it keeps animating in background).
            // Only stop scrubbing-only preview (not playing) for the outgoing target.
            if (IsPreviewActive && !IsPlaying)
            {
                // Was scrubbing but not playing — clear scrub state.
                // Don't call StopPreview: AnimationMode may still be needed for background objects.
                IsPreviewActive = false;
                _previewMask    = null;
                _restPose       = null;
                if (_globalPlayEntries.Count == 0)
                {
                    if (_globalUpdateRunning)
                    {
                        _globalUpdateRunning = false;
                        EditorApplication.update -= OnEditorUpdate;
                    }
                    PoseSampler.StopPreview();
                }
            }

            // Sync speed on the outgoing entry in case PlaybackSpeed changed
            if (IsPlaying && TargetObject != null)
            {
                var outEntry = _globalPlayEntries.Find(e => e.Go == TargetObject);
                if (outEntry != null) outEntry.Speed = PlaybackSpeed;
            }

            // Save outgoing state
            if (stateToSave != null)
                _objectStates[TargetObject.GetInstanceID()] = stateToSave;

            // Reset active-target flags (not the global list)
            IsPlaying       = false;
            IsPreviewActive = false;
            _previewMask    = null;
            _restPose       = null;

            TargetObject = go;

            if (go != null && _objectStates.TryGetValue(go.GetInstanceID(), out var saved))
            {
                ApplyState(saved);

                // If this object is already playing in the background, sync its live time
                var liveEntry = _globalPlayEntries.Find(e => e.Go == go);
                if (liveEntry != null)
                {
                    CurrentTime     = liveEntry.Time;
                    IsPlaying       = true;
                    IsPreviewActive = true;
                }
                else if (saved.IsPlaying && SelectedClip != null)
                {
                    // Was playing before; add it to the global list now
                    StartPlayback(ActiveMask);
                }
            }
            else
            {
                ResetState();
            }

            NotifyStateChanged();
        }

        PerObjectState CaptureState() => new PerObjectState
        {
            Clip        = SelectedClip,
            Time        = CurrentTime,
            Speed       = PlaybackSpeed,
            Mask        = ActiveMask.Clone(),
            BlendWeight = ActiveBlendWeight,
            ApplyMode   = ActiveApplyMode,
            Stack       = new List<PoseLayerConfig>(PoseStack),
            History     = new List<HistoryEntry>(History),
            IsPlaying   = IsPlaying
        };

        void ApplyState(PerObjectState s)
        {
            SelectedClip      = s.Clip;
            CurrentTime       = s.Time;
            PlaybackSpeed     = s.Speed;
            ActiveMask        = s.Mask.Clone();
            ActiveBlendWeight = s.BlendWeight;
            ActiveApplyMode   = s.ApplyMode;
            PoseStack.Clear(); PoseStack.AddRange(s.Stack);
            History.Clear();   History.AddRange(s.History);
        }

        void ResetState()
        {
            SelectedClip      = null;
            CurrentTime       = 0f;
            PlaybackSpeed     = 1f;
            ActiveMask        = new BoneMask();
            ActiveBlendWeight = 1f;
            ActiveApplyMode   = ApplyMode.Permanent;
            PoseStack.Clear();
            History.Clear();
        }

        /// <summary>Returns true if the object is currently playing (active or background).</summary>
        public bool GetSavedIsPlaying(GameObject go)
        {
            if (go == null) return false;
            if (go == TargetObject) return IsPlaying;
            // Check live global entries first (object may be actively animating in background)
            if (_globalPlayEntries.Exists(e => e.Go == go)) return true;
            return _objectStates.TryGetValue(go.GetInstanceID(), out var s) && s.IsPlaying;
        }

        /// <summary>Stops and clears the playing state for any object (active or background).</summary>
        public void ClearSavedPlayingState(GameObject go)
        {
            if (go == null) return;
            bool wasActiveTarget = go == TargetObject;
            _globalPlayEntries.RemoveAll(e => e.Go == go);
            if (wasActiveTarget) IsPlaying = false;
            if (_objectStates.TryGetValue(go.GetInstanceID(), out var s))
                s.IsPlaying = false;
            // Stop the update loop if nothing is playing anymore
            if (_globalPlayEntries.Count == 0 && _globalUpdateRunning)
            {
                _globalUpdateRunning = false;
                EditorApplication.update -= OnEditorUpdate;
                if (!IsPreviewActive && AnimationMode.InAnimationMode())
                    PoseSampler.StopPreview();
            }
        }

        /// <summary>Called by WorkspacePanel when an object is removed; cleans up all playback state.</summary>
        public void OnObjectRemoved(GameObject go)
        {
            if (go == null) return;
            ClearSavedPlayingState(go);
            _objectStates.Remove(go.GetInstanceID());
        }

        // ── Clip / Time ───────────────────────────────────────────────────────
        public void SetClip(AnimationClip clip)
        {
            SelectedClip = clip;
            CurrentTime  = 0f;
            if (IsPreviewActive) RefreshPreview();
            NotifyStateChanged();
        }

        public void SetTime(float t)
        {
            if (SelectedClip == null) return;
            CurrentTime = Mathf.Clamp(t, 0f, SelectedClip.length);
            if (IsPreviewActive) RefreshPreview();
            OnPreviewChanged?.Invoke();
        }

        public void SetFrame(int frame)
        {
            if (SelectedClip == null) return;
            SetTime(PoseSampler.FrameToTime(SelectedClip, frame));
        }

        public int CurrentFrame => SelectedClip != null
            ? PoseSampler.TimeToFrame(SelectedClip, CurrentTime)
            : 0;

        public int TotalFrames => SelectedClip != null
            ? PoseSampler.TotalFrames(SelectedClip)
            : 0;

        // ── Preview ───────────────────────────────────────────────────────────
        public void StartPreview(BoneMask mask = null)
        {
            if (TargetObject == null || SelectedClip == null) return;

            // Capture rest pose BEFORE AnimationMode starts (transforms are still at rest)
            _previewMask = mask;
            _restPose    = PoseSampler.CaptureRestPose(TargetObject);

            IsPreviewActive = true;
            PoseSampler.StartPreview(TargetObject, SelectedClip, CurrentTime);

            // Start the global update loop so the scrubbing target is re-sampled inside every
            // BeginSampling/EndSampling batch — otherwise background playback batches erase its pose.
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (!_globalUpdateRunning)
            {
                _globalUpdateRunning = true;
                EditorApplication.update += OnEditorUpdate;
            }

            OnPreviewChanged?.Invoke();
        }

        public void StopPreview()
        {
            StopPlayback(); // removes active target from global list if playing
            if (!IsPreviewActive) return;
            IsPreviewActive = false;
            _previewMask    = null;
            _restPose       = null;
            // Only stop AnimationMode and the update loop when no other objects are playing
            if (_globalPlayEntries.Count == 0)
            {
                if (_globalUpdateRunning)
                {
                    _globalUpdateRunning = false;
                    EditorApplication.update -= OnEditorUpdate;
                }
                PoseSampler.StopPreview();
            }
            OnPreviewChanged?.Invoke();
        }

        void RefreshPreview()
        {
            if (TargetObject == null || SelectedClip == null) return;

            bool hasMask = _previewMask != null && _previewMask.mode != BoneMask.MaskMode.AllBones;
            if (hasMask && _restPose != null)
                PoseSampler.UpdatePreviewWithMask(TargetObject, SelectedClip, CurrentTime, _previewMask, _restPose);
            else
                PoseSampler.UpdatePreview(TargetObject, SelectedClip, CurrentTime);
        }

        // ── Stop all ─────────────────────────────────────────────────────────
        /// <summary>Stops every playing object and exits AnimationMode entirely.
        /// Call this when the tool window closes so nothing leaks into the editor.</summary>
        public void StopAll()
        {
            _globalPlayEntries.Clear();
            IsPlaying       = false;
            IsPreviewActive = false;
            _previewMask    = null;
            _restPose       = null;
            if (_globalUpdateRunning)
            {
                _globalUpdateRunning = false;
                EditorApplication.update -= OnEditorUpdate;
            }
            PoseSampler.StopPreview(); // calls AnimationMode.StopAnimationMode()
            OnPreviewChanged?.Invoke();
        }

        /// <summary>
        /// Fully exits AnimationMode (required before writing transforms permanently).
        /// Returns background entries that were playing so they can be restarted afterwards.
        /// The active TargetObject is intentionally excluded from the returned list.
        /// </summary>
        List<GlobalPlayEntry> PauseAllForApply()
        {
            var background = _globalPlayEntries.FindAll(e => e.Go != TargetObject);
            _globalPlayEntries.Clear();
            IsPlaying       = false;
            IsPreviewActive = false;
            _previewMask    = null;
            _restPose       = null;
            if (_globalUpdateRunning)
            {
                _globalUpdateRunning = false;
                EditorApplication.update -= OnEditorUpdate;
            }
            PoseSampler.StopPreview(); // AnimationMode.StopAnimationMode()
            return background;
        }

        /// <summary>Restarts background playback after a permanent apply operation.</summary>
        void ResumeBackgroundPlayback(List<GlobalPlayEntry> background)
        {
            if (background.Count == 0) return;
            _globalPlayEntries.AddRange(background);
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (!_globalUpdateRunning)
            {
                _globalUpdateRunning = true;
                EditorApplication.update += OnEditorUpdate;
            }
        }

        // ── Playback ──────────────────────────────────────────────────────────
        public void StartPlayback(BoneMask mask = null)
        {
            if (TargetObject == null || SelectedClip == null) return;

            // Register (or update) this object in the global playback list
            _globalPlayEntries.RemoveAll(e => e.Go == TargetObject);
            _globalPlayEntries.Add(new GlobalPlayEntry
            {
                Go    = TargetObject,
                Clip  = SelectedClip,
                Time  = CurrentTime,
                Speed = PlaybackSpeed
            });

            IsPlaying       = true;
            IsPreviewActive = true;
            if (mask != null) _previewMask = mask;

            // Ensure AnimationMode is running
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            // Start the global update loop if not already running
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (!_globalUpdateRunning)
            {
                _globalUpdateRunning = true;
                EditorApplication.update += OnEditorUpdate;
            }

            OnPreviewChanged?.Invoke();
        }

        public void StopPlayback()
        {
            if (!IsPlaying) return;
            _globalPlayEntries.RemoveAll(e => e.Go == TargetObject);
            IsPlaying = false;

            // Keep the update loop alive if:
            //   • other background objects are still playing, OR
            //   • the active target is in scrubbing-only preview (holds the last frame)
            if (_globalPlayEntries.Count == 0 && !IsPreviewActive && _globalUpdateRunning)
            {
                _globalUpdateRunning = false;
                EditorApplication.update -= OnEditorUpdate;
            }

            OnPreviewChanged?.Invoke();
        }

        public void TogglePlayback()
        {
            if (IsPlaying) StopPlayback();
            else           StartPlayback();
        }

        void OnEditorUpdate()
        {
            bool hasPlayingEntries = _globalPlayEntries.Count > 0;
            bool hasScrubbing      = IsPreviewActive && !IsPlaying
                                     && TargetObject != null && SelectedClip != null;

            // Nothing to do — shut the loop down
            if (!hasPlayingEntries && !hasScrubbing)
            {
                _globalUpdateRunning = false;
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            var   now   = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            // Remove entries whose GameObjects were destroyed in the scene
            _globalPlayEntries.RemoveAll(e => e.Go == null);

            // Sample ALL playing objects AND the scrubbing target in one AnimationMode batch.
            // Including the scrubbing target here prevents BeginSampling from clearing its pose
            // when background objects are animating.

            AnimationMode.BeginSampling();
            foreach (var entry in _globalPlayEntries)
            {
                entry.Time += delta * entry.Speed;
                if (entry.Time >= entry.Clip.length)
                    entry.Time = entry.Time % entry.Clip.length;
                AnimationMode.SampleAnimationClip(entry.Go, entry.Clip, entry.Time);
            }
            if (hasScrubbing)
                AnimationMode.SampleAnimationClip(TargetObject, SelectedClip, CurrentTime);
            AnimationMode.EndSampling();

            // Keep CurrentTime in sync for the active target when it is playing
            var activeEntry = _globalPlayEntries.Find(e => e.Go == TargetObject);
            if (activeEntry != null)
                CurrentTime = activeEntry.Time;

            SceneView.RepaintAll();
            OnPreviewChanged?.Invoke();
        }

        // ── Sampling & Apply ─────────────────────────────────────────────────
        public PoseData SampleCurrentPose(BoneMask mask = null)
        {
            if (TargetObject == null || SelectedClip == null) return null;
            LastSampledPose = PoseSampler.Sample(TargetObject, SelectedClip, CurrentTime, mask, IsPreviewActive);
            return LastSampledPose;
        }

        public void ApplyCurrentPose(BoneMask mask, float blendWeight, ApplyMode mode)
        {
            if (TargetObject == null) return;

            if (mode == ApplyMode.Preview)
            {
                // Preview-only: sample and enter AnimationMode without writing transforms permanently
                PreviewPose(mask, blendWeight);
                return;
            }

            var pose = SampleCurrentPose(mask);
            if (pose == null) return;

            // Fully stop AnimationMode before writing transforms permanently.
            // AnimationMode.BeginSampling() resets all previously-driven values on every frame,
            // which would silently undo any transform writes made while AnimationMode is active.
            var background = PauseAllForApply();
            PoseApplicator.Apply(TargetObject, pose, mask, blendWeight, mode);
            ResumeBackgroundPlayback(background);

            AddHistory(new HistoryEntry
            {
                label         = pose.poseName,
                targetName    = TargetObject.name,
                poseSnapshot  = pose.Clone(),
                mode          = mode,
                timestamp     = System.DateTime.Now
            });

            NotifyStateChanged();
        }

        /// <summary>
        /// Enters a "pose preview" via AnimationMode — the pose is visible in the
        /// Scene view but no transforms are permanently modified. Calling StopPreview()
        /// or switching target/clip will revert the character to its original state.
        /// </summary>
        public void PreviewPose(BoneMask mask, float blendWeight)
        {
            if (TargetObject == null || SelectedClip == null) return;

            // Sample at current time (this puts us into AnimationMode)
            LastSampledPose = PoseSampler.Sample(TargetObject, SelectedClip, CurrentTime, mask,
                keepPreviewActive: true);

            IsPreviewActive = true;
            OnPreviewChanged?.Invoke();
            NotifyStateChanged();
        }

        public void ApplyStack(ApplyMode mode)
        {
            if (TargetObject == null) return;
            var background = PauseAllForApply();
            PoseApplicator.ApplyStack(TargetObject, PoseStack, mode);
            ResumeBackgroundPlayback(background);
            NotifyStateChanged();
        }

        // ── Diff ─────────────────────────────────────────────────────────────
        public void ComputeAndPublishDiff(PoseData targetPose, BoneMask mask = null)
        {
            if (TargetObject == null || targetPose == null) return;
            var diffs = PoseApplicator.ComputeDiff(TargetObject, targetPose, mask);
            OnDiffComputed?.Invoke(diffs);
        }

        // ── Pose Stack ───────────────────────────────────────────────────────
        public void AddLayer(PoseData pose)
        {
            PoseStack.Add(new PoseLayerConfig
            {
                layerName   = pose != null ? pose.poseName : "Layer",
                sourcePose  = pose,
                blendWeight = 1f,
                mask        = new BoneMask()
            });
            NotifyStateChanged();
        }

        public void RemoveLayer(int index)
        {
            if (index >= 0 && index < PoseStack.Count)
            {
                PoseStack.RemoveAt(index);
                NotifyStateChanged();
            }
        }

        public void MoveLayer(int from, int to)
        {
            if (from < 0 || from >= PoseStack.Count) return;
            var layer = PoseStack[from];
            PoseStack.RemoveAt(from);
            PoseStack.Insert(Mathf.Clamp(to, 0, PoseStack.Count), layer);
            NotifyStateChanged();
        }

        // ── History ───────────────────────────────────────────────────────────
        void AddHistory(HistoryEntry entry)
        {
            History.Insert(0, entry);
            if (History.Count > 20) History.RemoveAt(History.Count - 1);
        }

        public void UndoHistory(int index)
        {
            Undo.PerformUndo();
            // Note: We rely on Unity's Undo system for actual revert.
            // This is for display purposes.
        }

        // ── Library ───────────────────────────────────────────────────────────
        public void SavePoseToLibrary(PoseData pose, string overrideName = null)
        {
            if (pose == null) return;
            if (!string.IsNullOrEmpty(overrideName)) pose.poseName = overrideName;

            var defaultFileName = SanitizeAssetFileName(pose.poseName, "Pose");
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Pose", defaultFileName, "asset",
                "Choose where to save this pose", "Assets");

            if (string.IsNullOrEmpty(path)) return;

            // The user can still type an invalid name in the dialog; sanitize it into a Windows-safe file name.
            // Keep pose.poseName as-is (display name), only sanitize the asset file name on disk.
            path = SanitizeAssetPathFileName(path, "Pose");
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(pose, path);
            AssetDatabase.SaveAssets();

            if (ActiveLibrary != null)
            {
                ActiveLibrary.AddPose(pose);
                EditorUtility.SetDirty(ActiveLibrary);
                AssetDatabase.SaveAssets();
            }

            NotifyStateChanged();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        static string SanitizeAssetPathFileName(string assetPath, string fallbackFileName)
        {
            var dir = Path.GetDirectoryName(assetPath);
            var rawFileNameNoExt = Path.GetFileNameWithoutExtension(assetPath);

            // If the user entered something weird (or empty), still recover into a usable file name.
            var cleanFileNameNoExt = SanitizeAssetFileName(rawFileNameNoExt, fallbackFileName);
            var cleanDir = string.IsNullOrWhiteSpace(dir) ? "Assets" : dir.Replace('\\', '/');

            return $"{cleanDir}/{cleanFileNameNoExt}.asset";
        }

        static string SanitizeAssetFileName(string rawName, string fallbackName)
        {
            var name = (rawName ?? string.Empty).Trim();

            // Users sometimes include the extension manually (and Unity adds it again in the dialog UI).
            if (name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - ".asset".Length);

            if (string.IsNullOrWhiteSpace(name))
                name = fallbackName;

            // Windows invalid filename chars (includes ':' etc).
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (ch < 32)
                {
                    sb.Append('_');
                    continue;
                }

                if (Array.IndexOf(invalid, ch) >= 0)
                {
                    sb.Append('_');
                    continue;
                }

                sb.Append(ch);
            }

            name = sb.ToString().Trim();

            // Windows disallows trailing spaces/dots in filenames.
            name = name.TrimEnd(' ', '.');

            // Avoid special directory names.
            if (name == "." || name == "..")
                name = fallbackName;

            // Collapse repeated underscores a bit, so "A:::B" doesn't become "A___B".
            while (name.Contains("__"))
                name = name.Replace("__", "_");

            if (string.IsNullOrWhiteSpace(name))
                name = fallbackName;

            return name;
        }

        void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
