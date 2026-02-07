using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Quick setup helper for strawberry dataset generator.
    /// Attach this to any GameObject and click the button in Inspector.
    /// </summary>
    public class QuickSetup : MonoBehaviour
    {
        [Header("Required References")]
        [Tooltip("Drag the strawberry bush prefab here")]
        public GameObject strawberryBushPrefab;
        
        [Header("Optional")]
        [Tooltip("Leave empty to auto-find or create")]
        public StrawberryDatasetConfig config;
        
        [ContextMenu("Setup Everything")]
        public void SetupEverything()
        {
            Debug.Log("=== Quick Setup Started ===");
            
            // Find or create config
            if (config == null)
            {
                config = FindObjectOfType<StrawberryDatasetConfig>();
                
#if UNITY_EDITOR
                if (config == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:StrawberryDatasetConfig");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        config = UnityEditor.AssetDatabase.LoadAssetAtPath<StrawberryDatasetConfig>(path);
                        Debug.Log($"Found config: {path}");
                    }
                }
                
                if (config == null)
                {
                    Debug.LogError("No config found! Please create one via Tools > Strawberry Dataset Generator");
                    return;
                }
#endif
            }
            
            // Assign prefab to config
            if (strawberryBushPrefab != null)
            {
                config.strawberryBushPrefab = strawberryBushPrefab;
                Debug.Log($"Assigned prefab to config: {strawberryBushPrefab.name}");
                
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(config);
                UnityEditor.AssetDatabase.SaveAssets();
#endif
            }
            else
            {
                Debug.LogWarning("Strawberry bush prefab not assigned! Please drag it to this component.");
            }
            
            // Find or create generator
            var generator = FindObjectOfType<StrawberryDatasetBatchGenerator>();
            if (generator == null)
            {
                GameObject genObj = new GameObject("StrawberryDatasetGenerator");
                generator = genObj.AddComponent<StrawberryDatasetBatchGenerator>();
                Debug.Log("Created StrawberryDatasetBatchGenerator");
            }
            
            // Assign config to generator
            generator.config = config;
            
            // Find or create camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                Debug.Log("Created Main Camera");
            }
            
            generator.mainCamera = cam;
            
            Debug.Log("=== Quick Setup Complete! ===");
            Debug.Log("You can now use Tools > Strawberry Dataset Generator");
            
#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = generator.gameObject;
#endif
        }
        
        [ContextMenu("Test Scene Generation")]
        public void TestSceneGeneration()
        {
            var generator = FindObjectOfType<StrawberryDatasetBatchGenerator>();
            if (generator == null)
            {
                Debug.LogError("No generator found! Run 'Setup Everything' first.");
                return;
            }
            
            if (generator.sceneGenerator == null)
            {
                generator.sceneGenerator = generator.gameObject.AddComponent<StrawberrySceneGenerator>();
            }
            
            generator.sceneGenerator.config = config;
            generator.sceneGenerator.GenerateScene();
            
            Debug.Log("Test scene generated!");
        }
    }
}
