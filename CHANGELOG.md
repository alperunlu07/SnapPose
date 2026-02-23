# Changelog

All notable changes to SnapPose will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-02-23

### Added
- Initial release of SnapPose
- Timeline scrubber with frame-by-frame preview
- Pose snapshot capture from animation clips
- Pose stack with multi-layer blending
- Bone mask editor with hierarchical tree view
- Alt+Click support for recursive collapse/expand
- Alt+Click on checkboxes to apply state to all children
- Diff view with before/after bone comparison
- Mirror functionality across X/Y/Z axes
- History tracking for pose operations
- Support for both Generic and Humanoid rigs
- Collapsible inspector panels for better UX
- Apply modes: Permanent (with Undo) and Preview Only
- Save poses as ScriptableObject assets
- Pose library management
- Humanoid muscle inspector

### Features
- **Timeline Scrubber**: Frame-accurate animation scrubbing with live preview
- **Pose Stack**: Layer and blend multiple poses with individual weights and masks
- **Bone Mask**: Hierarchical bone selection with collapse/expand functionality
- **Diff View**: Visual preview of transform changes before applying
- **Mirror**: Mirror poses across local axes with auto-detect bone pairs
- **History**: Track and re-apply recent pose operations
- **Collapsible UI**: Organize panels with expand/collapse functionality

### Technical
- Built with Unity UIToolkit
- Uses AnimationMode API for non-destructive preview
- Proper Undo/Redo support
- ScriptableObject-based pose storage
- Extensible architecture with static utility classes
