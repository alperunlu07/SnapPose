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

        // ── Target ───────────────────────────────────────────────────────────
        public void SetTarget(GameObject go)
        {
            StopPreview();
            TargetObject = go;
            NotifyStateChanged();
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
            OnPreviewChanged?.Invoke();
        }

        public void StopPreview()
        {
            StopPlayback();
            if (!IsPreviewActive) return;
            IsPreviewActive = false;
            _previewMask    = null;
            _restPose       = null;
            PoseSampler.StopPreview();
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

        // ── Playback ──────────────────────────────────────────────────────────
        public void StartPlayback(BoneMask mask = null)
        {
            if (TargetObject == null || SelectedClip == null) return;

            // Preview must be active to show playback in scene
            if (!IsPreviewActive) StartPreview(mask);
            else if (mask != null) { _previewMask = mask; }  // update mask on already-active preview

            IsPlaying       = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            OnPreviewChanged?.Invoke();
        }

        public void StopPlayback()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            EditorApplication.update -= OnEditorUpdate;
            OnPreviewChanged?.Invoke();
        }

        public void TogglePlayback()
        {
            if (IsPlaying) StopPlayback();
            else           StartPlayback();
        }

        void OnEditorUpdate()
        {
            if (!IsPlaying || SelectedClip == null || TargetObject == null)
            {
                StopPlayback();
                return;
            }

            double now   = EditorApplication.timeSinceStartup;
            float  delta = (float)(now - _lastEditorTime) * PlaybackSpeed;
            _lastEditorTime = now;

            float next = CurrentTime + delta;

            // Loop
            if (next >= SelectedClip.length)
                next = next % SelectedClip.length;

            CurrentTime = next;
            RefreshPreview();   // mask-aware refresh
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

            StopPreview();
            PoseApplicator.Apply(TargetObject, pose, mask, blendWeight, mode);

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
            StopPreview();
            PoseApplicator.ApplyStack(TargetObject, PoseStack, mode);
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
