# ◈ SnapPose

**A Unity Editor tool for capturing, composing, and applying bone poses from animation clips.**

Designed for VFX artists, animators, and technical artists who need precise control over character idle positions when composing scenes or designing effects.

---

## ✨ Features

| Feature | Description |
|---|---|
| **Timeline Scrubber** | Scrub any AnimationClip frame by frame with live Scene preview |
| **Pose Snapshot** | Capture the exact bone transforms at any frame |
| **Pose Stack** | Layer multiple poses with per-layer blend weights and bone masks |
| **Diff View** | Preview which bones will change before applying, with magnitude visualization |
| **Apply Modes** | Apply permanently (Undo supported) or preview-only |
| **Pose Library** | Save named poses as ScriptableObject assets with tags |
| **Mirror** | Mirror pose across any local axis (X, Y, Z) |
| **History** | Track recent operations and re-apply them |
| **Generic & Humanoid** | Works with both rig types using Unity's AnimationMode API |

---

## 📦 Installation

### Option 1: Unity Package Manager (Recommended)

1. Open Unity and go to **Window → Package Manager**
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL...**
4. Enter: `https://github.com/alperunlu07/SnapPose.git?path=package`
5. Click **Add**

Unity will automatically download and install SnapPose. Access it via **Tools → SnapPose** (or `Ctrl+Shift+P`).

### Option 2: Unity Asset Store

Get SnapPose from the Unity Asset Store:
🔗 **[SnapPose on Asset Store](https://assetstore.unity.com/packages/slug/363568)**

### Option 3: Manual Installation

1. Download or clone this repository
2. Copy the `SnapPose` folder into your project's `Assets` directory
3. Unity will compile the scripts automatically
4. Open via **Tools → SnapPose** (or `Ctrl+Shift+P`)

### Requirements
- Unity **2021.3 LTS** or newer (UIToolkit required)
- No additional packages needed

---

## 🚀 Quick Start

```
1. Open  Tools → SnapPose
2. Drag your rigged character from the Hierarchy into the WORKSPACE panel
3. Drag an AnimationClip into the Clip field
4. Scrub the timeline to the desired frame
5. Click ▶ Start Preview to see the pose live in Scene view
6. Choose Apply Permanently or Preview Only
7. Click ⚡ APPLY POSE
```

---

## 🧱 Architecture

```
SnapPose/
├── Runtime/
│   └── Data/
│       ├── PoseData.cs          ← ScriptableObject: stores bone transforms
│       ├── PoseLibrary.cs       ← ScriptableObject: collection of poses
│       ├── PoseLayerConfig.cs   ← Pose stack layer definition
│       ├── BoneMask.cs          ← Bone inclusion/exclusion mask
│       └── BoneTransformData.cs ← Single bone snapshot
│
└── Editor/
    ├── Core/
    │   ├── PoseSampler.cs       ← Samples AnimationClip via AnimationMode
    │   ├── PoseApplicator.cs    ← Writes pose to transforms, computes diffs
    │   └── SnapPoseController.cs ← Central state & event hub
    │
    └── UI/
        ├── SnapPoseWindow.cs    ← Main EditorWindow (UIToolkit)
        ├── SnapPoseStyles.cs    ← All USS styles + helper methods
        └── Panels/
            ├── WorkspacePanel.cs   ← Object list, drag-and-drop
            ├── InspectorPanel.cs   ← Source, blend, mask, action bar
            ├── PoseStackPanel.cs   ← Layered pose composer
            ├── DiffViewPanel.cs    ← Before/after bone diff
            ├── TimelineScrubber.cs ← Custom VisualElement scrubber
            └── HistoryPanel.cs     ← Operation history
```

---

## 💡 Tips

- **Multi-character workflow**: Add multiple objects to the Workspace panel and switch between them without losing your clip/frame selection.
- **Pose Stack**: Add the same clip at different frames as separate layers to blend between them — great for finding a pose between two keyframes.
- **Diff View**: Always run a diff before permanent apply on complex rigs to catch unexpected large rotations.
- **Mirror**: Sample a pose first (📷 Sample), then use Mirror to flip it to the opposite side bones.

---

## 🔧 Extending

`PoseSampler` and `PoseApplicator` are static utility classes with no UI dependencies. You can use them from your own editor scripts:

```csharp
using SnapPose.Editor;

// Sample a pose
var pose = PoseSampler.Sample(myCharacter, myClip, timeInSeconds);

// Apply it
PoseApplicator.Apply(myCharacter, pose, mask: null, blendWeight: 1f, ApplyMode.Permanent);

// Compute diff
var diffs = PoseApplicator.ComputeDiff(myCharacter, pose);
```

---

## 📝 License

MIT License — free for personal and commercial use.

---

## 🤝 Contributing

PRs welcome! Priority areas:
- Humanoid muscle-space inspector
- 3D skeleton overlay in SceneView
- Pose retargeting between different rigs
- Export to FBX/BVH

---

*Made with ♥ for the Unity community.*
