using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace StrawberryBushTool
{
    /// <summary>
    /// Editor window for generating and spawning strawberry bushes in the scene.
    /// Accessible via Tools > Strawberry Bush Generator menu.
    /// </summary>
    public class StrawberryBushGenerator : EditorWindow
    {
        private StrawberryBushConfig config;
        private GameObject templatePrefab;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Strawberry Bush Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<StrawberryBushGenerator>("Strawberry Bush Generator");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
            LoadTemplatePrefab();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Strawberry Bush Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Configuration Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            var newConfig = (StrawberryBushConfig)EditorGUILayout.ObjectField(
                "Config Asset", 
                config, 
                typeof(StrawberryBushConfig), 
                false
            );
            
            if (newConfig != config)
            {
                config = newConfig;
                SaveConfigPath();
            }

            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "No configuration found. Click 'Create Config' to create one in Assets/Settings/", 
                    MessageType.Warning
                );
                
                if (GUILayout.Button("Create Config", GUILayout.Height(30)))
                {
                    CreateConfig();
                }
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("FBX Path:", config.fbxModelPath, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Material Path:", config.materialPath, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Texture Path:", config.texturePath, EditorStyles.wordWrappedLabel);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Template Prefab Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Template Prefab", EditorStyles.boldLabel);
            
            if (templatePrefab != null)
            {
                EditorGUILayout.HelpBox(
                    $"Template prefab '{config.templatePrefabName}' is ready.", 
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Template prefab not found. Generate it first before spawning bushes.", 
                    MessageType.Warning
                );
            }

            GUI.enabled = config != null;
            if (GUILayout.Button("Generate Template Prefab", GUILayout.Height(35)))
            {
                GenerateTemplatePrefab();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Spawn Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Spawn Bush", EditorStyles.boldLabel);
            
            GUI.enabled = templatePrefab != null;
            if (GUILayout.Button("Spawn Bush at Scene Center", GUILayout.Height(40)))
            {
                SpawnBushAtCenter();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void LoadConfig()
        {
            string configPath = EditorPrefs.GetString("StrawberryBushGenerator_ConfigPath", "");
            
            if (!string.IsNullOrEmpty(configPath))
            {
                config = AssetDatabase.LoadAssetAtPath<StrawberryBushConfig>(configPath);
            }

            if (config == null)
            {
                // Try to find existing config
                string[] guids = AssetDatabase.FindAssets("t:StrawberryBushConfig");
                if (guids.Length > 0)
                {
                    configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<StrawberryBushConfig>(configPath);
                    SaveConfigPath();
                }
            }
        }

        private void SaveConfigPath()
        {
            if (config != null)
            {
                string path = AssetDatabase.GetAssetPath(config);
                EditorPrefs.SetString("StrawberryBushGenerator_ConfigPath", path);
            }
        }

        private void CreateConfig()
        {
            string settingsDir = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(settingsDir))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            config = CreateInstance<StrawberryBushConfig>();
            string assetPath = $"{settingsDir}/StrawberryBushConfig.asset";
            
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            SaveConfigPath();
            
            EditorUtility.DisplayDialog(
                "Config Created", 
                $"Configuration created at {assetPath}", 
                "OK"
            );
        }

        private void LoadTemplatePrefab()
        {
            if (config != null)
            {
                templatePrefab = Resources.Load<GameObject>(config.templatePrefabName);
            }
        }

        private void GenerateTemplatePrefab()
        {
            if (config == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a configuration first.", "OK");
                return;
            }

            // Load the FBX model
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(config.fbxModelPath);
            if (fbxModel == null)
            {
                EditorUtility.DisplayDialog(
                    "Error", 
                    $"Could not load FBX model at path: {config.fbxModelPath}", 
                    "OK"
                );
                return;
            }

            // Load material and texture
            Material material = AssetDatabase.LoadAssetAtPath<Material>(config.materialPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(config.texturePath);

            // Instantiate the FBX
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            instance.name = "StrawberryBush_Template";

            // Remove excluded objects (Camera, Light, etc.)
            RemoveExcludedObjects(instance.transform);

            // Apply material and texture
            if (material != null)
            {
                ApplyMaterialToAllRenderers(instance.transform, material, texture);
            }

            // Create Resources folder if it doesn't exist
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            // Save as prefab
            string prefabPath = $"{resourcesPath}/{config.templatePrefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            
            // Hide the template instance in hierarchy
            instance.hideFlags = HideFlags.HideInHierarchy;
            
            // Clean up the instance
            DestroyImmediate(instance);

            templatePrefab = prefab;
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Success", 
                $"Template prefab created at {prefabPath}", 
                "OK"
            );
        }

        private void RemoveExcludedObjects(Transform parent)
        {
            // Collect objects to remove
            var toRemove = parent.GetComponentsInChildren<Transform>(true)
                .Where(t => config.excludedObjectNames.Any(name => 
                    t.name.Contains(name, System.StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Remove from bottom up to avoid hierarchy issues
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                if (toRemove[i] != parent) // Don't remove the root
                {
                    DestroyImmediate(toRemove[i].gameObject);
                }
            }
        }

        private void ApplyMaterialToAllRenderers(Transform parent, Material material, Texture2D texture)
        {
            if (material == null)
            {
                Debug.LogWarning("Material is null, skipping material application");
                return;
            }

            var renderers = parent.GetComponentsInChildren<Renderer>(true);
            
            // Create Materials folder if it doesn't exist
            string materialsFolder = "Assets/Resources/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            }
            
            int materialIndex = 0;
            foreach (var renderer in renderers)
            {
                // Create a copy of the material
                Material materialInstance = new Material(material);
                materialInstance.name = $"StrawberryBush_Material_{materialIndex}";
                
                // Apply texture if available
                if (texture != null)
                {
                    if (materialInstance.HasProperty("_BaseMap"))
                    {
                        materialInstance.SetTexture("_BaseMap", texture);
                    }
                    else if (materialInstance.HasProperty("_MainTex"))
                    {
                        materialInstance.SetTexture("_MainTex", texture);
                    }
                }
                
                // Save material as asset
                string materialPath = $"{materialsFolder}/{materialInstance.name}.mat";
                
                // Delete existing material if it exists
                if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
                {
                    AssetDatabase.DeleteAsset(materialPath);
                }
                
                AssetDatabase.CreateAsset(materialInstance, materialPath);
                
                // Load the saved material and assign it
                Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                renderer.sharedMaterial = savedMaterial;
                
                materialIndex++;
            }
            
            AssetDatabase.SaveAssets();
        }

        private void SpawnBushAtCenter()
        {
            if (templatePrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Error", 
                    "Template prefab not found. Please generate it first.", 
                    "OK"
                );
                return;
            }

            // Instantiate at world origin
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
            instance.transform.position = Vector3.zero;
            instance.name = "StrawberryBush";

            // Register undo
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Strawberry Bush");

            // Select the spawned object
            Selection.activeGameObject = instance;

            Debug.Log($"Spawned strawberry bush at scene center: {instance.name}");
        }
    }
}
