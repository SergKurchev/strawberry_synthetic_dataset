using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace StrawberryDataset
{
    /// <summary>
    /// Generates annotations in YOLO and COCO formats.
    /// Handles occlusion detection and visibility filtering.
    /// </summary>
    public class AnnotationGenerator : MonoBehaviour
    {
        [System.Serializable]
        public class ImageAnnotation
        {
            public int id;
            public string file_name;
            public int width;
            public int height;
        }
        
        [System.Serializable]
        public class ObjectAnnotation
        {
            public int id;
            public int image_id;
            public int category_id;
            public int instance_id;
            public int parent_id;
            public float[] bbox;
            public float area;
            public int[] segmentation_color;
            public float visibility_ratio;
            public string ripeness;
        }
        
        private Camera targetCamera;
        private int imageWidth;
        private int imageHeight;
        
        // Global annotation ID counter (persists across images)
        private static int globalAnnotationIdCounter = 1;
        
        public void Initialize(Camera camera, int width, int height)
        {
            targetCamera = camera;
            imageWidth = width;
            imageHeight = height;
        }
        
        /// <summary>
        /// Reset global annotation ID counter (call at start of dataset generation)
        /// </summary>
        public static void ResetAnnotationIdCounter()
        {
            globalAnnotationIdCounter = 1;
        }

        /// <summary>
        /// Set global annotation ID counter (used for resuming generation)
        /// </summary>
        public static void SetAnnotationIdCounter(int value)
        {
            globalAnnotationIdCounter = value;
        }

        private struct InstancePixelData
        {
            public int pixelCount;
            public int minX, maxX, minY, maxY;

            public static InstancePixelData Create()
            {
                return new InstancePixelData
                {
                    pixelCount = 0,
                    minX = int.MaxValue, maxX = int.MinValue,
                    minY = int.MaxValue, maxY = int.MinValue
                };
            }

            public void AddPixel(int x, int y)
            {
                pixelCount++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        
        /// <summary>
        /// Generate annotations for current frame with occlusion detection
        /// </summary>
        public List<ObjectAnnotation> GenerateAnnotations(
            int imageId,
            StrawberrySceneGenerator sceneGenerator,
            int minPixelCount,
            out RenderTexture segmentationMask)
        {
            // 1. Render segmentation mask
            segmentationMask = RenderSegmentationMask(sceneGenerator);
            
            // 2. Calculate pixel data and bounding boxes directly from the mask
            Dictionary<int, InstancePixelData> instanceData = CalculateMaskData(segmentationMask);
            
            // 3. Generate annotations
            List<ObjectAnnotation> annotations = new List<ObjectAnnotation>();
            // Use global counter instead of resetting to 1 for each image
            
            foreach (var bush in sceneGenerator.spawnedBushes)
            {
                var targetObjects = new List<(GameObject obj, string ripeness)>();
                foreach (var s in bush.strawberries) targetObjects.Add((s.gameObject, s.ripenessState));
                foreach (var p in bush.peduncles) targetObjects.Add((p.gameObject, "peduncle"));

                foreach (var (obj, ripeness) in targetObjects)
                {
                    var segId = obj.GetComponent<StrawberrySegmentationId>();
                    if (segId == null) continue;
                    
                    // Internal ID mapping for unique lookup
                    int lookupId = segId.instanceId;
                    if (segId.categoryId == 3) lookupId += 1000;
                    
                    if (!instanceData.ContainsKey(lookupId)) continue;
                    
                    var data = instanceData[lookupId];
                    // Filter by configurable minimum pixel count
                    if (data.pixelCount < minPixelCount) continue;

                    int bw = data.maxX - data.minX + 1;
                    int bh = data.maxY - data.minY + 1;
                    float cocoX = data.minX;
                    // CRITICAL: Flip Y-axis (Unity bottom-up → Image top-down)
                    // minY is top of bbox in Unity, but should be top in image coords too
                    float cocoY = (imageHeight - 1) - data.maxY; // Top-left corner in image coords
                    
                    Color segColor = segId.GetSegmentationColor();
                    annotations.Add(new ObjectAnnotation
                    {
                        id = globalAnnotationIdCounter++,
                        image_id = imageId,
                        category_id = segId.categoryId,
                        instance_id = segId.instanceId,
                        parent_id = segId.parentId,
                        bbox = new float[] { cocoX, cocoY, bw, bh },
                        area = data.pixelCount,
                        segmentation_color = new int[] {
                            Mathf.RoundToInt(segColor.r * 255),
                            Mathf.RoundToInt(segColor.g * 255),
                            Mathf.RoundToInt(segColor.b * 255)
                        },
                        visibility_ratio = 1.0f,
                        ripeness = ripeness
                    });
                }
            }
            
            return annotations;
        }
        
        private RenderTexture RenderSegmentationMask(StrawberrySceneGenerator sceneGenerator)
        {
            Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
            List<Material> tempMaterials = new List<Material>();
            Shader unlitShader = Shader.Find("Custom/SegmentationColor") ?? Shader.Find("Unlit/Color");
            
            Material blackMat = new Material(unlitShader);
            if (blackMat.HasProperty("_Color")) blackMat.SetColor("_Color", Color.black);
            tempMaterials.Add(blackMat);

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                if (r == null) continue;
                originalMaterials[r] = r.sharedMaterials;
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = blackMat;
                r.sharedMaterials = mats;
            }
            
            foreach (var bush in sceneGenerator.spawnedBushes) {
                var targetObjects = new List<(Renderer[] renderers, StrawberrySegmentationId sid)>();
                foreach (var s in bush.strawberries) targetObjects.Add((s.renderers, s.gameObject.GetComponent<StrawberrySegmentationId>()));
                foreach (var p in bush.peduncles) targetObjects.Add((p.renderers, p.gameObject.GetComponent<StrawberrySegmentationId>()));

                foreach (var (renderers, sid) in targetObjects) {
                    if (renderers == null || sid == null) continue;
                    Material segMat = new Material(unlitShader);
                    Color segColor = sid.GetSegmentationColor();
                    if (segMat.HasProperty("_Color")) segMat.SetColor("_Color", segColor);
                    tempMaterials.Add(segMat);
                    
                    foreach (var r in renderers) {
                        if (r == null) continue;
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int j = 0; j < mats.Length; j++) mats[j] = segMat;
                        r.sharedMaterials = mats;
                    }
                }
            }
            
            Color oldBg = targetCamera.backgroundColor;
            CameraClearFlags oldFlags = targetCamera.clearFlags;
            bool oldHDR = targetCamera.allowHDR;
            bool oldMSAA = targetCamera.allowMSAA;
            bool oldOcclusion = targetCamera.useOcclusionCulling;
            
            targetCamera.backgroundColor = Color.black;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.allowHDR = false;
            targetCamera.allowMSAA = false;
            targetCamera.useOcclusionCulling = false;
            int oldAA = QualitySettings.antiAliasing;
            QualitySettings.antiAliasing = 0;
            
            RenderTexture rt = RenderTexture.GetTemporary(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            targetCamera.targetTexture = rt;
            targetCamera.Render();
            targetCamera.targetTexture = null;
            QualitySettings.antiAliasing = oldAA;
            
            foreach (var kvp in originalMaterials) if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
            foreach (var mat in tempMaterials) if (mat != null) DestroyImmediate(mat);
            
            targetCamera.backgroundColor = oldBg;
            targetCamera.clearFlags = oldFlags;
            targetCamera.allowHDR = oldHDR;
            targetCamera.allowMSAA = oldMSAA;
            targetCamera.useOcclusionCulling = oldOcclusion;
            return rt;
        }

        private Dictionary<int, InstancePixelData> CalculateMaskData(RenderTexture rt)
        {
            Dictionary<int, InstancePixelData> visiblePixels = new Dictionary<int, InstancePixelData>();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Color32[] pixels = tex.GetPixels32();
            for (int y = 0; y < rt.height; y++) {
                for (int x = 0; x < rt.width; x++) {
                    Color32 p = pixels[y * rt.width + x];
                    int id = -1;
                    if (p.b == 3) id = 1000 + p.g; // Peduncle
                    else if (p.r > 0 || p.g > 0 || p.b > 0) id = p.r; // Strawberry (any non-black pixel with B!=3)

                    if (id >= 0) {
                        if (!visiblePixels.ContainsKey(id)) visiblePixels[id] = InstancePixelData.Create();
                        var d = visiblePixels[id]; d.AddPixel(x, y); visiblePixels[id] = d;
                    }
                }
            }
            DestroyImmediate(tex);
            return visiblePixels;
        }
        
        /// <summary>
        /// Save annotations to YOLO format file
        /// </summary>
        public void SaveYOLOAnnotations(List<ObjectAnnotation> annotations, string path)
        {
            StringBuilder sb = new StringBuilder();
            
            foreach (var ann in annotations)
            {
                // YOLO format: class_id center_x center_y width height (normalized)
                float centerX = (ann.bbox[0] + ann.bbox[2] / 2f) / imageWidth;
                float centerY = (ann.bbox[1] + ann.bbox[3] / 2f) / imageHeight;
                float width = ann.bbox[2] / imageWidth;
                float height = ann.bbox[3] / imageHeight;
                
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} {1:F6} {2:F6} {3:F6} {4:F6}",
                    ann.category_id, centerX, centerY, width, height));
            }
            
            File.WriteAllText(path, sb.ToString());
        }
        
        /// <summary>
        /// Save COCO-style annotations JSON
        /// </summary>
        public static void SaveCOCOAnnotations(
            List<ImageAnnotation> images,
            List<ObjectAnnotation> annotations,
            string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            
            // Images
            sb.AppendLine("  \"images\": [");
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                sb.Append($"    {{\"id\": {img.id}, \"file_name\": \"{img.file_name}\", \"width\": {img.width}, \"height\": {img.height}}}");
                if (i < images.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("  ],");
            
            // Categories
            sb.AppendLine("  \"categories\": [");
            sb.AppendLine("    {\"id\": 0, \"name\": \"strawberry_ripe\"},");
            sb.AppendLine("    {\"id\": 1, \"name\": \"strawberry_unripe\"},");
            sb.AppendLine("    {\"id\": 2, \"name\": \"strawberry_half_ripe\"},");
            sb.AppendLine("    {\"id\": 3, \"name\": \"peduncle\"}");
            sb.AppendLine("  ],");
            
            // Annotations
            sb.AppendLine("  \"annotations\": [");
            for (int i = 0; i < annotations.Count; i++)
            {
                var ann = annotations[i];
                string bboxStr = string.Format(CultureInfo.InvariantCulture,
                    "[{0:F2}, {1:F2}, {2:F2}, {3:F2}]",
                    ann.bbox[0], ann.bbox[1], ann.bbox[2], ann.bbox[3]);
                string areaStr = ann.area.ToString("F2", CultureInfo.InvariantCulture);
                string visStr = ann.visibility_ratio.ToString("F2", CultureInfo.InvariantCulture);
                
                sb.Append($"    {{\"id\": {ann.id}, \"image_id\": {ann.image_id}, \"category_id\": {ann.category_id}, ");
                sb.Append($"\"instance_id\": {ann.instance_id}, \"parent_id\": {ann.parent_id}, ");
                sb.Append($"\"bbox\": {bboxStr}, ");
                sb.Append($"\"area\": {areaStr}, ");
                sb.Append($"\"visibility_ratio\": {visStr}, ");
                sb.Append($"\"ripeness\": \"{ann.ripeness}\", ");
                sb.Append($"\"segmentation_color\": [{ann.segmentation_color[0]}, {ann.segmentation_color[1]}, {ann.segmentation_color[2]}]}}");
                if (i < annotations.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("  ]");
            
            sb.AppendLine("}");
            
            File.WriteAllText(path, sb.ToString());
        }
    }
}
