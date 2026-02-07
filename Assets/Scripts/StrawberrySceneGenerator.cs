using UnityEngine;
using System.Collections.Generic;

namespace StrawberryDataset
{
    /// <summary>
    /// Generates randomized greenhouse scenes with strawberry bushes.
    /// Handles bush placement, collision detection, greenhouse creation, and lighting.
    /// </summary>
    public class StrawberrySceneGenerator : MonoBehaviour
    {
        public StrawberryDatasetConfig config;
        
        [HideInInspector]
        public List<StrawberryBushInstance> spawnedBushes = new List<StrawberryBushInstance>();
        
        private GameObject greenhouseContainer;
        private Light sceneLight;
        
        /// <summary>
        /// Generate a complete random scene with bushes, greenhouse, and lighting
        /// </summary>
        public void GenerateScene()
        {
            ClearScene();
            
            if (config == null)
            {
                Debug.LogError("Config is null! Please assign StrawberryDatasetConfig.");
                return;
            }
            
            if (config.strawberryBushPrefab == null)
            {
                Debug.LogError("Strawberry bush prefab is not assigned in config!");
                return;
            }
            
            // Determine number of bushes
            int bushCount = Random.Range(config.minBushCount, config.maxBushCount + 1);
            
            // Generate bushes in a row
            GenerateBushRow(bushCount);
            
            // Create greenhouse around bushes
            GenerateGreenhouse();
            
            // Setup lighting
            SetupLighting();
            
            Debug.Log($"Scene generated: {bushCount} bushes");
        }
        
        /// <summary>
        /// Generate bushes in a row with proper spacing and constraints
        /// </summary>
        private void GenerateBushRow(int count)
        {
            List<BushPlacement> placements = new List<BushPlacement>();
            
            float currentX = 0f;
            
            for (int i = 0; i < count; i++)
            {
                // Random bush height (determines scale)
                float bushHeight = Random.Range(config.minBushHeight, config.maxBushHeight);
                
                // Calculate scale factor based on desired height
                float originalHeight = GetBushHeight(config.strawberryBushPrefab);
                float scaleFactor = bushHeight / originalHeight;
                
                // Random rotation around Y axis
                float yRotation = Random.Range(0f, 360f);
                
                // Random tilt (rotation around X and Z axes)
                float tiltX = Random.Range(-config.maxTiltAngle, config.maxTiltAngle);
                float tiltZ = Random.Range(-config.maxTiltAngle, config.maxTiltAngle);
                
                // Position along row
                Vector3 position = new Vector3(currentX, 0f, 0f);
                
                // Add random offsets
                position.x += Random.Range(-config.maxHorizontalOffset, config.maxHorizontalOffset);
                position.y += Random.Range(0f, config.maxVerticalOffset); // Only positive vertical offset
                position.z += Random.Range(-config.maxHorizontalOffset, config.maxHorizontalOffset);
                
                // Create placement data
                BushPlacement placement = new BushPlacement
                {
                    position = position,
                    rotation = Quaternion.Euler(tiltX, yRotation, tiltZ),
                    scale = Vector3.one * scaleFactor,
                    bushHeight = bushHeight
                };
                
                placements.Add(placement);
                
                // Calculate next X position with random gap
                float gap = Random.Range(config.minBushGap, config.maxBushGap);
                float bushWidth = GetBushWidth(config.strawberryBushPrefab) * scaleFactor;
                currentX += bushWidth + gap;
            }
            
            // Center the row around origin
            float totalWidth = currentX;
            float offset = -totalWidth / 2f;
            
            // Spawn bushes
            for (int i = 0; i < placements.Count; i++)
            {
                BushPlacement placement = placements[i];
                placement.position.x += offset;
                
                // Check collision with existing bushes
                bool hasCollision = false;
                int retryCount = 0;
                const int maxRetries = 5;
                
                while (hasCollision && retryCount < maxRetries)
                {
                    hasCollision = CheckCollision(placement, placements, i);
                    if (hasCollision)
                    {
                        // Adjust position slightly
                        placement.position.x += 0.02f;
                        retryCount++;
                    }
                }
                
                // Ensure bush is above ground
                if (placement.position.y < 0)
                {
                    placement.position.y = 0;
                }
                
                // Spawn the bush
                GameObject bushObj = Instantiate(config.strawberryBushPrefab, placement.position, placement.rotation);
                bushObj.transform.localScale = placement.scale;
                bushObj.name = $"StrawberryBush_{i:D2}";
                
                // Adjust materials
                AdjustBushMaterials(bushObj);
                
                // CRITICAL FIX: Align bush to floor EXACTLY
                Bounds bounds = new Bounds(bushObj.transform.position, Vector3.zero);
                Renderer[] renderers = bushObj.GetComponentsInChildren<Renderer>();
                
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                    
                    float liftAmount = 0.0f - bounds.min.y;
                    bushObj.transform.position += new Vector3(0, liftAmount, 0); 
                }
                
                // Add and initialize bush instance component
                var bushInstance = bushObj.AddComponent<StrawberryBushInstance>();
                bushInstance.Initialize(i);
                
                spawnedBushes.Add(bushInstance);
            }
        }
        
