using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SnapPose
{
    [CreateAssetMenu(fileName = "NewPose", menuName = "SnapPose/Pose Data")]
    public class PoseData : ScriptableObject
    {
        [Header("Metadata")]
        public string poseName       = "New Pose";
        public string createdFrom    = "";
        public string description    = "";
        public RigType rigType       = RigType.Generic;
        public List<string> tags     = new List<string>();

        [Header("Bone Data")]
        public BoneTransformData[] bones = new BoneTransformData[0];

        public Dictionary<string, BoneTransformData> BuildLookup()
        {
            var dict = new Dictionary<string, BoneTransformData>(bones.Length);
            foreach (var b in bones)
                dict[b.bonePath] = b;
            return dict;
        }

        public PoseData Clone()
        {
            var clone = CreateInstance<PoseData>();
            clone.poseName    = poseName;
            clone.createdFrom = createdFrom;
            clone.description = description;
            clone.rigType     = rigType;
            clone.tags        = new List<string>(tags);
            clone.bones       = bones.Select(b => b.Clone()).ToArray();
            return clone;
        }
    }
}
