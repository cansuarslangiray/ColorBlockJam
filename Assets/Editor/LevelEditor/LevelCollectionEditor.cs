using Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    [CustomEditor(typeof(LevelCollection))]
    public sealed class LevelCollectionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var collection = (LevelCollection)target;
            if (collection == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Data Sync", EditorStyles.boldLabel);

            if (!GUILayout.Button("Refresh Level Collection", GUILayout.Height(24f)))
            {
                return;
            }

            LevelCollection.LevelCollectionRefreshReport report = collection.RefreshEditorAssetLists(logResult: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string title = report.ValidationIssueCount == 0
                ? "Refresh Completed"
                : "Refresh Completed (Warnings)";
            string detail =
                $"Levels: {report.LevelCount}\nShapes: {report.ShapeCount}\nValidation Warnings: {report.ValidationIssueCount}";

            const int maxIssuePreview = 6;
            if (report.ValidationIssueCount > 0)
            {
                int visibleIssueCount = Mathf.Min(maxIssuePreview, report.ValidationIssueCount);
                for (int i = 0; i < visibleIssueCount; i++)
                {
                    detail += $"\n- {report.ValidationIssues[i]}";
                }

                if (report.ValidationIssueCount > visibleIssueCount)
                {
                    detail += $"\n... +{report.ValidationIssueCount - visibleIssueCount} more";
                }
            }

            EditorUtility.DisplayDialog(title, detail, "Tamam");
            Debug.Log($"[LevelCollectionEditor] {title}\n{detail}", collection);
        }
    }
}
