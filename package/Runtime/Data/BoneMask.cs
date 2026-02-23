using System.Collections.Generic;
using UnityEngine;

namespace SnapPose
{
    [System.Serializable]
    public class BoneMask
    {
        public enum MaskMode { AllBones, IncludeList, ExcludeList }

        public MaskMode mode = MaskMode.AllBones;
        public List<string> bonePaths = new List<string>();

        public bool IsIncluded(string bonePath)
        {
            switch (mode)
            {
                case MaskMode.AllBones:    return true;
                case MaskMode.IncludeList: return bonePaths.Contains(bonePath);
                case MaskMode.ExcludeList: return !bonePaths.Contains(bonePath);
                default:                   return true;
            }
        }

        public BoneMask Clone()
        {
            var m = new BoneMask { mode = mode };
            m.bonePaths.AddRange(bonePaths);
            return m;
        }
    }
}
