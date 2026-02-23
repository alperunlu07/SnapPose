using UnityEngine.UIElements;

namespace SnapPose.Editor.UI
{
    public class HistoryPanel : VisualElement
    {
        readonly SnapPoseController _ctrl;
        VisualElement               _list;

        public HistoryPanel(SnapPoseController ctrl)
        {
            _ctrl = ctrl;
            _ctrl.OnStateChanged += Rebuild;
            BuildUI();
        }

        void BuildUI()
        {
            // Section header now provided by CollapsibleSection wrapper

            _list = new ScrollView(ScrollViewMode.Vertical);
            ((ScrollView)_list).style.maxHeight = 150;
            Add(_list);

            Rebuild();
        }

        void Rebuild()
        {
            _list.Clear();

            if (_ctrl.History.Count == 0)
            {
                var empty = new Label("No operations yet.");
                empty.AddToClassList("snap-label-secondary");
                empty.style.paddingTop = 8;
                empty.style.paddingBottom = 8;
                empty.style.paddingLeft = 8;
                empty.style.paddingRight = 8;

                _list.Add(empty);
                return;
            }

            for (int i = 0; i < _ctrl.History.Count; i++)
            {
                var entry    = _ctrl.History[i];
                var localIdx = i;

                var item = new VisualElement();
                item.AddToClassList("snap-history-item");

                // Mode dot
                var dot = new VisualElement
                {
                    style =
                    {
                        width           = 7,
                        height          = 7,
                        borderBottomLeftRadius    = new StyleLength(4f),
                        borderBottomRightRadius   = new StyleLength(4f),
                        borderTopLeftRadius       = new StyleLength(4f),
                        borderTopRightRadius      = new StyleLength(4f),
                        backgroundColor = entry.mode == ApplyMode.Permanent
                            ? SnapPoseStyles.Accent
                            : SnapPoseStyles.TextDisabled,
                        marginRight     = 8
                    }
                };
                item.Add(dot);

                // Info
                var col = new VisualElement { style = { flexGrow = 1 } };

                var nameLbl = new Label(entry.label);
                nameLbl.AddToClassList("snap-label-primary");
                col.Add(nameLbl);

                var metaLbl = new Label($"{entry.targetName}  •  {entry.timestamp:HH:mm:ss}  •  {entry.mode}");
                metaLbl.AddToClassList("snap-label-secondary");
                metaLbl.style.fontSize = 9;
                col.Add(metaLbl);

                item.Add(col);

                // Re-apply button
                var reBtn = SnapPoseStyles.MakeSecondaryButton("↺", () =>
                {
                    if (_ctrl.TargetObject == null || entry.poseSnapshot == null) return;
                    PoseApplicator.Apply(_ctrl.TargetObject, entry.poseSnapshot, null, 1f, entry.mode);
                });
                reBtn.tooltip = "Re-apply this pose";
                reBtn.style.marginLeft = 4;
                item.Add(reBtn);

                _list.Add(item);
            }
        }
    }
}
