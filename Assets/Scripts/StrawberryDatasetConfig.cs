using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Configuration for strawberry dataset generation.
    /// Stores all parameters for scene generation, camera settings, and output options.
    /// </summary>
    [CreateAssetMenu(fileName = "StrawberryDatasetConfig", menuName = "Dataset/Strawberry Dataset Config")]
    public class StrawberryDatasetConfig : ScriptableObject
    {
        [Header("Strawberry Bush Settings")]
        [Tooltip("Reference to the strawberry bush prefab template")]
        public GameObject strawberryBushPrefab;
        
        [Header("Scene Generation")]
        [Tooltip("Minimum number of bushes per scene")]
        [Range(10, 14)]
        public int minBushCount = 10;
        
        [Tooltip("Maximum number of bushes per scene")]
        [Range(10, 14)]
        public int maxBushCount = 14;
        
        [Header("Bush Size Constraints")]
        [Tooltip("Minimum bush height in meters")]
        public float minBushHeight = 0.10f; // 10 cm
        
        [Tooltip("Maximum bush height in meters")]
        public float maxBushHeight = 0.15f; // 15 cm
        
        [Header("Bush Spacing")]
        [Tooltip("Minimum gap between bushes in meters")]
        public float minBushGap = 0.00f; // 0 cm
        
        [Tooltip("Maximum gap between bushes in meters")]
        public float maxBushGap = 0.05f; // 5 cm
        
        [Header("Bush Rotation & Tilt")]
        [Tooltip("Maximum tilt angle from vertical in degrees")]
        public float maxTiltAngle = 10f;
        
        [Header("Position Variation")]
        [Tooltip("Maximum horizontal offset from row centerline in meters")]
        public float maxHorizontalOffset = 0.02f; // 2 cm
        
        [Tooltip("Maximum vertical offset from base height in meters")]
        public float maxVerticalOffset = 0.01f; // 1 cm
        
        [Header("Greenhouse Settings")]
        [Tooltip("Padding around bush row for greenhouse walls in meters")]
        public float greenhousePadding = 2.0f;
        
        [Tooltip("Greenhouse wall height in meters")]
        public float greenhouseHeight = 3.0f;
        
        [Tooltip("Available wall colors for greenhouse")]
        public Color[] greenhouseWallColors = new Color[] 
        { 
            new Color(0.9f, 0.9f, 0.9f), // White
            new Color(0.8f, 0.8f, 0.8f), // Light Gray
            new Color(0.9f, 0.9f, 0.8f), // Beige
            new Color(0.8f, 0.9f, 0.8f)  // Light Green
        };
        [Tooltip("Optional: Assign a material to use for walls (overrides generated material)")]
        public Material wallMaterial;
        [Tooltip("Optional: Assign a material to use for the floor")]
        public Material floorMaterial;
        [Tooltip("Optional: Assign a material to use for the roof")]
        public Material roofMaterial;
        
        [Header("Lighting Settings")]
        [Tooltip("Minimum light elevation angle in degrees")]
        public float minLightElevation = 30f;
        
        [Tooltip("Maximum light elevation angle in degrees")]
        public float maxLightElevation = 70f;
        
        [Tooltip("Minimum light intensity")]
        public float minLightIntensity = 0.7f;
        
        [Tooltip("Maximum light intensity")]
        public float maxLightIntensity = 1.2f;
        
        [Header("Camera Settings")]
        [Tooltip("Field of view in degrees")]
        public float cameraFOV = 60f;
        
        [Tooltip("Minimum distance from any object (collision prevention)")]
        public float minCameraObjectDistance = 0.2f;
        
        [Header("Camera Positioning (NEW)")]
        [Tooltip("Minimum distance from targeted bush in meters")]
        public float minCameraToBushDistance = 0.3f;
        
        [Tooltip("Maximum distance from targeted bush in meters")]
        public float maxCameraToBushDistance = 1.5f;
        
        [Tooltip("Percentage of shots that focus on a specific bush (0-1)")]
        [Range(0f, 1f)]
        public float bushFocusedShotRatio = 0.7f;
        
        [Tooltip("Prevent camera frustum from extending outside greenhouse bounds")]
        public bool preventGreenhouseExit = false; // TEMPORARY: Disabled for testing
        
        [Tooltip("Maximum upward camera angle in degrees (positive = more freedom to look up)")]
        [Range(0f, 45f)]
        public float maxUpwardPitchAngle = 15f;
        
        [Tooltip("Maximum downward camera angle in degrees")]
        [Range(30f, 90f)]
        public float maxDownwardPitchAngle = 60f;
        
        [Tooltip("Maximum camera height above bush center in meters (prevents seeing sky)")]
        [Range(0.2f, 1.5f)]
        public float maxCameraHeightAboveBush = 0.4f;
        
        [Header("Sample Validation")]
        [Tooltip("Minimum percentage of image covered by bushes (0-1). Samples below this are rejected.")]
        [Range(0f, 0.5f)]
        public float minCoverageRatio = 0.005f; // 0.5% (TEMPORARY: Lowered for testing)
        
        [Header("Visualizations")]
        [Tooltip("Save visualization images (bbox + mask overlay)")]
        public bool saveVisualizations = true;
        
        [Tooltip("Color for mask overlay visualization (RGBA)")]
        public Color maskOverlayColor = new Color(0.2f, 0.4f, 0.9f, 0.4f); // Semi-transparent blue
        
        [Header("Dataset Generation")]
        [Tooltip("Total number of images to generate")]
        public int totalImages = 1000;
        
        [Tooltip("Number of images before switching scene layout")]
        public int imagesPerScene = 50;
        
        [Tooltip("Maximum retry attempts per image before giving up (prevents infinite loops)")]
        [Range(5, 100)]
        public int maxRetriesPerImage = 30;

        [Header("Post Processing")]
        [Tooltip("Automatically run visualization script after generation")]
        public bool autoVisualize = true;
        
        [Header("Image Settings")]
        [Tooltip("Output image width")]
        public int imageWidth = 1024;
        
        [Tooltip("Output image height")]
        public int imageHeight = 1024;
        
        [Header("Output Paths")]
        [Tooltip("Base output folder relative to project root")]
        public string outputFolder = "strawberry_dataset";
        
        [Header("Object Filtering")]
        [Tooltip("Minimum number of visible pixels to include object in annotations")]
        [Range(5, 10000)]
        public int minPixelCount = 15;
    }
}
