using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SnapPose.Editor
{
    /// <summary>
    /// Samples an AnimationClip at a specific time and returns a PoseData snapshot.
    /// Works for both Generic and Humanoid rigs using AnimationMode.
    /// </summary>
    public static class PoseSampler
    {
        /// <summary>
        /// Samples the clip at the given time (in seconds) and captures all bone transforms.
        /// The object is left unchanged after sampling unless keepPreviewActive = true.
        /// </summary>
        public static PoseData Sample(
            GameObject root,
            AnimationClip clip,
            float timeSeconds,
            BoneMask mask = null,
            bool keepPreviewActive = false)
        {
            if (root == null || clip == null)
            {
                Debug.LogError("[SnapPose] SamplePose: root or clip is null.");
                return null;
            }

            bool wasInMode = AnimationMode.InAnimationMode();
            if (!wasInMode) AnimationMode.StartAnimationMode();

            AnimationMode.SampleAnimationClip(root, clip, timeSeconds);

            var pose         = ScriptableObject.CreateInstance<PoseData>();
            pose.poseName    = $"{clip.name} : Frame {TimeToFrame(clip, timeSeconds)}";
            pose.createdFrom = $"{clip.name} @ {timeSeconds:F3}s  (frame {TimeToFrame(clip, timeSeconds)})";
            pose.rigType     = DetectRigType(root);

            var boneList = new List<BoneTransformData>();
            var allBones = root.GetComponentsInChildren<Transform>(true);

            foreach (var bone in allBones)
            {
                var path = GetRelativePath(root.transform, bone);
                if (mask != null && !mask.IsIncluded(path)) continue;
                boneList.Add(new BoneTransformData(path, bone));
            }

            pose.bones = boneList.ToArray();

            if (!keepPreviewActive && !wasInMode)
                AnimationMode.StopAnimationMode();

            return pose;
        }

        /// <summary>
        /// Starts live preview mode: object tracks the animation while scrubbing.
        /// Call StopPreview() when done.
        /// </summary>
        public static void StartPreview(GameObject root, AnimationClip clip, float timeSeconds)
        {
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(root, clip, timeSeconds);
            SceneView.RepaintAll();
        }

        public static void UpdatePreview(GameObject root, AnimationClip clip, float timeSeconds)
        {
            if (!AnimationMode.InAnimationMode()) return;
            AnimationMode.SampleAnimationClip(root, clip, timeSeconds);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Like UpdatePreview but respects a BoneMask.
        /// Samples the full clip, then immediately overwrites excluded bones
        /// with their rest-pose transforms so they stay frozen during playback.
        /// Compatible with Unity 6.
        /// </summary>
        public static void UpdatePreviewWithMask(
            GameObject root,
            AnimationClip clip,
            float timeSeconds,
            BoneMask mask,
            Dictionary<string, BoneTransformData> restPose)
        {
            if (!AnimationMode.InAnimationMode()) return;

            // Sample the full clip normally
            AnimationMode.SampleAnimationClip(root, clip, timeSeconds);

            // Overwrite excluded bones back to rest pose directly on the transform.
            // AnimationMode doesn't prevent transform writes; the override will be
            // visible until the next SampleAnimationClip call, which we control.
            if (mask != null && mask.mode != BoneMask.MaskMode.AllBones && restPose != null)
            {
                foreach (var kvp in restPose)
                {
                    string path = kvp.Key;
                    if (mask.IsIncluded(path)) continue;   // bone is included in animation — leave it

                    var t = string.IsNullOrEmpty(path)
                        ? root.transform
                        : root.transform.Find(path);
                    if (t == null) continue;

                    var rest = kvp.Value;
                    t.localPosition = rest.localPosition;
                    t.localRotation = rest.localRotation;
                    t.localScale    = rest.localScale;
                }
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Captures all bone transforms from root as a rest-pose snapshot.
        /// Call this before StartPreview so excluded bones can be pinned during playback.
        /// </summary>
        public static Dictionary<string, BoneTransformData> CaptureRestPose(GameObject root)
        {
            var dict = new Dictionary<string, BoneTransformData>();
            if (root == null) return dict;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = GetRelativePath(root.transform, t);
                dict[path] = new BoneTransformData(path, t);
            }
            return dict;
        }

        public static void StopPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            SceneView.RepaintAll();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        public static int TimeToFrame(AnimationClip clip, float timeSeconds)
            => Mathf.RoundToInt(timeSeconds * clip.frameRate);

        public static float FrameToTime(AnimationClip clip, int frame)
            => frame / clip.frameRate;

        public static int TotalFrames(AnimationClip clip)
            => Mathf.RoundToInt(clip.length * clip.frameRate);

        public static RigType DetectRigType(GameObject root)
        {
            var anim = root.GetComponent<Animator>();
            if (anim == null) return RigType.Generic;
            return anim.isHuman ? RigType.Humanoid : RigType.Generic;
        }

        public static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;
            var path   = target.name;
            var parent = target.parent;
            while (parent != null && parent != root)
            {
                path   = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>Collects all bone paths from a root transform.</summary>
        public static List<string> GetAllBonePaths(GameObject root)
        {
            var paths = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                paths.Add(GetRelativePath(root.transform, t));
            return paths;
        }
    }
}
