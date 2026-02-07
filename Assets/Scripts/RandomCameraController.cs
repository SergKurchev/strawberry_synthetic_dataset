using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Handles random camera positioning within greenhouse bounds.
    /// Ensures camera stays inside greenhouse and avoids collisions with objects.
    /// </summary>
    public class RandomCameraController : MonoBehaviour
    {
        public StrawberryDatasetConfig config;
        public StrawberrySceneGenerator sceneGenerator;
        public Camera targetCamera;
        
        /// <summary>
        /// Position camera at a random valid location inside the greenhouse
        /// </summary>
        public bool PositionRandomly()
        {
            if (config == null || sceneGenerator == null || targetCamera == null)
            {
                Debug.LogError("RandomCameraController: Missing required references");
                return false;
            }
            
            Bounds greenhouseBounds = sceneGenerator.GetGreenhouseBounds();
            
            const int maxAttempts = 30;
            bool useBushFocus = Random.value < config.bushFocusedShotRatio;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 cameraPos;
                Vector3 lookAtPoint;
                
                if (useBushFocus && sceneGenerator.spawnedBushes.Count > 0)
                {
                    // Bush-focused mode: position near a specific bush
                    var targetBush = sceneGenerator.spawnedBushes[Random.Range(0, sceneGenerator.spawnedBushes.Count)];
                    Bounds bushBounds = targetBush.GetBounds();
                    Vector3 bushCenter = bushBounds.center;
                    
                    // Random position at specified distance from bush
                    float distance = Random.Range(config.minCameraToBushDistance, config.maxCameraToBushDistance);
                    Vector3 randomDir = Random.onUnitSphere;
                    
                    // CRITICAL: Prefer horizontal and slightly downward angles (camera looks AT strawberries)
                    randomDir.y = Mathf.Clamp(randomDir.y, -0.35f, 0.15f); // Horizontal bias
                    randomDir = randomDir.normalized;
                    
                    cameraPos = bushCenter + randomDir * distance;
                    
                    // CRITICAL: Ensure minimum height above floor
                    const float minCameraHeight = 0.20f; // 20cm minimum
                    cameraPos.y = Mathf.Max(cameraPos.y, minCameraHeight);
                    
                    // CRITICAL: Limit maximum height to prevent seeing through roof
                    float maxCameraHeight = bushCenter.y + config.maxCameraHeightAboveBush;
                    cameraPos.y = Mathf.Min(cameraPos.y, maxCameraHeight);
                    
                    // Look directly at bush center (where strawberries are)
                    lookAtPoint = bushCenter;
                }
                else
                {
                    // Wide-angle mode: random position in greenhouse
                    Vector3 sceneCenter = greenhouseBounds.center;
                    
                    // Limit max height to prevent seeing through roof
                    float maxHeight = Mathf.Min(
                        greenhouseBounds.max.y - 0.5f,  // 0.5m from ceiling
                        sceneCenter.y + config.maxCameraHeightAboveBush  // or configured limit
                    );
                    
                    cameraPos = new Vector3(
                        Random.Range(greenhouseBounds.min.x + 0.2f, greenhouseBounds.max.x - 0.2f),
                        Random.Range(0.3f, maxHeight),
                        Random.Range(greenhouseBounds.min.z + 0.2f, greenhouseBounds.max.z - 0.2f)
                    );
                    
                    // Look at scene center (where bushes are)
                    lookAtPoint = sceneCenter;
                }
                
                // Validate position is inside greenhouse
                if (!greenhouseBounds.Contains(cameraPos))
                {
                    continue;
                }
                
                // Check min distance to all bushes (prevent penetration)
                bool tooClose = false;
                foreach (var bush in sceneGenerator.spawnedBushes)
                {
                    Bounds bushBounds = bush.GetBounds();
                    float distanceToBush = Vector3.Distance(cameraPos, bushBounds.center);
                    
                    // Use larger of: configured distance OR bush radius + safety margin
                    float bushRadius = Mathf.Max(bushBounds.extents.x, bushBounds.extents.z);
                    float requiredDistance = Mathf.Max(config.minCameraObjectDistance, bushRadius + 0.1f);
                    
                    if (distanceToBush < requiredDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (tooClose)
                {
                    continue;
                }
                
                // Apply position and rotation
                targetCamera.transform.position = cameraPos;
                targetCamera.transform.LookAt(lookAtPoint);
                
                // Random roll
                float roll = Random.Range(-5f, 5f);
                targetCamera.transform.Rotate(Vector3.forward, roll, Space.Self);
                
                // Set FOV
                targetCamera.fieldOfView = config.cameraFOV;
                
                // CRITICAL: Check if near plane intersects floor
                if (!IsNearPlaneAboveFloor())
                {
                    continue;
                }
                
                // Validate frustum is inside greenhouse if enabled
                if (config.preventGreenhouseExit && !IsFrustumInsideGreenhouse(greenhouseBounds))
                {
                    continue;
                }
                
                // CRITICAL: Prevent camera from looking too far upward (sees sky through roof)
                // Unity eulerAngles.x: 0=horizon, positive=looking down, 270-360=looking up
                float pitch = targetCamera.transform.eulerAngles.x;
                // Convert to -180 to +180 range: negative = up, positive = down
                if (pitch > 180f) pitch -= 360f;
                
                // Use configurable limits
                float maxUpwardAngle = -config.maxUpwardPitchAngle;  // Negate because looking up = negative pitch
                float maxDownwardAngle = config.maxDownwardPitchAngle;
                
                if (pitch < maxUpwardAngle || pitch > maxDownwardAngle)
                {
                    continue;
                }
                
                return true;
            }
            
            Debug.LogWarning($"Failed to find valid camera position after {maxAttempts} attempts. " +
                           $"Mode: {(useBushFocus ? "Bush-Focused" : "Wide-Angle")}, " +
                           $"preventGreenhouseExit: {config.preventGreenhouseExit}");
            return false;
        }
        
        /// <summary>
        /// Check if camera frustum is entirely inside greenhouse bounds
        /// </summary>
        private bool IsFrustumInsideGreenhouse(Bounds greenhouseBounds)
        {
            // Sample frustum corners at near and far planes
            float nearDist = targetCamera.nearClipPlane;
            float farDist = Mathf.Min(targetCamera.farClipPlane, 5f); // Check up to 5m
            
            // Add padding to bounds to be less strict
            Bounds paddedBounds = new Bounds(greenhouseBounds.center, greenhouseBounds.size * 0.95f);
            
            Vector3[] corners = new Vector3[8];
            
            // Get frustum corners at near plane (viewport coords: 0-1 range)
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 0f : 1f;
                float y = (i < 2) ? 0f : 1f;
                Vector3 viewportPoint = new Vector3(x, y, nearDist);
                corners[i] = targetCamera.ViewportToWorldPoint(viewportPoint);
            }
            
            // Get frustum corners at far plane
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 0f : 1f;
                float y = (i < 2) ? 0f : 1f;
                Vector3 viewportPoint = new Vector3(x, y, farDist);
                corners[i + 4] = targetCamera.ViewportToWorldPoint(viewportPoint);
            }
            
            // Check if most corners are inside (allow some to be outside)
            int insideCount = 0;
            foreach (Vector3 corner in corners)
            {
                if (paddedBounds.Contains(corner))
                {
                    insideCount++;
                }
            }
            
            // Require at least 6 out of 8 corners inside (75%)
            return insideCount >= 6;
        }
        
        /// <summary>
        /// Check if camera near plane stays above floor (prevents seeing infinity)
        /// </summary>
        private bool IsNearPlaneAboveFloor()
        {
            float nearDist = targetCamera.nearClipPlane;
            const float minFloorClearance = 0.02f; // 2cm safety margin
            
            // Check only 4 near plane corners (far plane can dip below - not visible)
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 0f : 1f;
                float y = (i < 2) ? 0f : 1f;
                
                Vector3 nearCorner = targetCamera.ViewportToWorldPoint(new Vector3(x, y, nearDist));
                if (nearCorner.y < minFloorClearance)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}
