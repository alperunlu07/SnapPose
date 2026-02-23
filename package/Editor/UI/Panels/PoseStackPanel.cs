using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    public class PoseStackPanel : VisualElement
    {
        readonly SnapPoseController _ctrl;
        VisualElement               _layerList;

        public PoseStackPanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnStateChanged += Rebuild;
            BuildUI();
        }

        void BuildUI()
        {
            // Layer list (section header now provided by CollapsibleSection wrapper)
            _layerList = new VisualElement();
            _layerList.style.marginBottom = 4;
            Add(_layerList);

            // Buttons row
            var btns = new VisualElement
                { style = { flexDirection = FlexDirection.Row, paddingTop = 4, paddingBottom = 4, paddingLeft = 4, paddingRight = 4 } };

            var addBtn = SnapPoseStyles.MakeSecondaryButton("＋ Add Layer", () =>
            {
                _ctrl.AddLayer(null);
            });
            addBtn.style.flexGrow = 1;

            var flatBtn = SnapPoseStyles.MakeSecondaryButton("⬇ Flatten", () =>
            {
                var preview = _ctrl.PoseStack.Count > 0;
                _ctrl.ApplyStack(ApplyMode.Permanent);
            });
            flatBtn.tooltip = "Merge all layers and apply permanently";

            btns.Add(addBtn);
            btns.Add(flatBtn);
            Add(btns);

            Rebuild();
        }

        void Rebuild()
        {
            _layerList.Clear();

            if (_ctrl.PoseStack.Count == 0)
            {
                var empty = new Label("No layers. Add a sampled pose as a layer.");
                empty.AddToClassList("snap-label-secondary");
                empty.style.paddingTop = 8;
                empty.style.paddingBottom = 8;
                empty.style.paddingLeft = 8;
                empty.style.paddingRight = 8;
                _layerList.Add(empty);
                return;
            }

            for (int i = 0; i < _ctrl.PoseStack.Count; i++)
            {
                var item = BuildLayerItem(i);
                _layerList.Add(item);
            }
        }

        VisualElement BuildLayerItem(int idx)
        {
            var layer = _ctrl.PoseStack[idx];

            var card = new VisualElement();
            card.AddToClassList("snap-layer-item");

            // ── Top row: toggle + name + move + delete ─────────────────────
            var top = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var toggle = new UnityEngine.UIElements.Toggle();
            toggle.value = layer.enabled;
            toggle.RegisterValueChangedCallback(e => { layer.enabled = e.newValue; });
            toggle.style.marginRight = 6;
            top.Add(toggle);

            // Accent stripe color based on blend
            var stripe = new VisualElement
            {
                style =
                {
                    width           = 3,
                    height          = 14,
                    borderBottomLeftRadius    = new StyleLength(2f),
                    borderTopLeftRadius       = new StyleLength(2f),
                    backgroundColor = Color.Lerp(SnapPoseStyles.TextDisabled, SnapPoseStyles.Accent, layer.blendWeight),
                    marginRight     = 7
                }
            };
            top.Add(stripe);

            var nameField = new UnityEngine.UIElements.TextField();
            nameField.value = layer.layerName;
            nameField.AddToClassList("snap-label-primary");
            nameField.style.flexGrow   = 1;
            nameField.style.marginRight = 4;
            nameField.RegisterValueChangedCallback(e => layer.layerName = e.newValue);
            top.Add(nameField);

            // Move up/down
            if (idx > 0)
            {
                var upBtn = SnapPoseStyles.MakeIconButton("↑", () => { _ctrl.MoveLayer(idx, idx - 1); }, "Move up");
                top.Add(upBtn);
            }
            if (idx < _ctrl.PoseStack.Count - 1)
            {
                var downBtn = SnapPoseStyles.MakeIconButton("↓", () => { _ctrl.MoveLayer(idx, idx + 1); }, "Move down");
                top.Add(downBtn);
            }

            var delBtn = SnapPoseStyles.MakeIconButton("✕", () => { _ctrl.RemoveLayer(idx); }, "Remove layer");
            delBtn.style.color = SnapPoseStyles.DangerColor;
            top.Add(delBtn);

            card.Add(top);

            // ── Pose reference ────────────────────────────────────────────────
            var poseRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };

            var poseLbl = new Label("Pose");
            poseLbl.AddToClassList("snap-label-secondary");
            poseLbl.style.width    = 40;
            poseLbl.style.minWidth = 40;
            poseRow.Add(poseLbl);

            var poseField = new ObjectField
            {
                objectType        = typeof(PoseData),
                allowSceneObjects = false,
                value             = layer.sourcePose
            };
            poseField.style.flexGrow = 1;
            poseField.RegisterValueChangedCallback(e => { layer.sourcePose = e.newValue as PoseData; });
            poseRow.Add(poseField);
            card.Add(poseRow);

            // ── Blend slider ──────────────────────────────────────────────────
            var blendRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };

            var blendLbl = new Label("Blend");
            blendLbl.AddToClassList("snap-label-secondary");
            blendLbl.style.width    = 40;
            blendLbl.style.minWidth = 40;
            blendRow.Add(blendLbl);

            var blendSlider = new Slider(0f, 1f);
            blendSlider.value    = layer.blendWeight;
            blendSlider.style.flexGrow = 1;
            blendSlider.RegisterValueChangedCallback(e =>
            {
                layer.blendWeight = e.newValue;
                stripe.style.backgroundColor =
                    Color.Lerp(SnapPoseStyles.TextDisabled, SnapPoseStyles.Accent, e.newValue);
            });
            blendRow.Add(blendSlider);

            var blendPct = new Label($"{(int)(layer.blendWeight * 100)}%");
            blendPct.AddToClassList("snap-label-accent");
            blendPct.style.width    = 34;
            blendPct.style.minWidth = 34;
            blendSlider.RegisterValueChangedCallback(e =>
                blendPct.text = $"{(int)(e.newValue * 100)}%");
            blendRow.Add(blendPct);
            card.Add(blendRow);

            // ── Mask editor ───────────────────────────────────────────────────
            var maskHeader = new Label("Bone Mask");
            maskHeader.AddToClassList("snap-label-secondary");
            maskHeader.style.marginTop  = 6;
            maskHeader.style.marginLeft = 2;
            card.Add(maskHeader);

            var maskEditor = new BoneMaskEditorPanel(_ctrl, layer.mask);
            card.Add(maskEditor);

            return card;
        }
    }
}
