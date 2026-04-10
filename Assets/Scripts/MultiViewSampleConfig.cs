using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Configuration for the new multi-view sample generation pipeline.
    /// One sample = N frames from random camera positions around a single bush.
    /// Segmentation: ripe / half_ripe / unripe only (no peduncles).
    /// </summary>
    [CreateAssetMenu(fileName = "MultiViewSampleConfig", menuName = "Dataset/Multi-View Sample Config")]
    public class MultiViewSampleConfig : ScriptableObject
    {
        [Header("Bush Prefab")]
        [Tooltip("Reference to the strawberry bush prefab (same as used in old pipeline)")]
        public GameObject strawberryBushPrefab;

        [Header("Room Settings")]
        [Tooltip("Side length of the cubic white room in meters (floor=black, walls+ceiling=white)")]
        public float roomSideLength = 1.0f;

        [Tooltip("Uniform scale applied to the spawned bush (1.0 = original size, 0.3 = 30% of original)")]
        [Range(0.05f, 2.0f)]
        public float bushScale = 0.3f;

        [Header("Camera Settings")]
        [Tooltip("Camera field of view in degrees")]
        public float cameraFOV = 60f;

        [Tooltip("Minimum distance from camera to bush center in meters")]
        public float minCameraDistance = 0.30f;

        [Tooltip("Maximum distance from camera to bush center in meters")]
        public float maxCameraDistance = 1.50f;

        [Tooltip("Fraction to shrink the bush bounding box inward on each side before picking look-at target (0.10 = 10%)")]
        [Range(0f, 0.49f)]
        public float bboxShrinkFactor = 0.10f;

        [Header("Frame / Sample Counts")]
        [Tooltip("Number of frames captured per sample")]
        public int framesPerSample = 20;

        [Tooltip("Total number of samples to generate")]
        public int totalSamples = 10;

        [Header("Image Settings")]
        [Tooltip("Output image width in pixels")]
        public int imageWidth = 1024;

        [Tooltip("Output image height in pixels")]
        public int imageHeight = 1024;

        [Header("Output")]
        [Tooltip("Base output folder relative to project root")]
        public string outputFolder = "multiview_dataset";

        [Tooltip("Minimum number of visible pixels for a strawberry to be included in annotations")]
        [Range(5, 500)]
        public int minPixelCount = 15;

        [Header("Post-Processing")]
        [Tooltip("Automatically run Python post-processing after each sample")]
        public bool autoPostProcess = true;

        [Tooltip("Path to python executable (leave empty to use 'python' from PATH)")]
        public string pythonExecutable = "python";
    }
}
