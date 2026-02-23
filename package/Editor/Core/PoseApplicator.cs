using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SnapPose.Editor
{
    public enum ApplyMode { Preview, Permanent }

    public static class PoseApplicator
    {
        /// <summary>
        /// Applies a single PoseData to a root GameObject.
        /// If mode = Permanent, registers an Undo operation.
        /// </summary>
        public static void Apply(
            GameObject root,
            PoseData pose,
            BoneMask mask = null,
            float blendWeight = 1f,
            ApplyMode mode = ApplyMode.Permanent)
        {
            if (root == null || pose == null) return;

            var lookup = BuildTransformLookup(root);
            var boneLookup = pose.BuildLookup();

            if (mode == ApplyMode.Permanent)
            {
                var transforms = new List<Object>();
                foreach (var bone in pose.bones)
                {
                    if (mask != null && !mask.IsIncluded(bone.bonePath)) continue;
                    if (lookup.TryGetValue(bone.bonePath, out var t))
                        transforms.Add(t);
                }
                Undo.RecordObjects(transforms.ToArray(), $"Apply Pose: {pose.poseName}");
            }

            foreach (var boneData in pose.bones)
            {
                if (mask != null && !mask.IsIncluded(boneData.bonePath)) continue;
                if (!lookup.TryGetValue(boneData.bonePath, out var t)) continue;

                if (Mathf.Approximately(blendWeight, 1f))
                {
                    t.localPosition = boneData.localPosition;
                    t.localRotation = boneData.localRotation;
                    t.localScale    = boneData.localScale;
                }
                else
                {
                    t.localPosition = Vector3.Lerp(t.localPosition, boneData.localPosition, blendWeight);
                    t.localRotation = Quaternion.Slerp(t.localRotation, boneData.localRotation, blendWeight);
                    t.localScale    = Vector3.Lerp(t.localScale, boneData.localScale, blendWeight);
                }

                if (mode == ApplyMode.Permanent)
                    EditorUtility.SetDirty(t);
            }
        }

        /// <summary>
        /// Applies multiple PoseLayerConfigs in order (stack-based).
        /// Each layer is applied on top of the previous ones.
        /// </summary>
        public static void ApplyStack(
            GameObject root,
            IList<PoseLayerConfig> layers,
            ApplyMode mode = ApplyMode.Permanent)
        {
            if (root == null) return;

            if (mode == ApplyMode.Permanent)
                Undo.RecordObjects(root.GetComponentsInChildren<Transform>(true), "Apply Pose Stack");

            var lookup = BuildTransformLookup(root);

            // First, capture current transforms as baseline
            var baseline = CaptureCurrent(root);

            foreach (var layer in layers)
            {
                if (!layer.enabled || layer.sourcePose == null) continue;
                ApplyLayerToLookup(lookup, baseline, layer);
            }

            if (mode == ApplyMode.Permanent)
                EditorUtility.SetDirty(root);
        }

        // ── Mirror ────────────────────────────────────────────────────────────

        public enum MirrorAxis { X, Y, Z }

        /// <summary>
        /// Mirrors pose using a <see cref="BoneMirrorMap"/>: for each enabled pair,
        /// reads the source bone from <paramref name="pose"/> and writes the
        /// mirrored rotation to the target bone on the live rig.
        /// Also optionally mirrors the source back onto itself for symmetrical bones.
        /// </summary>
        public static void MirrorPoseWithMap(
            GameObject    root,
            PoseData      pose,
            BoneMirrorMap map,
            MirrorAxis    axis,
            float         blendWeight = 1f,
            ApplyMode     mode        = ApplyMode.Permanent)
        {
            if (root == null || pose == null || map == null) return;

            var lookup     = BuildTransformLookup(root);
            var boneLookup = pose.BuildLookup();
            var pairLookup = map.BuildLookup(); // source → target

            if (mode == ApplyMode.Permanent)
                Undo.RecordObjects(root.GetComponentsInChildren<Transform>(true), "Mirror Pose");

            foreach (var kvp in pairLookup)
            {
                string srcPath = kvp.Key;
                string tgtPath = kvp.Value;

                if (!boneLookup.TryGetValue(srcPath, out var srcBone)) continue;
                if (!lookup.TryGetValue(tgtPath, out var tgtTransform))  continue;

                var mirroredRot = MirrorRotation(srcBone.localRotation, axis);
                var mirroredPos = MirrorPosition(srcBone.localPosition, axis);

                if (Mathf.Approximately(blendWeight, 1f))
                {
                    tgtTransform.localRotation = mirroredRot;
                    tgtTransform.localPosition = mirroredPos;
                }
                else
                {
                    tgtTransform.localRotation = Quaternion.Slerp(tgtTransform.localRotation, mirroredRot, blendWeight);
                    tgtTransform.localPosition = Vector3.Lerp(tgtTransform.localPosition, mirroredPos, blendWeight);
                }

                if (mode == ApplyMode.Permanent)
                    EditorUtility.SetDirty(tgtTransform);
            }
        }

        /// <summary>Legacy mirror: mirrors rotation of each bone in sourceMask onto itself.</summary>
        public static void MirrorPose(
            GameObject root,
            PoseData pose,
            MirrorAxis axis,
            BoneMask sourceMask,
            BoneMask targetMask,
            ApplyMode mode = ApplyMode.Permanent)
        {
            if (root == null || pose == null) return;

            var lookup = BuildTransformLookup(root);

            if (mode == ApplyMode.Permanent)
                Undo.RecordObjects(root.GetComponentsInChildren<Transform>(true), "Mirror Pose");

            foreach (var boneData in pose.bones)
            {
                if (!sourceMask.IsIncluded(boneData.bonePath)) continue;
                if (!lookup.TryGetValue(boneData.bonePath, out var t)) continue;

                var mirrored = MirrorRotation(boneData.localRotation, axis);
                t.localRotation = mirrored;

                if (mode == ApplyMode.Permanent)
                    EditorUtility.SetDirty(t);
            }
        }

        static Vector3 MirrorPosition(Vector3 pos, MirrorAxis axis)
        {
            switch (axis)
            {
                case MirrorAxis.X: return new Vector3(-pos.x,  pos.y,  pos.z);
                case MirrorAxis.Y: return new Vector3( pos.x, -pos.y,  pos.z);
                case MirrorAxis.Z: return new Vector3( pos.x,  pos.y, -pos.z);
                default:           return pos;
            }
        }

        // ── Diff ─────────────────────────────────────────────────────────────

        public struct BoneDiff
        {
            public string  bonePath;
            public Vector3    positionDelta;
            public Vector3    rotationDelta;   // Euler degrees
            public Vector3    scaleDelta;
            public float      magnitude;
        }

        public static List<BoneDiff> ComputeDiff(GameObject root, PoseData targetPose, BoneMask mask = null)
        {
            var diffs  = new List<BoneDiff>();
            var lookup = BuildTransformLookup(root);

            foreach (var boneData in targetPose.bones)
            {
                if (mask != null && !mask.IsIncluded(boneData.bonePath)) continue;
                if (!lookup.TryGetValue(boneData.bonePath, out var t)) continue;

                var posDelta = boneData.localPosition - t.localPosition;
                var rotDelta = (boneData.localRotation * Quaternion.Inverse(t.localRotation)).eulerAngles;
                // Normalize angles to -180..180
                rotDelta = new Vector3(
                    NormalizeAngle(rotDelta.x),
                    NormalizeAngle(rotDelta.y),
                    NormalizeAngle(rotDelta.z));
                var sclDelta = boneData.localScale - t.localScale;

                float mag = posDelta.magnitude + rotDelta.magnitude * 0.01f;

                if (mag > 0.0001f || sclDelta.magnitude > 0.0001f)
                {
                    diffs.Add(new BoneDiff
                    {
                        bonePath      = boneData.bonePath,
                        positionDelta = posDelta,
                        rotationDelta = rotDelta,
                        scaleDelta    = sclDelta,
                        magnitude     = mag
                    });
                }
            }

            diffs.Sort((a, b) => b.magnitude.CompareTo(a.magnitude));
            return diffs;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        static Dictionary<string, Transform> BuildTransformLookup(GameObject root)
        {
            var dict = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                dict[PoseSampler.GetRelativePath(root.transform, t)] = t;
            return dict;
        }

        static Dictionary<string, BoneTransformData> CaptureCurrent(GameObject root)
        {
            var dict = new Dictionary<string, BoneTransformData>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = PoseSampler.GetRelativePath(root.transform, t);
                dict[path] = new BoneTransformData(path, t);
            }
            return dict;
        }

        static void ApplyLayerToLookup(
            Dictionary<string, Transform> lookup,
            Dictionary<string, BoneTransformData> baseline,
            PoseLayerConfig layer)
        {
            foreach (var boneData in layer.sourcePose.bones)
            {
                if (!layer.mask.IsIncluded(boneData.bonePath)) continue;
                if (!lookup.TryGetValue(boneData.bonePath, out var t)) continue;

                var basePos = baseline.TryGetValue(boneData.bonePath, out var b)
                    ? b.localPosition : t.localPosition;
                var baseRot = baseline.TryGetValue(boneData.bonePath, out var bb)
                    ? bb.localRotation : t.localRotation;
                var baseScl = baseline.TryGetValue(boneData.bonePath, out var bbb)
                    ? bbb.localScale : t.localScale;

                t.localPosition = Vector3.Lerp(basePos, boneData.localPosition, layer.blendWeight);
                t.localRotation = Quaternion.Slerp(baseRot, boneData.localRotation, layer.blendWeight);
                t.localScale    = Vector3.Lerp(baseScl, boneData.localScale, layer.blendWeight);
            }
        }

        static Quaternion MirrorRotation(Quaternion q, MirrorAxis axis)
        {
            switch (axis)
            {
                case MirrorAxis.X: return new Quaternion(-q.x,  q.y,  q.z, -q.w);
                case MirrorAxis.Y: return new Quaternion( q.x, -q.y,  q.z, -q.w);
                case MirrorAxis.Z: return new Quaternion( q.x,  q.y, -q.z, -q.w);
                default:           return q;
            }
        }

        static float NormalizeAngle(float angle)
        {
            while (angle >  180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
