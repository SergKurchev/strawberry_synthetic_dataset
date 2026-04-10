using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Builds and manages the white-room scene used by the multi-view sample pipeline.
    /// Room layout: black floor + white walls (left, right, front, back) + white ceiling.
    /// Room is a cube with configurable side length.
    /// A single strawberry bush is placed at the world origin, aligned to the floor.
    ///
    /// NOTE: Walls and ceiling are thin Cubes (not Planes) so they are visible
    /// from the inside regardless of normal direction (Cubes render both faces
    /// with Unlit/Color in URP).
    /// </summary>
    public class WhiteRoomSceneSetup : MonoBehaviour
    {
        // ── Cached references ──────────────────────────────────────────────────
        private GameObject roomContainer;
        private GameObject bushObject;
        private StrawberryBushInstance bushInstance;
        private Light sceneLight;

        // ── Materials (created once, reused) ──────────────────────────────────
        private Material blackMat;
        private Material whiteMat;

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Create the white room, spawn the bush, set up lighting.
        /// Call this once before starting frame capture for a sample.
        /// </summary>
        public void InitializeScene(MultiViewSampleConfig cfg)
        {
            ClearScene();
            CreateMaterials();
            BuildRoom(cfg.roomSideLength);
            SpawnBush(cfg.strawberryBushPrefab, cfg.bushScale);
            SetupLighting();
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Destroy all generated scene objects and materials.
        /// </summary>
        public void ClearScene()
        {
            if (roomContainer != null) DestroyImmediate(roomContainer);
            if (bushObject    != null) DestroyImmediate(bushObject);
            if (sceneLight    != null) DestroyImmediate(sceneLight.gameObject);
            if (blackMat      != null) DestroyImmediate(blackMat);
            if (whiteMat      != null) DestroyImmediate(whiteMat);

            roomContainer = null;
            bushObject    = null;
            bushInstance  = null;
            sceneLight    = null;
            blackMat      = null;
            whiteMat      = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>Returns the StrawberryBushInstance for the spawned bush.</summary>
        public StrawberryBushInstance GetBushInstance() => bushInstance;

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the world-space Bounds of the spawned bush (union of all renderers).
        /// </summary>
        public Bounds GetBushWorldBounds()
        {
            if (bushInstance == null)
                return new Bounds(Vector3.zero, Vector3.one * 0.15f);

            return bushInstance.GetBounds();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Private helpers
        // ═════════════════════════════════════════════════════════════════════

        private void CreateMaterials()
        {
            // Unlit/Color — works in both Built-in and URP
            Shader unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = Shader.Find("Diffuse");

            blackMat = new Material(unlitShader) { color = Color.black };
            whiteMat = new Material(unlitShader) { color = Color.white };
        }

        private void BuildRoom(float side)
        {
            roomContainer = new GameObject("WhiteRoom");

            float s = side;           // all sides equal (cube room)
            float t = s * 0.01f;      // panel thickness (1% of room size)

            // ── Floor — Black, flat on y = 0 ───────────────────────────────
            // A thin Cube sitting just below y=0 so its top face is at y=0
            CreateCubePanel("Floor",
                pos:   new Vector3(0f, -t * 0.5f, 0f),
                scale: new Vector3(s, t, s),
                mat:   blackMat);

            // ── Ceiling — White, at y = s ───────────────────────────────────
            CreateCubePanel("Ceiling",
                pos:   new Vector3(0f, s + t * 0.5f, 0f),
                scale: new Vector3(s, t, s),
                mat:   whiteMat);

            // ── Left wall  (−X face) ────────────────────────────────────────
            CreateCubePanel("WallLeft",
                pos:   new Vector3(-s * 0.5f - t * 0.5f, s * 0.5f, 0f),
                scale: new Vector3(t, s, s),
                mat:   whiteMat);

            // ── Right wall (+X face) ────────────────────────────────────────
            CreateCubePanel("WallRight",
                pos:   new Vector3(s * 0.5f + t * 0.5f, s * 0.5f, 0f),
                scale: new Vector3(t, s, s),
                mat:   whiteMat);

            // ── Front wall (+Z face) ────────────────────────────────────────
            CreateCubePanel("WallFront",
                pos:   new Vector3(0f, s * 0.5f, s * 0.5f + t * 0.5f),
                scale: new Vector3(s, s, t),
                mat:   whiteMat);

            // ── Back wall  (−Z face) ────────────────────────────────────────
            CreateCubePanel("WallBack",
                pos:   new Vector3(0f, s * 0.5f, -s * 0.5f - t * 0.5f),
                scale: new Vector3(s, s, t),
                mat:   whiteMat);
        }

        private void CreateCubePanel(string objName, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = objName;
            obj.transform.SetParent(roomContainer.transform, worldPositionStays: false);
            obj.transform.position   = pos;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().material = mat;

            // Remove collider — no physics needed
            var col = obj.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }

        private void SpawnBush(GameObject prefab, float scale)
        {
            if (prefab == null)
            {
                Debug.LogError("[WhiteRoomSceneSetup] Strawberry bush prefab is null!");
                return;
            }

            bushObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            bushObject.name = "StrawberryBush_00";

            // Apply user-defined scale BEFORE bounds calculation
            bushObject.transform.localScale = Vector3.one * scale;

            // Lift bush so its lowest point sits exactly on y = 0
            AlignBushToFloor();

            // Register the bush instance component
            bushInstance = bushObject.AddComponent<StrawberryBushInstance>();
            bushInstance.Initialize(0);
        }

        private void AlignBushToFloor()
        {
            Renderer[] renderers = bushObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds combined = renderers[0].bounds;
            foreach (var r in renderers) combined.Encapsulate(r.bounds);

            float lift = 0f - combined.min.y;
            bushObject.transform.position += new Vector3(0f, lift, 0f);
        }

        private void SetupLighting()
        {
            // Look for an existing directional light first; if none, create one
            Light existing = FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional)
            {
                sceneLight = existing;
            }
            else
            {
                GameObject lightObj = new GameObject("MultiViewLight");
                sceneLight = lightObj.AddComponent<Light>();
                sceneLight.type = LightType.Directional;
            }

            sceneLight.transform.position = new Vector3(0f, 10f, 0f);
            sceneLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sceneLight.intensity          = 1.0f;
            sceneLight.color              = new Color(1f, 0.98f, 0.95f);
        }
    }
}
