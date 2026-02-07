using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace StrawberryDataset
{
    /// <summary>
    /// Main batch dataset generator.
    /// Orchestrates scene generation, camera positioning, rendering, and annotation export.
    /// </summary>
    public class StrawberryDatasetBatchGenerator : MonoBehaviour
    {
        [Header("References")]
        public StrawberryDatasetConfig config;
        public Camera mainCamera;
        public StrawberrySceneGenerator sceneGenerator;
        public RandomCameraController cameraController;
        
        [Header("Status")]
        public bool isGenerating = false;
        public int currentImageCount = 0;
        public int currentSceneCount = 0;
        
        private AnnotationGenerator annotationGenerator;
        private DepthCaptureSystem depthCaptureSystem;
        private VisualizationGenerator visualizationGenerator;
        
        // Output paths
        private string basePath;
        private string imagesPath;
        private string labelsPath;
        private string depthPath;
        private string depthNpyPath;
        private string masksPath;
        private string bboxVizPath;
        private string maskVizPath;
        
        private List<AnnotationGenerator.ImageAnnotation> allImages = new List<AnnotationGenerator.ImageAnnotation>();
        private List<AnnotationGenerator.ObjectAnnotation> allAnnotations = new List<AnnotationGenerator.ObjectAnnotation>();
        private Dictionary<string, DepthCaptureSystem.DepthMetadata> allDepthMetadata = new Dictionary<string, DepthCaptureSystem.DepthMetadata>();
        private string tempMetadataPath;
        
        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
            
            if (sceneGenerator == null)
                sceneGenerator = gameObject.AddComponent<StrawberrySceneGenerator>();
            
            if (cameraController == null)
                cameraController = gameObject.AddComponent<RandomCameraController>();
            
            annotationGenerator = gameObject.AddComponent<AnnotationGenerator>();
            depthCaptureSystem = gameObject.AddComponent<DepthCaptureSystem>();
            visualizationGenerator = gameObject.AddComponent<VisualizationGenerator>();
        }
        
        /// <summary>
        /// Start full dataset generation
        /// </summary>
        public void StartBatchGeneration()
        {
            if (!isGenerating)
            {
                StartCoroutine(BatchGenerationCoroutine());
            }
        }
        
        /// <summary>
        /// Generate test dataset (10 samples)
        /// </summary>
        public void StartTestGeneration()
        {
            if (!isGenerating)
            {
                StartCoroutine(BatchGenerationCoroutine(10, 10));
            }
        }
        
        /// <summary>
        /// Main batch generation coroutine
        /// </summary>
        private IEnumerator BatchGenerationCoroutine(int? totalImages = null, int? imagesPerScene = null)
        {
            isGenerating = true;
            
            // Validate config
            if (config == null)
            {
                Debug.LogError("Config is null! Cannot generate dataset.");
                isGenerating = false;
                yield break;
            }
            
            int targetImages = totalImages ?? config.totalImages;
            int shotsPerScene = imagesPerScene ?? config.imagesPerScene;
            
            // Ensure all components are initialized
            if (mainCamera == null)
                mainCamera = Camera.main;
            
            if (sceneGenerator == null)
                sceneGenerator = gameObject.AddComponent<StrawberrySceneGenerator>();
            
            if (cameraController == null)
                cameraController = gameObject.AddComponent<RandomCameraController>();
            
            if (annotationGenerator == null)
                annotationGenerator = gameObject.AddComponent<AnnotationGenerator>();
            
            if (depthCaptureSystem == null)
                depthCaptureSystem = gameObject.AddComponent<DepthCaptureSystem>();
            
            if (visualizationGenerator == null)
                visualizationGenerator = gameObject.AddComponent<VisualizationGenerator>();
            
            // Initialize systems
            sceneGenerator.config = config;
            cameraController.config = config;
            cameraController.sceneGenerator = sceneGenerator;
            cameraController.targetCamera = mainCamera;
            
            annotationGenerator.Initialize(mainCamera, config.imageWidth, config.imageHeight);
            depthCaptureSystem.Initialize(mainCamera);
            
            // Setup output directories
            basePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", config.outputFolder));
            imagesPath = Path.Combine(basePath, "images");
            labelsPath = Path.Combine(basePath, "labels");
            depthPath = Path.Combine(basePath, "depth");
            depthNpyPath = Path.Combine(basePath, "depth_npy");
            masksPath = Path.Combine(basePath, "masks");
            string vizPath = Path.Combine(basePath, "visualizations");
            bboxVizPath = Path.Combine(vizPath, "bbox");
            maskVizPath = Path.Combine(vizPath, "mask_overlay");
            tempMetadataPath = Path.Combine(basePath, "metadata_temp");
            
            Directory.CreateDirectory(imagesPath);
            Directory.CreateDirectory(tempMetadataPath);
            Directory.CreateDirectory(labelsPath);
            Directory.CreateDirectory(depthPath);
            Directory.CreateDirectory(depthNpyPath);
            Directory.CreateDirectory(masksPath);
            if (config.saveVisualizations)
            {
                Directory.CreateDirectory(bboxVizPath);
                Directory.CreateDirectory(maskVizPath);
            }
            
            Debug.Log($"Starting dataset generation: {targetImages} images, {shotsPerScene} per scene");
            Debug.Log($"Output path: {basePath}");
            
            // Check for existing data and resume if found
            int startIndex = 0;
            bool isResumeMode = false;
            
            if (Directory.Exists(imagesPath))
            {
                var existingImages = Directory.GetFiles(imagesPath, "*.png");
                if (existingImages.Length > 0)
                {
                    // Find highest index
                    foreach (var imgPath in existingImages)
                    {
                        string filename = Path.GetFileNameWithoutExtension(imgPath);
                        if (int.TryParse(filename, out int index))
                        {
                            startIndex = Mathf.Max(startIndex, index + 1);
                        }
                    }
                    
                    isResumeMode = true;
                    Debug.Log($"📁 Found {existingImages.Length} existing images. Resuming from index {startIndex}");
                }
            }

            if (isResumeMode)
            {
                // RESUME: Load existing data instead of clearing
                LoadExistingData();
                Debug.LogWarning("⚠ Note: depth_metadata.json will currently only contain entries for images generated in this session.");
            }
            else
            {
                // FRESH START: Clear all lists and reset counters
                Debug.Log("📁 Starting fresh generation");
                allImages.Clear();
                allAnnotations.Clear();
                allDepthMetadata.Clear();
                AnnotationGenerator.ResetAnnotationIdCounter();
            }
            
            currentImageCount = startIndex;
            currentSceneCount = 0;
            
            // Emergency safety: max scenes to prevent infinite loop
            const int maxScenes = 100;
            
            // Generation loop
            while (currentImageCount < targetImages && currentSceneCount < maxScenes)
            {
                // Generate new scene
                sceneGenerator.GenerateScene();
                currentSceneCount++;
                
                yield return null;
                
                // Capture multiple shots from this scene
                int successfulShots = 0;
                int attemptedShots = 0;
                int maxRetriesPerShot = config.maxRetriesPerImage;
                
                for (int shot = 0; shot < shotsPerScene && currentImageCount < targetImages; shot++)
                {
                    bool shotSuccessful = false;
                    int retryCount = 0;
                    
                    // Retry loop for this shot
                    while (!shotSuccessful && retryCount < maxRetriesPerShot)
                    {
                        attemptedShots++;
                        retryCount++;
                        
                        // Position camera randomly
                        bool cameraPositioned = cameraController.PositionRandomly();
                        if (!cameraPositioned)
                        {
                            Debug.LogWarning($"Attempt {retryCount}/{maxRetriesPerShot}: Failed to position camera for image {currentImageCount}");
                            continue; // Retry
                        }
                        
                        yield return null;
                        
                        string fileName = $"{currentImageCount:D5}.png";
                        
                        // Generate annotations with segmentation mask
                        RenderTexture segMask;
                        List<AnnotationGenerator.ObjectAnnotation> frameAnnotations = annotationGenerator.GenerateAnnotations(
                            currentImageCount,
                            sceneGenerator,
                            config.minPixelCount,
                            out segMask
                        );
                        
                        // Validate coverage
                        float coverageRatio = CalculateCoverageRatio(segMask, config.imageWidth, config.imageHeight);
                        if (coverageRatio < config.minCoverageRatio)
                        {
                            Debug.LogWarning($"Attempt {retryCount}/{maxRetriesPerShot}: Low coverage ({coverageRatio * 100:F1}%) for image {currentImageCount}, retrying...");
                            segMask.Release();
                            continue; // Retry
                        }
                        
                        // Success! Capture and save
                        RenderTexture rgbRT = CaptureRGBImage(Path.Combine(imagesPath, fileName));
                        
                        // Save segmentation mask
                        SaveRenderTexture(segMask, Path.Combine(masksPath, fileName));
                        
                        // Save YOLO labels
                        annotationGenerator.SaveYOLOAnnotations(
                            frameAnnotations,
                            Path.Combine(labelsPath, $"{currentImageCount:D5}.txt")
                        );
                        
                        // Capture depth
                        DepthCaptureSystem.DepthMetadata depthMeta = depthCaptureSystem.CaptureDepth(
                            Path.Combine(depthPath, fileName),
                            Path.Combine(depthNpyPath, $"{currentImageCount:D5}.npy"),
                            config.imageWidth,
                            config.imageHeight
                        );
                        
                        // Log depth range for verification
                        if (currentImageCount % 10 == 0)
                        {
                            Debug.Log($"Sample {currentImageCount} depth range: {depthMeta.depth_range.min_meters:F2}m - {depthMeta.depth_range.max_meters:F2}m");
                        }
                        
                        // Generate visualizations
                        if (config.saveVisualizations)
                        {
                            // BBox visualization
                            Texture2D bboxViz = visualizationGenerator.GenerateBBoxVisualization(
                                rgbRT,
                                frameAnnotations,
                                config.imageWidth,
                                config.imageHeight
                            );
                            byte[] bboxBytes = bboxViz.EncodeToPNG();
                            File.WriteAllBytes(Path.Combine(bboxVizPath, fileName), bboxBytes);
                            DestroyImmediate(bboxViz);
                            
                            // Mask overlay visualization
                            Texture2D maskViz = visualizationGenerator.GenerateMaskOverlay(
                                rgbRT,
                                segMask,
                                config.maskOverlayColor,
                                config.imageWidth,
                                config.imageHeight
                            );
                            byte[] maskBytes = maskViz.EncodeToPNG();
                            File.WriteAllBytes(Path.Combine(maskVizPath, fileName), maskBytes);
                            DestroyImmediate(maskViz);
                        }
                        
                        // Cleanup
                        rgbRT.Release();
                        segMask.Release();
                        
                        // Store metadata
                        allImages.Add(new AnnotationGenerator.ImageAnnotation
                        {
                            id = currentImageCount,
                            file_name = fileName,
                            width = config.imageWidth,
                            height = config.imageHeight
                        });
                        
                        allAnnotations.AddRange(frameAnnotations);

                        allAnnotations.AddRange(frameAnnotations);
                        allDepthMetadata[fileName] = depthMeta;
                        
                        // INCREMENTAL METADATA SAVE
                        SaveFrameMetadata(currentImageCount, depthMeta);
                        
                        currentImageCount++;
                        successfulShots++;
                        shotSuccessful = true;
                        
                        Debug.Log($"✓ Successfully captured image {currentImageCount - 1} (shot {shot + 1}/{shotsPerScene}, attempt {retryCount})");
                    }
                    
                    // If retries exhausted for this shot, log and move to next shot
                    if (!shotSuccessful)
                    {
                        Debug.LogWarning($"✗ Failed to capture shot {shot + 1} after {maxRetriesPerShot} attempts. Moving to next shot...");
                    }
                    
                    yield return null;
                }
                
                // Log scene completion stats
                Debug.Log($"Scene {currentSceneCount} complete: {successfulShots}/{shotsPerScene} successful shots");
                
                // Progressive save to avoid data loss
                AnnotationGenerator.SaveCOCOAnnotations(allImages, allAnnotations, Path.Combine(basePath, "annotations.json"));
                
                if (currentImageCount % 10 == 0)
                {
                    Debug.Log($"Progress: {currentImageCount}/{targetImages} images ({currentSceneCount} scenes)");
                }
                
                // Clear scene for next iteration
                sceneGenerator.ClearScene();
                yield return null;
            }
            
            // Check if stopped due to max scenes
            if (currentImageCount < targetImages)
            {
                Debug.LogWarning($"Generation stopped after {currentSceneCount} scenes with only {currentImageCount}/{targetImages} images. " +
                               "Validation may be too strict! Check preventGreenhouseExit and minCoverageRatio settings.");
            }
            
            // Save final annotations
            AnnotationGenerator.SaveCOCOAnnotations(allImages, allAnnotations, Path.Combine(basePath, "annotations.json"));
            // Save final annotations
            AnnotationGenerator.SaveCOCOAnnotations(allImages, allAnnotations, Path.Combine(basePath, "annotations.json"));
            
            // CONSOLIDATE METADATA instead of just saving current session's dictionary
            ConsolidateMetadata();
            
            SaveDatasetInfo(basePath);
            SaveDatasetInfo(basePath);
            
            Debug.Log($"Dataset generation complete! {currentImageCount} images saved to {basePath}");
            
            // Auto Visualization
            if (config.autoVisualize)
            {
                Debug.Log("Running auto-visualization...");
                string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Scripts", "simple_visualizer.py"));
                if (File.Exists(scriptPath))
                {
                    string args = $"\"{scriptPath}\" \"{basePath}\"";
                    try 
                    {
                        System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
                        start.FileName = "python";
                        start.Arguments = args;
                        start.UseShellExecute = false;
                        start.CreateNoWindow = true; // Use false if you want to see the terminal
                        
                        System.Diagnostics.Process.Start(start);
                        Debug.Log("Visualization script started.");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to run visualization script: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"Visualization script not found at {scriptPath}");
                }
            }
            
            isGenerating = false;
        }
        
        /// <summary>
        /// Capture RGB image from camera and return RenderTexture (caller must Release())
        /// </summary>
        private RenderTexture CaptureRGBImage(string outputPath)
        {
            RenderTexture rt = RenderTexture.GetTemporary(config.imageWidth, config.imageHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture oldRT = mainCamera.targetTexture;
            
            mainCamera.targetTexture = rt;
            mainCamera.Render();
            mainCamera.targetTexture = oldRT;
            
            // Save to file
            SaveRenderTexture(rt, outputPath);
            
            // Return RT for visualization (caller must release)
            return rt;
        }
        
        /// <summary>
        /// Save render texture to PNG
        /// </summary>
        private void SaveRenderTexture(RenderTexture rt, string filePath)
        {
            RenderTexture.active = rt;
            // CRITICAL: Use Linear color space (true) to prevent Gamma correction during readback
            // CRITICAL: Use RGBA32 to avoid channel alignment issues
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            
            // Debug check of a random center pixel
            Color32 centerPixel = tex.GetPixel(rt.width/2, rt.height/2);
            // Debug.Log($"SavedPixel(Center): R={centerPixel.r} G={centerPixel.g} B={centerPixel.b} A={centerPixel.a}");
            
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            
            RenderTexture.active = null;
            DestroyImmediate(tex);
        }
        
        /// <summary>
        /// Calculate percentage of image covered by non-background pixels
        /// </summary>
        private float CalculateCoverageRatio(RenderTexture maskRT, int width, int height)
        {
            RenderTexture.active = maskRT;
            Texture2D maskTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            maskTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            maskTex.Apply();
            RenderTexture.active = null;
            
            Color[] pixels = maskTex.GetPixels();
            int nonBackgroundCount = 0;
            
            for (int i = 0; i < pixels.Length; i++)
            {
                // Background is (0,0,0), anything else is an object
                if (pixels[i].r > 0.01f || pixels[i].g > 0.01f || pixels[i].b > 0.01f)
                {
                    nonBackgroundCount++;
                }
            }
            
            DestroyImmediate(maskTex);
            
            return (float)nonBackgroundCount / pixels.Length;
        }
        
        /// <summary>
        /// Save enhanced visualization
        /// </summary>
        private void SaveVisualization(string filePath, RenderTexture maskRT)
        {
            RenderTexture.active = maskRT;
            Texture2D mask = new Texture2D(maskRT.width, maskRT.height, TextureFormat.RGB24, false);
            mask.ReadPixels(new Rect(0, 0, maskRT.width, maskRT.height), 0, 0);
            mask.Apply();
            RenderTexture.active = null;
            
            // Enhance colors for visibility
            Color32[] pixels = mask.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > 0 || pixels[i].g > 0)
                {
                    int hue = (pixels[i].r * 37 + pixels[i].g * 73) % 256;
                    pixels[i] = new Color32(
                        (byte)((pixels[i].r * 30) % 256 + 50),
                        (byte)((pixels[i].g * 100) % 256 + 50),
                        (byte)hue,
                        255
                    );
                }
            }
            mask.SetPixels32(pixels);
            mask.Apply();
            
            byte[] bytes = mask.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            
            DestroyImmediate(mask);
        }

        /// <summary>
        /// Save individual frame metadata to temp folder
        /// </summary>
        private void SaveFrameMetadata(int imageIndex, DepthCaptureSystem.DepthMetadata metadata)
        {
            if (string.IsNullOrEmpty(tempMetadataPath))
                tempMetadataPath = Path.Combine(basePath, "metadata_temp");
                
            if (!Directory.Exists(tempMetadataPath))
                Directory.CreateDirectory(tempMetadataPath);

            string filename = $"{imageIndex:D5}_meta.json";
            string path = Path.Combine(tempMetadataPath, filename);
            string json = JsonUtility.ToJson(metadata, true);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Consolidate all individual metadata files into master depth_metadata.json
        /// </summary>
        public void ConsolidateMetadata()
        {
            if (string.IsNullOrEmpty(tempMetadataPath) || !Directory.Exists(tempMetadataPath))
            {
                Debug.LogWarning("No temp metadata folder found to consolidate.");
                return;
            }

            Debug.Log("🔄 Consolidating metadata...");
            
            var metaFiles = Directory.GetFiles(tempMetadataPath, "*_meta.json");
            var consolidatedData = new SortedDictionary<string, DepthCaptureSystem.DepthMetadata>();

            // Load all temp files
            foreach (var file in metaFiles)
            {
                try 
                {
                    string json = File.ReadAllText(file);
                    var meta = JsonUtility.FromJson<DepthCaptureSystem.DepthMetadata>(json);
                    
                    // Extract index from filename "00001_meta.json" -> "00001.png"
                    string filename = Path.GetFileName(file);
                    string indexStr = filename.Split('_')[0];
                    string key = $"{indexStr}.png";
                    
                    consolidatedData[key] = meta;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error reading meta file {file}: {e.Message}");
                }
            }
            
            // Build master JSON manually to matching existing format and precision
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            
            int count = 0;
            foreach (var kvp in consolidatedData)
            {
                var meta = kvp.Value;
                var intr = meta.camera_intrinsics;
                var extr = meta.camera_extrinsics;
                var depth = meta.depth_range;
                
                sb.Append($"  \"{kvp.Key}\": {{");
                
                // Camera intrinsics
                sb.Append($"\"camera_intrinsics\": {{");
                sb.Append($"\"fx\": {intr.fx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"fy\": {intr.fy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"cx\": {intr.cx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"cy\": {intr.cy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append("}, ");
                
                // Camera extrinsics
                sb.Append($"\"camera_extrinsics\": {{");
                sb.Append($"\"position\": [");
                sb.Append($"{extr.position[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.position[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.position[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($"], \"rotation\": [");
                sb.Append($"{extr.rotation[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[3].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($"]}}, ");
                
                // Depth range
                sb.Append($"\"depth_range\": {{");
                sb.Append($"\"min_meters\": {depth.min_meters.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"max_meters\": {depth.max_meters.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append("}");
                
                sb.Append("}");
                
                count++;
                if (count < consolidatedData.Count)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            
            sb.AppendLine("}");
            
            string masterPath = Path.Combine(basePath, "depth_metadata.json");
            File.WriteAllText(masterPath, sb.ToString());
            Debug.Log($"✅ Consolidated {consolidatedData.Count} metadata entries to {masterPath}");
        }
        
        /// <summary>
        /// Save depth metadata JSON (Legacy - kept for reference or direct calls)
        /// </summary>
        private void SaveDepthMetadata(string path)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            
            int count = 0;
            foreach (var kvp in allDepthMetadata)
            {
                var meta = kvp.Value;
                var intr = meta.camera_intrinsics;
                var extr = meta.camera_extrinsics;
                var depth = meta.depth_range;
                
                sb.Append($"  \"{kvp.Key}\": {{");
                
                // Camera intrinsics with InvariantCulture (dots, not commas)
                sb.Append($"\"camera_intrinsics\": {{");
                sb.Append($"\"fx\": {intr.fx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"fy\": {intr.fy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"cx\": {intr.cx.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"cy\": {intr.cy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append("}, ");
                
                // Camera extrinsics with InvariantCulture
                sb.Append($"\"camera_extrinsics\": {{");
                sb.Append($"\"position\": [");
                sb.Append($"{extr.position[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.position[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.position[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($"], \"rotation\": [");
                sb.Append($"{extr.rotation[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"{extr.rotation[3].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($"]}}, ");
                
                // Depth range with InvariantCulture
                sb.Append($"\"depth_range\": {{");
                sb.Append($"\"min_meters\": {depth.min_meters.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"max_meters\": {depth.max_meters.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append("}");
                
                sb.Append("}");
                
                count++;
                if (count < allDepthMetadata.Count)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }
        
        /// <summary>
        /// Save dataset info
        /// </summary>
        private void SaveDatasetInfo(string basePath)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"total_images\": {allImages.Count},");
            sb.AppendLine($"  \"total_annotations\": {allAnnotations.Count},");
            sb.AppendLine($"  \"total_scenes\": {currentSceneCount},");
            sb.AppendLine($"  \"image_width\": {config.imageWidth},");
            sb.AppendLine($"  \"image_height\": {config.imageHeight},");
            sb.AppendLine($"  \"classes\": [\"strawberry_ripe\", \"strawberry_unripe\", \"strawberry_half_ripe\", \"peduncle\"],");
            sb.AppendLine($"  \"generation_date\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
            sb.AppendLine("}");
            
            File.WriteAllText(Path.Combine(basePath, "dataset_info.json"), sb.ToString());
        }
        /// <summary>
        /// JSON wrapper for Unity's JsonUtility to load COCO annotations
        /// </summary>
        [System.Serializable]
        private class AnnotationsWrapper
        {
            public AnnotationGenerator.ImageAnnotation[] images;
            public AnnotationGenerator.ObjectAnnotation[] annotations;
        }

        /// <summary>
        /// Loads existing annotations from JSON to resume generation
        /// </summary>
        private void LoadExistingData()
        {
            string annotationsPath = Path.Combine(basePath, "annotations.json");
            if (File.Exists(annotationsPath))
            {
                try
                {
                    string json = File.ReadAllText(annotationsPath);
                    AnnotationsWrapper wrapper = JsonUtility.FromJson<AnnotationsWrapper>(json);
                    
                    if (wrapper != null)
                    {
                        if (wrapper.images != null)
                            allImages = new List<AnnotationGenerator.ImageAnnotation>(wrapper.images);
                            
                        if (wrapper.annotations != null)
                        {
                            allAnnotations = new List<AnnotationGenerator.ObjectAnnotation>(wrapper.annotations);
                            
                            // Set the global annotation ID counter to continue correctly
                            int maxId = 0;
                            foreach (var ann in allAnnotations)
                            {
                                if (ann.id > maxId) maxId = ann.id;
                            }
                            AnnotationGenerator.SetAnnotationIdCounter(maxId + 1);
                        }
                        
                        Debug.Log($"✓ Successfully loaded {allImages.Count} images and {allAnnotations.Count} annotations from existing files.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load existing annotations: {e.Message}");
                }
            }

            // Depth metadata is a dictionary which JsonUtility doesn't support.
            // For now, we'll notify that depth metadata might be incomplete unless we find a better way.
            // But since we generate it from allDepthMetadata, we'll just have to deal with it.
            // Actually, we could potentially parse it manually if needed, but for now let's hope 
            // the user doesn't need to resume depth metadata specifically or we accept it starts fresh.
        }
    }
}
