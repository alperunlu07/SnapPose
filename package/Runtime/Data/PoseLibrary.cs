using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SnapPose
{
    [CreateAssetMenu(fileName = "NewPoseLibrary", menuName = "SnapPose/Pose Library")]
    public class PoseLibrary : ScriptableObject
    {
        public string       libraryName = "My Pose Library";
        public List<PoseData> poses     = new List<PoseData>();

        public IEnumerable<PoseData> FilterByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return poses;
            return poses.Where(p => p.tags.Contains(tag));
        }

        public IEnumerable<string> GetAllTags()
        {
            var tags = new HashSet<string>();
            foreach (var pose in poses)
                foreach (var tag in pose.tags)
                    tags.Add(tag);
            return tags;
        }

        public void AddPose(PoseData pose)
        {
            if (!poses.Contains(pose))
                poses.Add(pose);
        }

        public void RemovePose(PoseData pose)
        {
            poses.Remove(pose);
        }
    }
}
