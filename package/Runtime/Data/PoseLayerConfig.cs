using UnityEngine;

namespace SnapPose
{
    [System.Serializable]
    public class PoseLayerConfig
    {
        public string    layerName   = "Layer";
        public PoseData  sourcePose;
        public BoneMask  mask        = new BoneMask();
        [Range(0f, 1f)]
        public float     blendWeight = 1f;
        public bool      enabled     = true;
    }
}
