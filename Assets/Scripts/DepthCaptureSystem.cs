using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace StrawberryDataset
{
    /// <summary>
    /// Captures depth maps in meters and saves camera parameters.
    /// Compatible with NYU-Depth V2 format.
    /// </summary>
    public class DepthCaptureSystem : MonoBehaviour
    {
        [System.Serializable]
        public class CameraIntrinsics
        {
            public float fx;
            public float fy;
            public float cx;
            public float cy;
        }
        
        [System.Serializable]
        public class CameraExtrinsics
        {
            public float[] position = new float[3];
            public float[] rotation = new float[4]; // Quaternion (x, y, z, w)
        }
        
        [System.Serializable]
        public class DepthRange
        {
            public float min_meters;
            public float max_meters;
        }
        
        [System.Serializable]
        public class DepthMetadata
        {
            public CameraIntrinsics camera_intrinsics;
            public CameraExtrinsics camera_extrinsics;
            public DepthRange depth_range;
        }
        
        private Camera targetCamera;
        private Shader depthShader;
        
        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            depthShader = Shader.Find("Hidden/DepthCapture");
            
            if (depthShader == null)
            {
                Debug.LogError("DepthCapture shader not found! Make sure it exists in Assets/Shaders/");
            }
        }
        
        /// <summary>
        /// Capture depth map and save as PNG (16-bit) and NPY (float32)
        /// </summary>
        public DepthMetadata CaptureDepth(string pngPath, string npyPath, int width, int height)
        {
            if (targetCamera == null)
            {
                Debug.LogError("Camera not initialized");
                return null;
            }
            
            // Render depth to texture
            RenderTexture depthRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.RFloat);
            
            RenderTexture previousRT = targetCamera.targetTexture;
            CameraClearFlags previousClearFlags = targetCamera.clearFlags;
            Color previousBgColor = targetCamera.backgroundColor;
            
            targetCamera.targetTexture = depthRT;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = Color.black;
            
            // Render with depth replacement shader
            targetCamera.RenderWithShader(depthShader, "");
            
            // Read depth values
            RenderTexture.active = depthRT;
            Texture2D depthTex = new Texture2D(width, height, TextureFormat.RFloat, false);
            depthTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            depthTex.Apply();
            
            // Convert to metric depth
            float[] depthData = new float[width * height];
            Color[] pixels = depthTex.GetPixels();
            
            float nearPlane = targetCamera.nearClipPlane;
            float farPlane = targetCamera.farClipPlane;
            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;
            
            for (int i = 0; i < pixels.Length; i++)
            {
                // Convert normalized depth to metric depth
                float normalizedDepth = pixels[i].r;
                float linearDepth = normalizedDepth * (farPlane - nearPlane) + nearPlane;
                depthData[i] = linearDepth;
                
                if (linearDepth > 0.01f) // Ignore near-zero values
                {
                    minDepth = Mathf.Min(minDepth, linearDepth);
                    maxDepth = Mathf.Max(maxDepth, linearDepth);
                }
            }
            
            // Save as 16-bit PNG (millimeters)
            SaveDepthAsPNG(depthData, pngPath, width, height);
            
            // Save as NPY (meters)
            SaveDepthAsNPY(depthData, npyPath, width, height);
            
            // Cleanup
            RenderTexture.active = null;
            targetCamera.targetTexture = previousRT;
            targetCamera.clearFlags = previousClearFlags;
            targetCamera.backgroundColor = previousBgColor;
            
            RenderTexture.ReleaseTemporary(depthRT);
            DestroyImmediate(depthTex);
            
            // Extract camera parameters
            DepthMetadata metadata = new DepthMetadata
            {
                camera_intrinsics = ExtractIntrinsics(width, height),
                camera_extrinsics = ExtractExtrinsics(),
                depth_range = new DepthRange
                {
                    min_meters = minDepth,
                    max_meters = maxDepth
                }
            };
            
            return metadata;
        }
        
        /// <summary>
        /// Save depth as 16-bit PNG (values in millimeters)
        /// </summary>
        private void SaveDepthAsPNG(float[] depthData, string path, int width, int height)
        {
            Texture2D depthImage = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] colors = new Color[depthData.Length];
            
            for (int i = 0; i < depthData.Length; i++)
            {
                // Convert meters to millimeters and encode in RGB
                ushort depthMM = (ushort)Mathf.Clamp(depthData[i] * 1000f, 0, 65535);
                
                // Encode 16-bit value in two 8-bit channels
                byte high = (byte)(depthMM >> 8);
                byte low = (byte)(depthMM & 0xFF);
                
                colors[i] = new Color(high / 255f, low / 255f, 0f);
            }
            
            depthImage.SetPixels(colors);
            depthImage.Apply();
            
            byte[] bytes = depthImage.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            
            DestroyImmediate(depthImage);
        }
        
        /// <summary>
        /// Save depth as NPY file (float32 array in meters)
        /// </summary>
        private void SaveDepthAsNPY(float[] depthData, string path, int width, int height)
        {
            // Simple NPY format writer for 2D float32 array
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                // NPY header
                writer.Write((byte)0x93); // Magic number
                writer.Write("NUMPY".ToCharArray());
                writer.Write((byte)1); // Major version
                writer.Write((byte)0); // Minor version
                
                string header = $"{{'descr': '<f4', 'fortran_order': False, 'shape': ({height}, {width}), }}";
                int headerLen = header.Length;
                int padding = 64 - ((10 + headerLen) % 64);
                header += new string(' ', padding);
                
                writer.Write((ushort)header.Length);
                writer.Write(header.ToCharArray());
                
                // Data
                foreach (float depth in depthData)
                {
                    writer.Write(depth);
                }
            }
        }
        
        /// <summary>
        /// Extract camera intrinsic parameters
        /// </summary>
        private CameraIntrinsics ExtractIntrinsics(int width, int height)
        {
            float fov = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float fy = (height / 2f) / Mathf.Tan(fov / 2f);
            float fx = fy; // Assuming square pixels
            
            return new CameraIntrinsics
            {
                fx = fx,
                fy = fy,
                cx = width / 2f,
                cy = height / 2f
            };
        }
        
        /// <summary>
        /// Extract camera extrinsic parameters
        /// </summary>
        private CameraExtrinsics ExtractExtrinsics()
        {
            Vector3 pos = targetCamera.transform.position;
            Quaternion rot = targetCamera.transform.rotation;
            
            return new CameraExtrinsics
            {
                position = new float[] { pos.x, pos.y, pos.z },
                rotation = new float[] { rot.x, rot.y, rot.z, rot.w }
            };
        }
    }
}