        /// <summary>
        /// Adjust bush materials to reduce excessive reflectivity
        /// </summary>
        private void AdjustBushMaterials(GameObject bushObj)
        {
            var renderers = bushObj.GetComponentsInChildren<Renderer>(true);
            
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null)
                {
                    // Create instance of material to avoid modifying the original asset
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            // Create material instance
                            Material matInstance = new Material(materials[i]);
                            
                            // Reduce smoothness/glossiness to make less reflective
                            if (matInstance.HasProperty("_Smoothness"))
                            {
                                float currentSmoothness = matInstance.GetFloat("_Smoothness");
                                matInstance.SetFloat("_Smoothness", Mathf.Min(currentSmoothness * 0.3f, 0.3f));
                            }
                            
                            if (matInstance.HasProperty("_Glossiness"))
                            {
                                float currentGlossiness = matInstance.GetFloat("_Glossiness");
                                matInstance.SetFloat("_Glossiness", Mathf.Min(currentGlossiness * 0.3f, 0.3f));
                            }
                            
                            // Adjust metallic as requested (0.98)
                            if (matInstance.HasProperty("_Metallic"))
                            {
                                matInstance.SetFloat("_Metallic", 0.98f);
                            }
                            
                            materials[i] = matInstance;
                        }
                    }
                    
                    renderer.sharedMaterials = materials;
                }
            }
        }
        
        /// <summary>
        /// Check if a bush placement collides with existing bushes
        /// </summary>
        private bool CheckCollision(BushPlacement placement, List<BushPlacement> existingPlacements, int currentIndex)
        {
            // Simple sphere-based collision check
            float radius = config.maxBushHeight / 2f; // Approximate radius
            
            for (int i = 0; i < currentIndex; i++)
            {
                float distance = Vector3.Distance(placement.position, existingPlacements[i].position);
                float combinedRadius = radius + (existingPlacements[i].bushHeight / 2f);
                
                if (distance < combinedRadius)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Generate greenhouse using user-provided snippet logic
        /// </summary>
        private void GenerateGreenhouse()
        {
            // Remove existing if present
            if (greenhouseContainer) DestroyImmediate(greenhouseContainer);
            greenhouseContainer = new GameObject("Greenhouse");
            
            // Calculate bounds from bushes
            Bounds bounds = CalculateSceneBounds();
            
            // Map dimensions to user snippet inputs:
            // The snippet creates a tunnel along Z (by default logic). 
            // My bushes are along X. So I map:
            // size.x = Length of row (X axis) + padding
            // size.y = Height
            // size.z = Width of greenhouse (Z axis) + padding
            
            float width = bounds.size.x + config.greenhousePadding * 2; // Along X
            float height = config.greenhouseHeight;
            float depth = bounds.size.z + config.greenhousePadding * 2; // Along Z
            
            // Ensure minimum dimensions
            if (width < 4.0f) width = 4.0f;
            if (depth < 4.0f) depth = 4.0f;
            
            Vector3 center = bounds.center;
            center.y = height / 2.0f;
            
            // Pass size as (X=Width, Y=Height, Z=Depth)
            CreateGreenhouseStructure(new Vector3(width, height, depth), center, "Walls_Outer");
        }

        private void CreateGreenhouseStructure(Vector3 size, Vector3 center, string name)
        {
            GameObject walls = new GameObject(name);
            walls.transform.SetParent(greenhouseContainer.transform);
            
            // Helper to create random material (COPIED FROM SNIPPET)
            Material randomMat() {
                Material m = new Material(Shader.Find("Unlit/Color"));
                // Fallback if URP/Unlit not found
                if (m.shader == null) m = new Material(Shader.Find("Diffuse")); 
                // Random HSV Color
                m.color = Random.ColorHSV(0f, 1f, 0.3f, 0.7f, 0.2f, 0.5f); 
                return m;
            }
            
            // Floor (Optional Visual)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(walls.transform);
            floor.transform.position = new Vector3(center.x, 0, center.z);
            // Plane is 10x10 units by default, so scale / 10
            floor.transform.localScale = new Vector3(size.x/10f, 1, size.z/10f); 
            floor.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(floor.GetComponent<Collider>());
            
            // Left Wall (-X direction relative to center)
            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.transform.parent = walls.transform;
            left.transform.position = center - Vector3.right * (size.x/2); 
            left.transform.localScale = new Vector3(0.1f, size.y, size.z);
            left.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(left.GetComponent<Collider>());
            
            // Right Wall (+X direction relative to center)
            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.transform.parent = walls.transform;
            right.transform.position = center + Vector3.right * (size.x/2);
            right.transform.localScale = new Vector3(0.1f, size.y, size.z);
            right.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(right.GetComponent<Collider>());
            
            // Front Wall (+Z direction relative to center)
            GameObject fwd = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fwd.transform.parent = walls.transform;
            fwd.transform.position = center + Vector3.forward * (size.z/2);
            fwd.transform.localScale = new Vector3(size.x, size.y, 0.1f);
            fwd.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(fwd.GetComponent<Collider>());
            
            // Back Wall (-Z direction relative to center)
            GameObject bwd = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bwd.transform.parent = walls.transform;
            bwd.transform.position = center - Vector3.forward * (size.z/2);
            bwd.transform.localScale = new Vector3(size.x, size.y, 0.1f);
            bwd.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(bwd.GetComponent<Collider>());
            
            // Roof (Top) - Added per request
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.parent = walls.transform;
            roof.transform.position = center + Vector3.up * (size.y/2);
            roof.transform.localScale = new Vector3(size.x, 0.1f, size.z);
            roof.GetComponent<Renderer>().material = randomMat();
            DestroyImmediate(roof.GetComponent<Collider>());
        }

        /// <summary>
        /// Setup random directional lighting
        /// </summary>
        private void SetupLighting()
        {
            // Find or create directional light
            sceneLight = FindFirstObjectByType<Light>();
            
            if (sceneLight == null || sceneLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("SceneLight");
                sceneLight = lightObj.AddComponent<Light>();
                sceneLight.type = LightType.Directional;
            }
            
            // Position light high above to avoid visual clutter in scene view
            sceneLight.transform.position = new Vector3(0f, 10f, 0f);
            
            // Random elevation and azimuth
            float elevation = Random.Range(config.minLightElevation, config.maxLightElevation);
            float azimuth = Random.Range(0f, 360f);
            
            sceneLight.transform.rotation = Quaternion.Euler(elevation, azimuth, 0);
            
            // Random intensity
            sceneLight.intensity = Random.Range(config.minLightIntensity, config.maxLightIntensity);
            
            // Warm white color
            sceneLight.color = new Color(1f, 0.98f, 0.95f);
        }
        
        /// <summary>
        /// Calculate combined bounds of all spawned bushes
        /// </summary>
        private Bounds CalculateSceneBounds()
        {
            if (spawnedBushes.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }
            
            Bounds bounds = spawnedBushes[0].GetBounds();
            
            for (int i = 1; i < spawnedBushes.Count; i++)
            {
                bounds.Encapsulate(spawnedBushes[i].GetBounds());
            }
            
            return bounds;
        }
        
        /// <summary>
        /// Get greenhouse bounds for camera positioning
        /// </summary>
        public Bounds GetGreenhouseBounds()
        {
            Bounds sceneBounds = CalculateSceneBounds();
            Vector3 min = sceneBounds.min - new Vector3(config.greenhousePadding, 0, config.greenhousePadding);
            Vector3 max = sceneBounds.max + new Vector3(config.greenhousePadding, config.greenhouseHeight, config.greenhousePadding);
            
            Vector3 center = (min + max) / 2f;
            Vector3 size = max - min;
            
            return new Bounds(center, size);
        }
        
        /// <summary>
        /// Clear all generated scene objects
        /// </summary>
        public void ClearScene()
        {
            // Destroy all spawned bushes
            foreach (var bush in spawnedBushes)
            {
                if (bush != null)
                {
                    DestroyImmediate(bush.gameObject);
                }
            }
            spawnedBushes.Clear();
            
            // Destroy greenhouse
            if (greenhouseContainer != null)
            {
                DestroyImmediate(greenhouseContainer);
            }
        }
        
        /// <summary>
        /// Get approximate height of bush prefab
        /// </summary>
        private float GetBushHeight(GameObject prefab)
        {
            // Try to get bounds from prefab
            // If prefab has renderers, calculate bounds
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }
                return bounds.size.y;
            }
            
            // Default fallback
            return 0.15f; // 15 cm
        }
        
        /// <summary>
        /// Get approximate width of bush prefab
        /// </summary>
        private float GetBushWidth(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }
                return Mathf.Max(bounds.size.x, bounds.size.z);
            }
            
            return 0.10f; // 10 cm
        }
        
        /// <summary>
        /// Helper class for bush placement data
        /// </summary>
        private class BushPlacement
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float bushHeight;
        }
    }
}
