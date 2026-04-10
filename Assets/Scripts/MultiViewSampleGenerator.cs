using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace StrawberryDataset
{
    /// <summary>
    /// Main orchestrator for the multi-view sample generation pipeline.
    ///
    /// One sample = N frames around a single static bush inside a white room.
    /// Per frame: RGB image, depth .npy, segmentation mask, YOLO label.
    /// Per sample: cameras.json, color_map.json, then Python post-processing
    ///             (COCO annotations, merged point cloud .ply, HTML viewer).
    ///
    /// Segmentation covers 3 strawberry classes only (ripe / unripe / half_ripe).
    /// Peduncles are NOT included in masks or annotations.
    /// </summary>
    public class MultiViewSampleGenerator : MonoBehaviour
    {
        // ── Inspector / external references ───────────────────────────────────
        [Header("References")]
        public MultiViewSampleConfig config;
        public Camera mainCamera;

        // ── Status ─────────────────────────────────────────────────────────────
        [Header("Status (read-only)")]
        public bool  isGenerating    = false;
        public int   currentSample   = 0;
        public int   currentFrame    = 0;

        // ── Private components (created dynamically) ───────────────────────────
        private WhiteRoomSceneSetup    sceneSetup;
        private MultiViewCameraController cameraController;
        private DepthCaptureSystem     depthSystem;

        // ── Color map: instanceId → SegInfo (for current sample) ───────────────
        private Dictionary<int, SegInfo> colorMap = new Dictionary<int, SegInfo>();

        [System.Serializable]
        private class SegInfo
        {
            public int   instance_id;
            public int   category_id;      // 0=ripe, 1=unripe, 2=half_ripe
            public string ripeness;
            public int[] color = new int[3]; // R,G,B 0-255
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════════

        public void StartGeneration(int? overrideSamples = null)
        {
            if (!isGenerating)
                StartCoroutine(GenerationCoroutine(overrideSamples ?? config.totalSamples));
        }

        public void StopGeneration()
        {
            StopAllCoroutines();
            isGenerating = false;
            CleanupComponents();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Core Coroutine
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator GenerationCoroutine(int totalSamples)
        {
            isGenerating  = true;
            currentSample = 0;

            // ── Validate ───────────────────────────────────────────────────
            if (config == null)
            {
                Debug.LogError("[MultiViewSampleGenerator] Config is null.");
                isGenerating = false;
                yield break;
            }
            if (config.strawberryBushPrefab == null)
            {
                Debug.LogError("[MultiViewSampleGenerator] Bush prefab not assigned in config.");
                isGenerating = false;
                yield break;
            }

            // ── Ensure camera ──────────────────────────────────────────────
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject camObj = new GameObject("MultiView_Camera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            // ── Ensure helper components ───────────────────────────────────
            sceneSetup          = GetOrAdd<WhiteRoomSceneSetup>();
            cameraController    = GetOrAdd<MultiViewCameraController>();
            depthSystem         = GetOrAdd<DepthCaptureSystem>();

            cameraController.targetCamera = mainCamera;
            cameraController.sceneSetup   = sceneSetup;
            cameraController.config       = config;
            depthSystem.Initialize(mainCamera);

            // ── Root output directory ──────────────────────────────────────
            string rootPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", config.outputFolder));
            Directory.CreateDirectory(rootPath);

            Debug.Log($"[MultiViewSampleGenerator] Starting generation: {totalSamples} samples → {rootPath}");

            // ══════════════════════════════════════════════════════════════
            //  Sample loop
            // ══════════════════════════════════════════════════════════════
            for (int s = 0; s < totalSamples; s++)
            {
                currentSample = s;
                string samplePath = Path.Combine(rootPath, $"sample_{s:D5}");

                // Create sub-directories
                string rgbPath    = Path.Combine(samplePath, "rgb");
                string depthPath  = Path.Combine(samplePath, "depth");
                string masksPath  = Path.Combine(samplePath, "masks");
                string labelsPath = Path.Combine(samplePath, "labels");
                Directory.CreateDirectory(rgbPath);
                Directory.CreateDirectory(depthPath);
                Directory.CreateDirectory(masksPath);
                Directory.CreateDirectory(labelsPath);

                // ── 1. Build scene ─────────────────────────────────────────
                sceneSetup.InitializeScene(config);
                yield return null; // let Unity process the spawned objects

                var bushInstance = sceneSetup.GetBushInstance();
                if (bushInstance == null)
                {
                    Debug.LogError($"[MultiViewSampleGenerator] Bush instance missing for sample {s}. Skipping.");
                    sceneSetup.ClearScene();
                    continue;
                }

                // ── 2. Assign stable per-sample color IDs ──────────────────
                colorMap.Clear();
                AssignGlobalColorIds(bushInstance);
                SaveColorMap(Path.Combine(samplePath, "color_map.json"));

                // Camera data list for this sample
                var frameCameras = new List<MultiViewCameraController.FrameCameraData>();

                // YOLO annotations per frame
                var frameYoloLines = new List<List<string>>();

                // ── 3. Frame loop ──────────────────────────────────────────
                for (int f = 0; f < config.framesPerSample; f++)
                {
                    currentFrame = f;

                    // Position camera
                    bool ok = cameraController.PositionForFrame();
                    if (!ok)
                    {
                        Debug.LogWarning($"[MultiViewSampleGenerator] Sample {s} frame {f}: camera positioning failed, using last position.");
                    }

                    yield return null;

                    string frameName = $"{f:D5}";

                    // ── a. RGB capture ──────────────────────────────────────
                    CaptureRGB(Path.Combine(rgbPath, frameName + ".png"));

                    // ── b. Depth capture ────────────────────────────────────
                    depthSystem.CaptureDepth(
                        Path.Combine(depthPath, frameName + ".png"),   // PNG (not used downstream but kept for debug)
                        Path.Combine(depthPath, frameName + ".npy"),
                        config.imageWidth,
                        config.imageHeight
                    );

                    // ── c. Segmentation mask ────────────────────────────────
                    RenderTexture maskRT = RenderSegmentationMask();
                    SaveRenderTextureToPNG(maskRT, Path.Combine(masksPath, frameName + ".png"));

                    // ── d. YOLO labels (from mask) ──────────────────────────
                    List<string> yoloLines = ExtractYoloLabels(maskRT);
                    frameYoloLines.Add(yoloLines);
                    File.WriteAllLines(Path.Combine(labelsPath, frameName + ".txt"), yoloLines);

                    maskRT.Release();

                    // ── e. Camera parameters ────────────────────────────────
                    frameCameras.Add(new MultiViewCameraController.FrameCameraData
                    {
                        frame_index = f,
                        intrinsics  = cameraController.GetIntrinsics(),
                        extrinsics  = cameraController.GetExtrinsics()
                    });

                    yield return null;
                }

                // ── 4. Save cameras.json ───────────────────────────────────
                SaveCamerasJson(frameCameras, Path.Combine(samplePath, "cameras.json"));

                // ── 5. Python post-processing ──────────────────────────────
                if (config.autoPostProcess)
                    RunPythonPostProcess(samplePath);

                // ── 6. Clear scene for next sample ─────────────────────────
                sceneSetup.ClearScene();
                yield return null;

                Debug.Log($"[MultiViewSampleGenerator] ✓ Sample {s} complete.");
            }

            isGenerating = false;
            Debug.Log($"[MultiViewSampleGenerator] ✅ All {totalSamples} samples generated.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Segmentation Color Assignment
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Assign a unique, stable segmentation color to every strawberry
        /// (peduncles are skipped). The mapping is stored in colorMap for saving.
        /// </summary>
        private void AssignGlobalColorIds(StrawberryBushInstance bushInstance)
        {
            foreach (var s in bushInstance.strawberries)
            {
                var segId = s.gameObject.GetComponent<StrawberrySegmentationId>();
                if (segId == null) continue;

                // Color already encodes instanceId via GetSegmentationColor()
                Color c = segId.GetSegmentationColor();
                colorMap[segId.instanceId] = new SegInfo
                {
                    instance_id = segId.instanceId,
                    category_id = segId.categoryId,
                    ripeness    = s.ripenessState,
                    color       = new int[]
                    {
                        Mathf.RoundToInt(c.r * 255),
                        Mathf.RoundToInt(c.g * 255),
                        Mathf.RoundToInt(c.b * 255)
                    }
                };
            }
            // Peduncles intentionally omitted
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Rendering helpers
        // ══════════════════════════════════════════════════════════════════════

        private void CaptureRGB(string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                config.imageWidth, config.imageHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = mainCamera.targetTexture;
            mainCamera.targetTexture = rt;
            mainCamera.Render();
            mainCamera.targetTexture = prev;

            SaveRenderTextureToPNG(rt, path);
            RenderTexture.ReleaseTemporary(rt);
        }

        /// <summary>
        /// Render a segmentation mask using material substitution.
        /// Strawberries get their segmentation color; everything else is black.
        /// Peduncles are treated as background (black).
        /// Returns a temporary RenderTexture — caller must Release() it.
        /// </summary>
        private RenderTexture RenderSegmentationMask()
        {
            // ── Collect all renderers and save original materials ───────────
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var originals    = new Dictionary<Renderer, Material[]>();
            var tempMats     = new List<Material>();

            Shader unlitShader = Shader.Find("Custom/SegmentationColor")
                              ?? Shader.Find("Unlit/Color");

            // Default: black (background)
            Material blackMat = new Material(unlitShader);
            if (blackMat.HasProperty("_Color")) blackMat.SetColor("_Color", Color.black);
            tempMats.Add(blackMat);

            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                originals[r] = r.sharedMaterials;
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = blackMat;
                r.sharedMaterials = mats;
            }

            // ── Apply per-strawberry segmentation colors ────────────────────
            var bushInstance = sceneSetup.GetBushInstance();
            if (bushInstance != null)
            {
                foreach (var s in bushInstance.strawberries)
                {
                    var segId = s.gameObject.GetComponent<StrawberrySegmentationId>();
                    if (segId == null || s.renderers == null) continue;

                    Material segMat = new Material(unlitShader);
                    Color segColor  = segId.GetSegmentationColor();
                    if (segMat.HasProperty("_Color"))
                    {
                        // CRITICAL: In Linear color space projects, SetColor applies an sRGB->Linear 
                        // conversion which destroys small ID values (e.g. 1/255 becomes ~0.0000002).
                        // Use SetVector to pass the raw exact float values to the shader!
                        segMat.SetVector("_Color", new Vector4(segColor.r, segColor.g, segColor.b, segColor.a));
                    }
                    tempMats.Add(segMat);

                    foreach (var r in s.renderers)
                    {
                        if (r == null) continue;
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int j = 0; j < mats.Length; j++) mats[j] = segMat;
                        r.sharedMaterials = mats;
                    }
                }
                // Peduncles stay black — no action needed
            }

            // ── Render ──────────────────────────────────────────────────────
            Color       oldBg    = mainCamera.backgroundColor;
            CameraClearFlags oldFlags = mainCamera.clearFlags;
            bool        oldHDR   = mainCamera.allowHDR;
            bool        oldMSAA  = mainCamera.allowMSAA;
            int         oldAA    = QualitySettings.antiAliasing;

            mainCamera.backgroundColor = Color.black;
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.allowHDR        = false;
            mainCamera.allowMSAA       = false;
            QualitySettings.antiAliasing = 0;

            RenderTexture rt = RenderTexture.GetTemporary(
                config.imageWidth, config.imageHeight, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            mainCamera.targetTexture = rt;
            mainCamera.Render();
            mainCamera.targetTexture = null;

            // ── Restore ─────────────────────────────────────────────────────
            QualitySettings.antiAliasing = oldAA;
            foreach (var kvp in originals) if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
            foreach (var m in tempMats)    if (m != null)       DestroyImmediate(m);

            mainCamera.backgroundColor = oldBg;
            mainCamera.clearFlags      = oldFlags;
            mainCamera.allowHDR        = oldHDR;
            mainCamera.allowMSAA       = oldMSAA;

            return rt;
        }

        private void SaveRenderTextureToPNG(RenderTexture rt, string path)
        {
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            DestroyImmediate(tex);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  YOLO label extraction from mask
        // ══════════════════════════════════════════════════════════════════════

        private List<string> ExtractYoloLabels(RenderTexture maskRT)
        {
            var lines = new List<string>();

            RenderTexture.active = maskRT;
            Texture2D tex = new Texture2D(maskRT.width, maskRT.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, maskRT.width, maskRT.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            Color32[] pixels = tex.GetPixels32();
            int W = maskRT.width;
            int H = maskRT.height;

            // Gather per-instance pixel bounds
            var instanceBounds = new Dictionary<int, (int minX, int maxX, int minY, int maxY, int count, int catId)>();

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Color32 p = pixels[y * W + x];
                    // Only strawberries: R > 0, B ∈ {0,1,2}
                    if (p.r == 0) continue;

                    int instanceId = p.r;
                    int catId      = p.b; // 0,1,2

                    if (!instanceBounds.ContainsKey(instanceId))
                        instanceBounds[instanceId] = (x, x, y, y, 1, catId);
                    else
                    {
                        var d = instanceBounds[instanceId];
                        instanceBounds[instanceId] = (
                            Mathf.Min(d.minX, x),
                            Mathf.Max(d.maxX, x),
                            Mathf.Min(d.minY, y),
                            Mathf.Max(d.maxY, y),
                            d.count + 1,
                            d.catId
                        );
                    }
                }
            }

            DestroyImmediate(tex);

            foreach (var kvp in instanceBounds)
            {
                var d = kvp.Value;
                if (d.count < config.minPixelCount) continue;

                // YOLO: class cx cy w h (normalized, y flipped: image top = y=1 in Unity)
                float bx = (d.minX + d.maxX) / 2f / W;
                float by = 1f - (d.minY + d.maxY) / 2f / H; // flip Y
                float bw = (d.maxX - d.minX + 1f) / W;
                float bh = (d.maxY - d.minY + 1f) / H;

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} {1:F6} {2:F6} {3:F6} {4:F6}", d.catId, bx, by, bw, bh));
            }

            return lines;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  JSON serialization helpers
        // ══════════════════════════════════════════════════════════════════════

        private void SaveColorMap(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            int i = 0;
            foreach (var kvp in colorMap)
            {
                var info = kvp.Value;
                sb.Append($"  \"{kvp.Key}\": {{");
                sb.Append($"\"instance_id\": {info.instance_id}, ");
                sb.Append($"\"category_id\": {info.category_id}, ");
                sb.Append($"\"ripeness\": \"{info.ripeness}\", ");
                sb.Append($"\"color\": [{info.color[0]}, {info.color[1]}, {info.color[2]}]");
                sb.Append("}");
                if (++i < colorMap.Count) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private void SaveCamerasJson(
            List<MultiViewCameraController.FrameCameraData> frames, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < frames.Count; i++)
            {
                var f    = frames[i];
                var intr = f.intrinsics;
                var extr = f.extrinsics;

                sb.AppendLine("  {");
                sb.AppendLine($"    \"frame_index\": {f.frame_index},");
                sb.AppendLine($"    \"intrinsics\": {{");
                sb.AppendLine($"      \"fx\": {intr.fx.ToString("F4", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"fy\": {intr.fy.ToString("F4", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"cx\": {intr.cx.ToString("F4", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"cy\": {intr.cy.ToString("F4", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"width\": {intr.width},");
                sb.AppendLine($"      \"height\": {intr.height},");
                sb.AppendLine($"      \"near\": {intr.near.ToString("F6", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"far\": {intr.far.ToString("F4", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"fov_deg\": {intr.fov_deg.ToString("F6", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"    }},");
                sb.AppendLine($"    \"position\": [{Fmt(extr.position[0])}, {Fmt(extr.position[1])}, {Fmt(extr.position[2])}],");
                sb.AppendLine($"    \"rotation\": [{Fmt(extr.rotation[0])}, {Fmt(extr.rotation[1])}, {Fmt(extr.rotation[2])}, {Fmt(extr.rotation[3])}]");
                sb.Append("  }");
                if (i < frames.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString());
        }


        private static string Fmt(float v) =>
            v.ToString("F6", CultureInfo.InvariantCulture);

        // ══════════════════════════════════════════════════════════════════════
        //  Python post-processing launcher
        // ══════════════════════════════════════════════════════════════════════

        private void RunPythonPostProcess(string samplePath)
        {
            string scriptDir  = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Scripts"));
            string scriptPath = Path.Combine(scriptDir, "multi_view_postprocess.py");

            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[MultiViewSampleGenerator] Post-process script not found: {scriptPath}");
                return;
            }

            string python = string.IsNullOrEmpty(config.pythonExecutable)
                ? "python" : config.pythonExecutable;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = python,
                    Arguments              = $"\"{scriptPath}\" \"{samplePath}\"",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                // Non-blocking: let it run while Unity continues
                Debug.Log($"[MultiViewSampleGenerator] Python post-process started for {Path.GetFileName(samplePath)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MultiViewSampleGenerator] Failed to launch Python: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Utility
        // ══════════════════════════════════════════════════════════════════════

        private T GetOrAdd<T>() where T : Component
        {
            T comp = GetComponent<T>();
            if (comp == null) comp = gameObject.AddComponent<T>();
            return comp;
        }

        private void CleanupComponents()
        {
            if (sceneSetup != null) sceneSetup.ClearScene();
        }

        private void OnDestroy() => CleanupComponents();
    }
}
