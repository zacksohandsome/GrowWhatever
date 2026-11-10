using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GrowWhatever.Editor
{
    [CustomEditor(typeof(FarmSession))]
    public sealed class FarmSessionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var debug = new Foldout { text = "Debug / Time Skip", value = true };
            root.Add(debug);
            debug.Add(new HelpBox("Play Mode only. Advances plant growth and processing, then saves the current farm.",
                HelpBoxMessageType.Info));

            var minutesProperty = serializedObject.FindProperty("debugSkipMinutes");
            var minutes = new DoubleField("Minutes to skip") { bindingPath = minutesProperty.propertyPath };
            debug.Add(minutes);
            var session = (FarmSession)target;
            var skip = new Button(() =>
            {
                serializedObject.ApplyModifiedProperties();
                session.DebugSkipTime(minutesProperty.doubleValue);
            }) { text = "Skip Time" };
            debug.Add(skip);

            void RefreshButton()
            {
                double seconds = minutes.value * 60;
                skip.SetEnabled(Application.isPlaying && session != null && session.Game != null && session.Ready &&
                    seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds));
            }

            minutes.RegisterValueChangedCallback(_ => RefreshButton());
            root.schedule.Execute(RefreshButton).Every(100);
            RefreshButton();
            return root;
        }
    }
}
