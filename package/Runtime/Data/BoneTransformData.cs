using UnityEngine;

namespace SnapPose
{
    [System.Serializable]
    public class BoneTransformData
    {
        public string bonePath;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public BoneTransformData() { }

        public BoneTransformData(string path, Transform t)
        {
            bonePath      = path;
            localPosition = t.localPosition;
            localRotation = t.localRotation;
            localScale    = t.localScale;
        }

        public BoneTransformData Clone() => new BoneTransformData
        {
            bonePath      = bonePath,
            localPosition = localPosition,
            localRotation = localRotation,
            localScale    = localScale
        };
    }
}
