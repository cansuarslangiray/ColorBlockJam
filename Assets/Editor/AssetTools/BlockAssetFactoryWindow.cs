using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEditor;
using UnityEngine;

namespace Editor.AssetTools
{
    public class BlockAssetFactoryWindow : EditorWindow
    {
        private const string DefaultRootPath = "Assets/Art/GeneratedBlocks";

        private string _outputRootPath = DefaultRootPath;
        private bool _generatePrefabs = true;
        private bool _overwriteExistingAssets = true;
        private bool _generateVisualProfile = true;

        [MenuItem("Tools/Color Block Jam/Block Asset Factory")]
        private static void OpenWindow()
        {
            var window = GetWindow<BlockAssetFactoryWindow>();
            window.titleContent = new GUIContent("Block Factory");
            window.minSize = new Vector2(420f, 220f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity Built-in Block Asset Factory", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _outputRootPath = EditorGUILayout.TextField("Output Root", _outputRootPath);
            _generatePrefabs = EditorGUILayout.Toggle("Generate Prefabs", _generatePrefabs);
            _generateVisualProfile = EditorGUILayout.Toggle("Generate Visual Profile", _generateVisualProfile);
            _overwriteExistingAssets = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExistingAssets);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Block Assets", GUILayout.Height(36f)))
            {
                GenerateAssets();
            }

            EditorGUILayout.HelpBox(
                "Bu araç dış asset kullanmadan Unity primitive cube + Unity material ile blok assetleri üretir.",
                MessageType.Info);
        }

        private void GenerateAssets()
        {
            if (string.IsNullOrWhiteSpace(_outputRootPath))
            {
                EditorUtility.DisplayDialog("Invalid Path", "Output root boş olamaz.", "OK");
                return;
            }

            string materialsFolder = _outputRootPath + "/Materials";
            string prefabsFolder = _outputRootPath + "/Prefabs";

            EnsureFolder(_outputRootPath);
            EnsureFolder(materialsFolder);

            if (_generatePrefabs)
            {
                EnsureFolder(prefabsFolder);
            }

            List<BlockColorMaterialEntry> entries = new List<BlockColorMaterialEntry>();
            GameObject firstPrefab = null;

            BlockColor[] allColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
            for (int i = 0; i < allColors.Length; i++)
            {
                BlockColor colorType = allColors[i];
                Material material = CreateOrUpdateMaterial(materialsFolder, colorType);

                entries.Add(new BlockColorMaterialEntry
                {
                    colorType = colorType,
                    material = material
                });

                if (_generatePrefabs)
                {
                    GameObject prefab = CreateOrUpdatePrefab(prefabsFolder, colorType, material);
                    if (firstPrefab == null)
                    {
                        firstPrefab = prefab;
                    }
                }
            }

            if (_generateVisualProfile)
            {
                CreateOrUpdateVisualProfile(_outputRootPath, entries, firstPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Completed", "Block assetleri başarıyla oluşturuldu.", "OK");
        }

        private Material CreateOrUpdateMaterial(string folderPath, BlockColor colorType)
        {
            string materialPath = folderPath + "/MAT_Block_" + colorType + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                material = new Material(shader)
                {
                    name = "MAT_Block_" + colorType
                };

                AssetDatabase.CreateAsset(material, materialPath);
            }

            if (_overwriteExistingAssets)
            {
                material.color = BlockColorUtility.GetColor(colorType);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private GameObject CreateOrUpdatePrefab(string folderPath, BlockColor colorType, Material material)
        {
            string prefabPath = folderPath + "/PF_Block_" + colorType + ".prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (existingPrefab != null && !_overwriteExistingAssets)
            {
                return existingPrefab;
            }

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "PF_Block_" + colorType;
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            temp.GetComponent<Renderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            DestroyImmediate(temp);
            return prefab;
        }

        private void CreateOrUpdateVisualProfile(
            string rootFolder,
            List<BlockColorMaterialEntry> entries,
            GameObject firstPrefab)
        {
            string profilePath = rootFolder + "/BlockVisualProfile.asset";
            BlockVisualProfile profile = AssetDatabase.LoadAssetAtPath<BlockVisualProfile>(profilePath);

            if (profile == null)
            {
                profile = CreateInstance<BlockVisualProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            profile.materialsByColor = entries;
            if (firstPrefab != null)
            {
                profile.defaultBlockPrefab = firstPrefab;
            }

            EditorUtility.SetDirty(profile);
        }

        private void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] split = folderPath.Split('/');
            string current = split[0];

            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }
    }
}
