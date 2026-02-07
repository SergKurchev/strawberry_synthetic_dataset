using UnityEngine;
using System.Collections.Generic;

namespace StrawberryDataset
{
    /// <summary>
    /// Generates visualization overlays for dataset samples.
    /// Creates BBox visualizations and mask overlays.
    /// </summary>
    public class VisualizationGenerator : MonoBehaviour
    {
        private static readonly Color[] CATEGORY_COLORS = new Color[]
        {
            new Color(0.0f, 1.0f, 0.0f, 1.0f),  // 0: ripe (green)
            new Color(1.0f, 0.0f, 0.0f, 1.0f),  // 1: unripe (red)
            new Color(1.0f, 0.5f, 0.0f, 1.0f),  // 2: half_ripe (orange)
            new Color(0.6f, 0.4f, 0.2f, 1.0f)   // 3: peduncle (brown)
        };
        
        private static readonly string[] CATEGORY_NAMES = new string[]
        {
            "ripe", "unripe", "half", "ped"
        };
        
        /// <summary>
        /// Generate BBox visualization (RGB image with drawn bounding boxes)
        /// </summary>
        public Texture2D GenerateBBoxVisualization(
            RenderTexture rgbSource,
            List<AnnotationGenerator.ObjectAnnotation> annotations,
            int width,
            int height)
        {
            // Copy RGB to Texture2D
            RenderTexture.active = rgbSource;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            
            // Draw bounding boxes
            Color[] pixels = result.GetPixels();
            
            foreach (var ann in annotations)
            {
                // Convert COCO bbox to Unity coordinates
                int x = Mathf.RoundToInt(ann.bbox[0]);
                int y = Mathf.RoundToInt(ann.bbox[1]);
                int w = Mathf.RoundToInt(ann.bbox[2]);
                int h = Mathf.RoundToInt(ann.bbox[3]);
                
                // Convert Y (COCO uses bottom-left origin, Unity uses bottom-left for pixels)
                // Note: COCO Y is already flipped in AnnotationGenerator
                
                Color boxColor = CATEGORY_COLORS[ann.category_id];
                int lineThickness = 2;
                
                // Draw rectangle (4 lines)
                DrawLine(pixels, width, height, x, y, x + w, y, boxColor, lineThickness);                 // Top
                DrawLine(pixels, width, height, x, y + h, x + w, y + h, boxColor, lineThickness);         // Bottom
                DrawLine(pixels, width, height, x, y, x, y + h, boxColor, lineThickness);                 // Left
                DrawLine(pixels, width, height, x + w, y, x + w, y + h, boxColor, lineThickness);         // Right
                
                // Draw label background and text
                string label = $"{CATEGORY_NAMES[ann.category_id]} {ann.instance_id}";
                int labelY = Mathf.Max(0, y - 15);
                DrawLabelBackground(pixels, width, height, x, labelY, label.Length * 7, 12, boxColor);
            }
            
            result.SetPixels(pixels);
            result.Apply();
            
            return result;
        }
        
        /// <summary>
        /// Generate mask overlay visualization (RGB + semi-transparent mask)
        /// </summary>
        public Texture2D GenerateMaskOverlay(
            RenderTexture rgbSource,
            RenderTexture maskSource,
            Color overlayColor,
            int width,
            int height)
        {
            // Read RGB
            RenderTexture.active = rgbSource;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = null;
            
            // Read mask
            RenderTexture.active = maskSource;
            Texture2D maskTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            maskTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = null;
            
            // Blend
            Color[] rgbPixels = result.GetPixels();
            Color[] maskPixels = maskTex.GetPixels();
            
            for (int i = 0; i < rgbPixels.Length; i++)
            {
                // Check if mask pixel is not background (0,0,0)
                if (maskPixels[i].r > 0.01f || maskPixels[i].g > 0.01f || maskPixels[i].b > 0.01f)
                {
                    // Blend with overlay color
                    rgbPixels[i] = Color.Lerp(rgbPixels[i], overlayColor, overlayColor.a);
                }
            }
            
            result.SetPixels(rgbPixels);
            result.Apply();
            
            DestroyImmediate(maskTex);
            
            return result;
        }
        
        /// <summary>
        /// Draw a line using Bresenham's algorithm
        /// </summary>
        private void DrawLine(Color[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            
            int x = x0;
            int y = y0;
            
            while (true)
            {
                // Draw thick line
                for (int ty = -thickness/2; ty <= thickness/2; ty++)
                {
                    for (int tx = -thickness/2; tx <= thickness/2; tx++)
                    {
                        int px = x + tx;
                        int py = y + ty;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            // CRITICAL: Flip Y for texture pixel array (bottom-left origin)
                            int flippedY = height - 1 - py;
                            pixels[flippedY * width + px] = color;
                        }
                    }
                }
                
                if (x == x1 && y == y1) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
        
        /// <summary>
        /// Draw label background rectangle
        /// </summary>
        private void DrawLabelBackground(Color[] pixels, int width, int height, int x, int y, int w, int h, Color color)
        {
            Color bgColor = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 0.7f);
            
            for (int py = y; py < y + h && py < height; py++)
            {
                for (int px = x; px < x + w && px < width; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        // CRITICAL: Flip Y for texture pixel array (bottom-left origin)
                        int flippedY = height - 1 - py;
                        int idx = flippedY * width + px;
                        pixels[idx] = Color.Lerp(pixels[idx], bgColor, 0.7f);
                    }
                }
            }
        }
    }
}
