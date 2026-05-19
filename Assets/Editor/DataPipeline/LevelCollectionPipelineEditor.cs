using Runtime.Data;
using Editor.LevelAuthoring;
using UnityEditor;
using UnityEngine;

namespace Editor.DataPipeline
{
    [CustomEditor(typeof(LevelCollection))]
    public sealed class LevelCollectionPipelineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var levelCollection = (LevelCollection)target;
            if (levelCollection == null)
            {
                return;
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Pipeline", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Level Editor", GUILayout.Height(24f)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Color Block Jam/Level Editor");
            }

            if (GUILayout.Button("Open Shape Editor", GUILayout.Height(24f)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Color Block Jam/Shape Editor");
            }

            if (GUILayout.Button("Sync Collection From Assets", GUILayout.Height(24f)))
            {
                LevelContentPipelineTool.SyncCollectionFromAssets();
            }

            if (GUILayout.Button("Create Empty Level Asset", GUILayout.Height(24f)))
            {
                LevelContentPipelineTool.CreateEmptyLevelAsset();
            }

            if (GUILayout.Button("Sync Shape Prefabs + Pools", GUILayout.Height(24f)))
            {
                BlockShapePrefabPipeline.SyncShapePrefabsAndPoolPrefabs();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Levels and shapes are sourced from ScriptableObject assets. Count is not fixed; add as many assets as you want.",
                MessageType.Info);
        }
    }
}
