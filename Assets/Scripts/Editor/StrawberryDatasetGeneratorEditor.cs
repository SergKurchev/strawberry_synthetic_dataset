using UnityEngine;
using UnityEditor;

namespace StrawberryDataset
{
    /// <summary>
    /// Editor window for both dataset generation pipelines.
    /// Tab 0: Old Pipeline  (unchanged)
    /// Tab 1: Multi-View Samples (new pipeline)
    /// </summary>
    public class StrawberryDatasetGeneratorEditor : EditorWindow
    {
        // ── Shared ─────────────────────────────────────────────────────────────
        private int          activeTab      = 0;
        private readonly string[] tabNames  = { "Old Pipeline", "Multi-View Samples" };
        private Vector2      scrollPosition;

        // ── Tab 0: Old pipeline ────────────────────────────────────────────────
        private StrawberryDatasetConfig     config;
        private StrawberryDatasetBatchGenerator generator;

        // ── Tab 1: Multi-view pipeline ─────────────────────────────────────────
        private MultiViewSampleConfig       mvConfig;
        private MultiViewSampleGenerator    mvGenerator;

        // ══════════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Strawberry Dataset Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<StrawberryDatasetGeneratorEditor>("Strawberry Dataset Generator");
            window.minSize = new Vector2(480, 460);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOldConfig();
            FindOldGenerator();
            LoadMVConfig();
            FindMVGenerator();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Main GUI
        // ══════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            activeTab = GUILayout.Toolbar(activeTab, tabNames, GUILayout.Height(28));
            EditorGUILayout.Space(6);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (activeTab == 0)
                DrawOldPipelineTab();
            else
                DrawMultiViewTab();

            EditorGUILayout.EndScrollView();

            // Auto-repaint when either generator is running
            bool isRunning =
                (generator  != null && generator.isGenerating) ||
                (mvGenerator != null && mvGenerator.isGenerating);
            if (isRunning) Repaint();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB 0 — Old Pipeline (original UI, unchanged)
        // ══════════════════════════════════════════════════════════════════════

        private void DrawOldPipelineTab()
        {
            EditorGUILayout.LabelField("Strawberry Dataset Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Configuration
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            var newConfig = (StrawberryDatasetConfig)EditorGUILayout.ObjectField(
                "Config Asset", config, typeof(StrawberryDatasetConfig), false);

            if (newConfig != config)
            {
                config = newConfig;
                SaveOldConfigPath();
            }

            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "No configuration found. Click 'Create Config' to create one.",
                    MessageType.Warning);

                if (GUILayout.Button("Create Config", GUILayout.Height(30)))
                    CreateOldConfig();
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
                    MessageType.Info);

                if (GUILayout.Button("Setup Generator", GUILayout.Height(30)))
                    SetupOldGenerator();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    generator.isGenerating
                        ? $"Generating... {generator.currentImageCount} images, {generator.currentSceneCount} scenes"
                        : "Ready to generate",
                    generator.isGenerating ? MessageType.Info : MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Scene Controls
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Controls", EditorStyles.boldLabel);

            GUI.enabled = generator != null && !generator.isGenerating && config != null;

            if (GUILayout.Button("Generate Random Scene", GUILayout.Height(35)))
            {
                if (generator.sceneGenerator == null)
                    generator.sceneGenerator = generator.gameObject.AddComponent<StrawberrySceneGenerator>();
                generator.sceneGenerator.config = config;
                generator.sceneGenerator.GenerateScene();
            }

            if (GUILayout.Button("Clear Scene", GUILayout.Height(35)))
            {
                if (generator.sceneGenerator == null)
                    generator.sceneGenerator = generator.gameObject.AddComponent<StrawberrySceneGenerator>();
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
                    "Yes", "Cancel");

