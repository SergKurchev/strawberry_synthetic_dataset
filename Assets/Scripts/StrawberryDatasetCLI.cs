using UnityEngine;
using System;

namespace StrawberryDataset
{
    /// <summary>
    /// Command-line interface for headless dataset generation.
    /// Usage: Unity.exe -batchmode -quit -executeMethod StrawberryDatasetCLI.GenerateDataset
    /// </summary>
    public class StrawberryDatasetCLI
    {
        /// <summary>
        /// Main CLI entry point for dataset generation
        /// </summary>
        public static void GenerateDataset()
        {
            Debug.Log("=== Strawberry Dataset CLI Generation Started ===");
            
            // Parse command line arguments
            string[] args = Environment.GetCommandLineArgs();
            string outputPath = "strawberry_dataset";
            int totalSamples = 1000;
            int samplesPerScene = 50;
            string configPath = "";
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-outputPath" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                }
                else if (args[i] == "-totalSamples" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out totalSamples);
                }
                else if (args[i] == "-samplesPerScene" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out samplesPerScene);
                }
                else if (args[i] == "-config" && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                }
            }
            
            Debug.Log($"Output Path: {outputPath}");
            Debug.Log($"Total Samples: {totalSamples}");
            Debug.Log($"Samples Per Scene: {samplesPerScene}");
            
            // Load or create config
            StrawberryDatasetConfig config = null;
            
            if (!string.IsNullOrEmpty(configPath))
            {
#if UNITY_EDITOR
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(configPath);
#endif
            }
            
            if (config == null)
            {
                // Find first config in project
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:StrawberryDatasetConfig");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = UnityEditor.AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(path);
                    Debug.Log($"Using config: {path}");
                }
#endif
            }
            
            if (config == null)
            {
                Debug.LogError("No config found! Please specify -config path or create a StrawberryDatasetConfig asset.");
                return;
            }
            
            // Override config settings
            config.outputFolder = outputPath;
            config.totalImages = totalSamples;
            config.imagesPerScene = samplesPerScene;
            
            // Setup scene
            GameObject generatorObj = new GameObject("StrawberryDatasetGenerator_CLI");
            var generator = generatorObj.AddComponent<StrawberryDatasetBatchGenerator>();
            
            // Setup camera
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            
            generator.mainCamera = cam;
            generator.config = config;
            
            // Start generation
            Debug.Log("Starting batch generation...");
            generator.StartBatchGeneration();
            
            // Wait for completion (in batchmode, Unity will exit when all coroutines finish)
            Debug.Log("Generation started. Unity will exit when complete.");
        }
        
        /// <summary>
        /// Generate single test scene (for debugging)
        /// </summary>
        public static void GenerateTestScene()
        {
            Debug.Log("=== Generating Test Scene ===");
            
            // Load config
            StrawberryDatasetConfig config = null;
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:StrawberryDatasetConfig");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(path);
            }
#endif
            
            if (config == null)
            {
                Debug.LogError("No config found!");
                return;
            }
            
            // Setup generator
            GameObject generatorObj = new GameObject("StrawberryDatasetGenerator_Test");
            var generator = generatorObj.AddComponent<StrawberryDatasetBatchGenerator>();
            
            generator.sceneGenerator.config = config;
            generator.sceneGenerator.GenerateScene();
            
            Debug.Log("Test scene generated!");
        }
    }
}
