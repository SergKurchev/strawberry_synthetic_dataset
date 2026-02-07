using UnityEngine;
using UnityEditor;

namespace StrawberryDataset
{
    /// <summary>
    /// Editor window for strawberry dataset generation.
    /// Provides UI controls for scene generation and dataset export.
    /// </summary>
    public class StrawberryDatasetGeneratorEditor : EditorWindow
    {
        private StrawberryDatasetConfig config;
        private StrawberryDatasetBatchGenerator generator;
        private Vector2 scrollPosition;
        
        [MenuItem("Tools/Strawberry Dataset Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<StrawberryDatasetGeneratorEditor>("Strawberry Dataset Generator");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadConfig();
            FindGenerator();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Strawberry Dataset Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Configuration
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            var newConfig = (StrawberryDatasetConfig)EditorGUILayout.ObjectField(
                "Config Asset",
                config,
                typeof(StrawberryDatasetConfig),
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
                    "No configuration found. Click 'Create Config' to create one.",
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
                EditorGUILayout.LabelField($"Output: {config.outputFolder}");
                EditorGUILayout.LabelField($"Total Images: {config.totalImages}");
                EditorGUILayout.LabelField($"Images Per Scene: {config.imagesPerScene}");
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Generator Status
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Generator Status", EditorStyles.boldLabel);
            
            if (generator == null)
            {
                EditorGUILayout.HelpBox(
                    "Generator not found in scene. Click 'Setup Generator' to create one.",
                    MessageType.Info
                );
                
                if (GUILayout.Button("Setup Generator", GUILayout.Height(30)))
                {
                    SetupGenerator();
                }
            }
            else
            {
                if (generator.isGenerating)
                {
                    EditorGUILayout.HelpBox(
                        $"Generating... {generator.currentImageCount} images, {generator.currentSceneCount} scenes",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox("Ready to generate", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Scene Controls
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Controls", EditorStyles.boldLabel);
            
            GUI.enabled = generator != null && !generator.isGenerating && config != null;
            
            if (GUILayout.Button("Generate Random Scene", GUILayout.Height(35)))
            {
                // Ensure sceneGenerator is initialized
                if (generator.sceneGenerator == null)
                {
                    generator.sceneGenerator = generator.gameObject.AddComponent<StrawberrySceneGenerator>();
                }
                generator.sceneGenerator.config = config;
                generator.sceneGenerator.GenerateScene();
            }
            
            if (GUILayout.Button("Clear Scene", GUILayout.Height(35)))
            {
                // Ensure sceneGenerator is initialized
                if (generator.sceneGenerator == null)
                {
                    generator.sceneGenerator = generator.gameObject.AddComponent<StrawberrySceneGenerator>();
                }
                generator.sceneGenerator.ClearScene();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Dataset Generation
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Dataset Generation", EditorStyles.boldLabel);
            
            GUI.enabled = generator != null && !generator.isGenerating && config != null;
            
            if (GUILayout.Button("Generate Test Dataset (10 samples)", GUILayout.Height(40)))
            {
                generator.config = config;
                generator.StartTestGeneration();
            }
            
            if (GUILayout.Button("Generate Full Dataset (1000 samples)", GUILayout.Height(40)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Generate Full Dataset",
                    $"This will generate {config.totalImages} images. This may take 50-60 minutes. Continue?",
                    "Yes",
                    "Cancel"
                );
                
                if (confirmed)
                {
                    generator.config = config;
                    generator.StartBatchGeneration();
                }
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
            
            // Auto-repaint during generation
            if (generator != null && generator.isGenerating)
            {
                Repaint();
            }
        }
        
        private void LoadConfig()
        {
            string configPath = EditorPrefs.GetString("StrawberryDatasetGenerator_ConfigPath", "");
            
            if (!string.IsNullOrEmpty(configPath))
            {
                config = AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(configPath);
            }
            
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:StrawberryDatasetConfig");
                if (guids.Length > 0)
                {
                    configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(configPath);
                    SaveConfigPath();
                }
            }
        }
        
        private void SaveConfigPath()
        {
            if (config != null)
            {
                string path = AssetDatabase.GetAssetPath(config);
                EditorPrefs.SetString("StrawberryDatasetGenerator_ConfigPath", path);
            }
        }
        
        private void CreateConfig()
        {
            string settingsDir = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(settingsDir))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
            
            config = CreateInstance<StrawberryDatasetConfig>();
            string assetPath = $"{settingsDir}/StrawberryDatasetConfig.asset";
            
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            SaveConfigPath();
            
            EditorUtility.DisplayDialog(
                "Config Created",
                $"Configuration created at {assetPath}\n\nPlease assign the strawberry bush prefab in the config!",
                "OK"
            );
            
            Selection.activeObject = config;
        }
        
        private void FindGenerator()
        {
            generator = FindObjectOfType<StrawberryDatasetBatchGenerator>();
        }
        
        private void SetupGenerator()
        {
            GameObject generatorObj = new GameObject("StrawberryDatasetGenerator");
            generator = generatorObj.AddComponent<StrawberryDatasetBatchGenerator>();
            
            // Find or create camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            
            generator.mainCamera = cam;
            
            Selection.activeGameObject = generatorObj;
            
            EditorUtility.DisplayDialog(
                "Generator Setup",
                "Generator created in scene. Please assign the config and verify camera settings.",
                "OK"
            );
        }
    }
}