                if (confirmed)
                {
                    generator.config = config;
                    generator.StartBatchGeneration();
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB 1 — Multi-View Samples (new pipeline)
        // ══════════════════════════════════════════════════════════════════════

        private void DrawMultiViewTab()
        {
            EditorGUILayout.LabelField("Multi-View Sample Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // ── Config ─────────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            var newMV = (MultiViewSampleConfig)EditorGUILayout.ObjectField(
                "Config Asset", mvConfig, typeof(MultiViewSampleConfig), false);
            if (newMV != mvConfig)
            {
                mvConfig = newMV;
                SaveMVConfigPath();
            }

            if (mvConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "No Multi-View config found. Click 'Create Config' to create one.",
                    MessageType.Warning);

                if (GUILayout.Button("Create Multi-View Config", GUILayout.Height(30)))
                    CreateMVConfig();
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Output folder : {mvConfig.outputFolder}");
                EditorGUILayout.LabelField($"Total samples : {mvConfig.totalSamples}");
                EditorGUILayout.LabelField($"Frames/sample : {mvConfig.framesPerSample}");
                EditorGUILayout.LabelField($"Image size    : {mvConfig.imageWidth} × {mvConfig.imageHeight}");
                EditorGUILayout.LabelField($"Room size     : {mvConfig.roomSideLength} m cube");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // ── Generator status ───────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Generator Status", EditorStyles.boldLabel);

            if (mvGenerator == null)
            {
                EditorGUILayout.HelpBox(
                    "Generator not found in scene. Click 'Setup Generator' to create one.",
                    MessageType.Info);

                if (GUILayout.Button("Setup Generator", GUILayout.Height(30)))
                    SetupMVGenerator();
            }
            else if (mvGenerator.isGenerating)
            {
                EditorGUILayout.HelpBox(
                    $"⏳ Generating... Sample {mvGenerator.currentSample + 1} / Frame {mvGenerator.currentFrame + 1}",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Ready to generate", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // ── Scene preview ──────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);

            bool readyForScene = mvGenerator != null && !mvGenerator.isGenerating && mvConfig != null;
            GUI.enabled = readyForScene;

            if (GUILayout.Button("Setup Scene (white room + bush)", GUILayout.Height(35)))
                SetupPreviewScene();

            if (GUILayout.Button("Clear Preview Scene", GUILayout.Height(35)))
                ClearPreviewScene();

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // ── Generation buttons ─────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Dataset Generation", EditorStyles.boldLabel);

            bool readyToGenerate = mvGenerator != null && !mvGenerator.isGenerating && mvConfig != null;
            GUI.enabled = readyToGenerate;

            if (GUILayout.Button("Generate 1 Test Sample", GUILayout.Height(40)))
            {
                mvGenerator.config = mvConfig;
                mvGenerator.StartGeneration(1);
            }

            if (GUILayout.Button($"Generate All {(mvConfig != null ? mvConfig.totalSamples : 0)} Samples", GUILayout.Height(40)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Generate All Samples",
                    $"Generate {mvConfig.totalSamples} samples ({mvConfig.totalSamples * mvConfig.framesPerSample} frames total).\nThis may take a while. Continue?",
                    "Yes", "Cancel");

                if (confirmed)
                {
                    mvGenerator.config = mvConfig;
                    mvGenerator.StartGeneration();
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Scene helpers (Tab 1)
        // ══════════════════════════════════════════════════════════════════════

        private void SetupPreviewScene()
        {
            EnsureMVGeneratorComponents();
            var sceneSetup = mvGenerator.GetComponent<WhiteRoomSceneSetup>()
                          ?? mvGenerator.gameObject.AddComponent<WhiteRoomSceneSetup>();
            sceneSetup.InitializeScene(mvConfig);
        }

        private void ClearPreviewScene()
        {
            if (mvGenerator == null) return;
            var sceneSetup = mvGenerator.GetComponent<WhiteRoomSceneSetup>();
            if (sceneSetup != null) sceneSetup.ClearScene();
        }

        private void EnsureMVGeneratorComponents()
        {
            if (mvGenerator == null) SetupMVGenerator();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Old-pipeline helpers (tab 0)
        // ══════════════════════════════════════════════════════════════════════

        private void LoadOldConfig()
        {
            string path = EditorPrefs.GetString("StrawberryDatasetGenerator_ConfigPath", "");
            if (!string.IsNullOrEmpty(path))
                config = AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(path);

            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:StrawberryDatasetConfig");
                if (guids.Length > 0)
                {
                    path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(path);
                    SaveOldConfigPath();
                }
            }
        }

        private void SaveOldConfigPath()
        {
            if (config != null)
                EditorPrefs.SetString("StrawberryDatasetGenerator_ConfigPath",
                                      AssetDatabase.GetAssetPath(config));
        }

        private void CreateOldConfig()
        {
            EnsureFolder("Assets/Settings");
            config = CreateInstance<StrawberryDatasetConfig>();
            string assetPath = "Assets/Settings/StrawberryDatasetConfig.asset";
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SaveOldConfigPath();
            EditorUtility.DisplayDialog("Config Created",
                $"Configuration created at {assetPath}\n\nPlease assign the bush prefab.", "OK");
            Selection.activeObject = config;
        }

        private void FindOldGenerator()
        {
            generator = FindObjectOfType<StrawberryDatasetBatchGenerator>();
        }

        private void SetupOldGenerator()
        {
            GameObject obj = new GameObject("StrawberryDatasetGenerator");
            generator = obj.AddComponent<StrawberryDatasetBatchGenerator>();

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            generator.mainCamera = cam;
            Selection.activeGameObject = obj;
            EditorUtility.DisplayDialog("Generator Setup",
                "Generator created. Assign the config and verify camera settings.", "OK");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Multi-view helpers
        // ══════════════════════════════════════════════════════════════════════

        private void LoadMVConfig()
        {
            string path = EditorPrefs.GetString("MultiViewSampleConfig_Path", "");
            if (!string.IsNullOrEmpty(path))
                mvConfig = AssetDatabase.LoadAssetAtPath<MultiViewSampleConfig>(path);

            if (mvConfig == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:MultiViewSampleConfig");
                if (guids.Length > 0)
                {
                    path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    mvConfig = AssetDatabase.LoadAssetAtPath<MultiViewSampleConfig>(path);
                    SaveMVConfigPath();
                }
            }
        }

        private void SaveMVConfigPath()
        {
            if (mvConfig != null)
                EditorPrefs.SetString("MultiViewSampleConfig_Path",
                                      AssetDatabase.GetAssetPath(mvConfig));
        }

        private void CreateMVConfig()
        {
            EnsureFolder("Assets/Settings");
            mvConfig = CreateInstance<MultiViewSampleConfig>();
            string assetPath = "Assets/Settings/MultiViewSampleConfig.asset";
            AssetDatabase.CreateAsset(mvConfig, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SaveMVConfigPath();
            EditorUtility.DisplayDialog("Config Created",
                $"Multi-View config created at {assetPath}\n\nAssign the bush prefab.", "OK");
            Selection.activeObject = mvConfig;
        }

        private void FindMVGenerator()
        {
            mvGenerator = FindObjectOfType<MultiViewSampleGenerator>();
        }

        private void SetupMVGenerator()
        {
            // Re-use the same GameObject if already in scene; otherwise create one
            GameObject obj = GameObject.Find("MultiViewSampleGenerator");
            if (obj == null) obj = new GameObject("MultiViewSampleGenerator");

            mvGenerator = obj.GetComponent<MultiViewSampleGenerator>()
                       ?? obj.AddComponent<MultiViewSampleGenerator>();

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            mvGenerator.mainCamera = cam;
            Selection.activeGameObject = obj;
            Debug.Log("[MultiViewSampleGenerator] Generator created in scene.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Shared utility
        // ══════════════════════════════════════════════════════════════════════

        private static void EnsureFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
                string leaf   = System.IO.Path.GetFileName(folderPath);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
