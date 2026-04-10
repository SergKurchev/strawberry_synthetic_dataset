using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Positions the capture camera for the multi-view sample pipeline.
    /// Each call to PositionForFrame() places the camera at a random point
    /// that is inside the 1m³ room, at a configurable distance from the bush,
    /// and oriented toward a random point inside the (shrunk) bush bounding box.
    /// </summary>
    public class MultiViewCameraController : MonoBehaviour
    {
        public Camera targetCamera;
        public WhiteRoomSceneSetup sceneSetup;
        public MultiViewSampleConfig config;

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Attempt to place the camera for one frame.
        /// Returns false if a valid position could not be found after maxAttempts.
        /// </summary>
        public bool PositionForFrame()
        {
            if (targetCamera == null || sceneSetup == null || config == null)
            {
                Debug.LogError("[MultiViewCameraController] Missing required references.");
                return false;
            }

            Bounds bushBounds = sceneSetup.GetBushWorldBounds();
            Vector3 bushCenter = bushBounds.center;

            // Shrunk bounds for look-at target selection
            Bounds shrunkBounds = ShrinkBounds(bushBounds, config.bboxShrinkFactor);

            // Room half-extents (camera must stay inside)
            float half = config.roomSideLength * 0.5f;

            const int maxAttempts = 50;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // ── 1. Random look-at target inside shrunk bush BB ──────────
                Vector3 lookTarget = new Vector3(
                    Random.Range(shrunkBounds.min.x, shrunkBounds.max.x),
                    Random.Range(shrunkBounds.min.y, shrunkBounds.max.y),
                    Random.Range(shrunkBounds.min.z, shrunkBounds.max.z)
                );

                // ── 2. Random camera position on a sphere around bush ───────
                float distance = Random.Range(config.minCameraDistance, config.maxCameraDistance);
                Vector3 direction = Random.onUnitSphere;

                // Downward bias: push Y component into the -0.5 … +0.3 range so the
                // camera prefers side/low angles rather than top-down.
                // This prevents ceiling-proximity artifacts and unusual top-down shots.
                direction.y = Mathf.Clamp(direction.y, -0.6f, 0.3f);
                if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                direction.Normalize();

                Vector3 cameraPos = bushCenter + direction * distance;

                // ── 3. Height clamp: stay between 5 cm and 70% of room height ──
                // 70% cap prevents the camera from reaching the ceiling and avoids
                // top-down views that produce near-clip artifacts on leaf geometry.
                float maxCameraY = config.roomSideLength * 0.70f;
                cameraPos.y = Mathf.Clamp(cameraPos.y, 0.05f, maxCameraY);

                // ── 4. Room bounds clamp: keep inside ±half on X and Z ──────
                cameraPos.x = Mathf.Clamp(cameraPos.x, -half + 0.05f, half - 0.05f);
                cameraPos.z = Mathf.Clamp(cameraPos.z, -half + 0.05f, half - 0.05f);

                // ── 5. Apply transform ───────────────────────────────────────
                targetCamera.transform.position = cameraPos;
                targetCamera.transform.LookAt(lookTarget);
                targetCamera.fieldOfView = config.cameraFOV;

                // ── 6. Validate: near-plane must not be underground ──────────
                if (!NearPlaneAboveFloor())
                    continue;

                return true;
            }

            Debug.LogWarning($"[MultiViewCameraController] Could not find valid camera position after {maxAttempts} attempts.");
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Extract camera intrinsics from current camera state.
        /// </summary>
        public CameraIntrinsicsData GetIntrinsics()
        {
            int w = config.imageWidth;
            int h = config.imageHeight;
            float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float fy = (h / 2f) / Mathf.Tan(fovRad / 2f);
            float fx = fy; // square pixels

            return new CameraIntrinsicsData
            {
                fx = fx,
                fy = fy,
                cx = w / 2f,
                cy = h / 2f
            };
        }

        /// <summary>
        /// Extract current camera extrinsics (world-space position + quaternion rotation).
        /// </summary>
        public CameraExtrinsicsData GetExtrinsics()
        {
            Vector3 pos = targetCamera.transform.position;
            Quaternion rot = targetCamera.transform.rotation;

            return new CameraExtrinsicsData
            {
                position = new float[] { pos.x, pos.y, pos.z },
                rotation = new float[] { rot.x, rot.y, rot.z, rot.w }
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Private helpers
        // ═════════════════════════════════════════════════════════════════════

        private Bounds ShrinkBounds(Bounds b, float factor)
        {
            Vector3 shrink = b.size * factor;
            return new Bounds(b.center, b.size - shrink * 2f);
        }

        private bool NearPlaneAboveFloor()
        {
            float nd = targetCamera.nearClipPlane;
            const float minClearance = 0.01f;

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 0f : 1f;
                float y = (i < 2) ? 0f : 1f;
                Vector3 corner = targetCamera.ViewportToWorldPoint(new Vector3(x, y, nd));
                if (corner.y < minClearance) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Data structs (serializable for JSON)
        // ─────────────────────────────────────────────────────────────────────

        [System.Serializable]
        public class CameraIntrinsicsData
        {
            public float fx;
            public float fy;
            public float cx;
            public float cy;
        }

        [System.Serializable]
        public class CameraExtrinsicsData
        {
            public float[] position = new float[3];
            public float[] rotation = new float[4]; // quaternion x,y,z,w
        }

        [System.Serializable]
        public class FrameCameraData
        {
            public int frame_index;
            public CameraIntrinsicsData intrinsics;
            public CameraExtrinsicsData extrinsics;
        }
    }
}
