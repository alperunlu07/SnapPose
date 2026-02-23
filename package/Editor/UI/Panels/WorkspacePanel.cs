using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    public class WorkspacePanel : VisualElement
    {
        readonly SnapPoseController    _ctrl;
        readonly List<GameObject>      _objects    = new List<GameObject>();
        int                            _selectedIdx = -1;

        VisualElement _listContainer;
        Label         _emptyLabel;

        public WorkspacePanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            style.flexDirection = FlexDirection.Column;
            style.flexGrow      = 1;

            BuildUI();
        }

        void BuildUI()
        {
            // ── Header ───────────────────────────────────────────────────────
            Add(SnapPoseStyles.MakeSectionHeader("WORKSPACE", "◈"));

            // ── Drop zone hint ────────────────────────────────────────────────
            var dropHint = new Label("Drop rigs here  ↓");
            dropHint.AddToClassList("snap-label-secondary");
            dropHint.style.unityTextAlign        = TextAnchor.MiddleCenter;
            dropHint.style.paddingTop       = 5;
            dropHint.style.paddingBottom    = 5;
            dropHint.style.color            = SnapPoseStyles.TextDisabled;

            // Enable drag-and-drop onto this panel
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);

            Add(dropHint);
            Add(SnapPoseStyles.MakeSeparator());

            // ── List ─────────────────────────────────────────────────────────
            _listContainer = new VisualElement();
            _listContainer.style.flexGrow = 1;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("snap-scrollview");
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;   // Allow shrinking when needed
            scroll.style.minHeight = 0;    // Can shrink below content height
            scroll.Add(_listContainer);
            Add(scroll);

            _emptyLabel = new Label("No objects added.\nDrag GameObjects here\nor use + below.");
            _emptyLabel.AddToClassList("snap-label-secondary");
            _emptyLabel.style.whiteSpace    = WhiteSpace.Normal;
            _emptyLabel.style.unityTextAlign     = TextAnchor.MiddleCenter;
            _emptyLabel.style.paddingTop    = 20;
            _emptyLabel.style.paddingLeft   = 10;
            _emptyLabel.style.paddingRight  = 10;
            _listContainer.Add(_emptyLabel);

            // ── Footer ────────────────────────────────────────────────────────
            Add(SnapPoseStyles.MakeSeparator());

            var footer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, paddingTop = 4, paddingBottom = 4, paddingLeft = 4, paddingRight = 4 }
            };

            var addBtn = SnapPoseStyles.MakeSecondaryButton("＋ Add Selected", AddSelectedObject);
            addBtn.style.flexGrow = 1;
            footer.Add(addBtn);

            var clearBtn = SnapPoseStyles.MakeIconButton("🗑", ClearAll, "Remove all");
            footer.Add(clearBtn);

            Add(footer);

            // ── Library Picker ────────────────────────────────────────────────
            Add(SnapPoseStyles.MakeSeparator());
            Add(SnapPoseStyles.MakeSectionHeader("POSE LIBRARY", "📚"));

            var libRow = new VisualElement { style = { paddingTop = 6 , paddingBottom = 6, paddingLeft = 4, paddingRight = 4 } };
            var libField = new ObjectField("Library")
            {
                objectType     = typeof(PoseLibrary),
                allowSceneObjects = false
            };
            libField.AddToClassList("snap-label-primary");
            libField.RegisterValueChangedCallback(e =>
            {
                _ctrl.ActiveLibrary = e.newValue as PoseLibrary;
            });

            var newLibBtn = SnapPoseStyles.MakeSecondaryButton("New", CreateNewLibrary);
            newLibBtn.style.marginTop = 4;

            libRow.Add(libField);
            libRow.Add(newLibBtn);
            Add(libRow);
        }

        // ── List management ───────────────────────────────────────────────────
        void AddSelectedObject()
        {
            foreach (var go in Selection.gameObjects)
                TryAddObject(go);
        }

        void TryAddObject(GameObject go)
        {
            if (go == null || _objects.Contains(go)) return;
            _objects.Add(go);
            RebuildList();
            SelectObject(_objects.Count - 1);
        }

        void RemoveObject(int idx)
        {
            if (idx < 0 || idx >= _objects.Count) return;
            _objects.RemoveAt(idx);
            if (_selectedIdx >= _objects.Count) _selectedIdx = _objects.Count - 1;
            RebuildList();
            if (_selectedIdx >= 0) _ctrl.SetTarget(_objects[_selectedIdx]);
            else _ctrl.SetTarget(null);
        }

        void ClearAll()
        {
            _objects.Clear();
            _selectedIdx = -1;
            _ctrl.SetTarget(null);
            RebuildList();
        }

        void SelectObject(int idx)
        {
            _selectedIdx = idx;
            _ctrl.SetTarget(idx >= 0 && idx < _objects.Count ? _objects[idx] : null);
            RebuildList();
        }

        void RebuildList()
        {
            _listContainer.Clear();
            _emptyLabel.style.display = _objects.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _listContainer.Add(_emptyLabel);

            for (int i = 0; i < _objects.Count; i++)
            {
                int localIdx = i;
                var go       = _objects[i];
                if (go == null) continue;

                var rig = PoseSampler.DetectRigType(go);

                var item = new VisualElement();
                item.AddToClassList("snap-object-item");
                if (localIdx == _selectedIdx) item.AddToClassList("snap-object-item--selected");

                // Rig-type dot
                var dot = new VisualElement();
                dot.AddToClassList("snap-object-dot");
                dot.AddToClassList(rig == RigType.Humanoid
                    ? "snap-object-dot--humanoid"
                    : "snap-object-dot--generic");
                item.Add(dot);

                // Name + rig badge
                var col = new VisualElement { style = { flexGrow = 1 } };
                var nameLbl = new Label(go.name);
                nameLbl.AddToClassList("snap-label-primary");

                var rigBadge = new Label(rig.ToString());
                rigBadge.AddToClassList("snap-rig-badge");
                rigBadge.AddToClassList(rig == RigType.Humanoid
                    ? "snap-rig-badge--humanoid"
                    : "snap-rig-badge--generic");

                var nameRow = new VisualElement
                    { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                nameRow.Add(nameLbl);
                nameRow.Add(rigBadge);
                col.Add(nameRow);
                item.Add(col);

                // Remove button
                var rmBtn = SnapPoseStyles.MakeIconButton("✕", () => RemoveObject(localIdx), "Remove");
                item.Add(rmBtn);

                item.RegisterCallback<ClickEvent>(_ => SelectObject(localIdx));
                _listContainer.Add(item);
            }
        }

        // ── Drag & Drop ───────────────────────────────────────────────────────
        void OnDragUpdated(DragUpdatedEvent e)
        {
            bool hasGO = false;
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is GameObject) { hasGO = true; break; }
            DragAndDrop.visualMode = hasGO ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            e.StopPropagation();
        }

        void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is GameObject go)
                    TryAddObject(go);
            e.StopPropagation();
        }

        // ── Library ───────────────────────────────────────────────────────────
        void CreateNewLibrary()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "New Pose Library", "MyPoseLibrary", "asset",
                "Create a new Pose Library asset");
            if (string.IsNullOrEmpty(path)) return;
            var lib = ScriptableObject.CreateInstance<PoseLibrary>();
            AssetDatabase.CreateAsset(lib, path);
            AssetDatabase.SaveAssets();
            _ctrl.ActiveLibrary = lib;
        }
    }
}
