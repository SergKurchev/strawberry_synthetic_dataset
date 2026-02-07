using UnityEngine;
using System.Collections.Generic;

namespace StrawberryDataset
{
    /// <summary>
    /// Component attached to each spawned strawberry bush instance.
    /// Tracks all strawberry and peduncle child objects with their metadata.
    /// </summary>
    public class StrawberryBushInstance : MonoBehaviour
    {
        [System.Serializable]
        public class StrawberryData
        {
            public GameObject gameObject;
            public int instanceId;
            public int peduncleId;
            public string ripenessState;
            public Renderer[] renderers;
        }
        
        [System.Serializable]
        public class PeduncleData
        {
            public GameObject gameObject;
            public int instanceId;
            public Renderer[] renderers;
        }
        
        public List<StrawberryData> strawberries = new List<StrawberryData>();
        public List<PeduncleData> peduncles = new List<PeduncleData>();
        
        private static int globalInstanceIdCounter = 1;
        
        /// <summary>
        /// Initialize the bush by finding and cataloging all strawberry and peduncle objects.
        /// Global indexing: BushIndex * 18 + SuffixID (0-17)
        /// </summary>
        public void Initialize(int bushIndex)
        {
            strawberries.Clear();
            peduncles.Clear();
            
            Transform worldTransform = transform.Find("world");
            if (worldTransform == null) return;
            
            Dictionary<int, int> peduncleGlobalIdMap = new Dictionary<int, int>();
            
            // 1. Process peduncles
            foreach (Transform child in worldTransform)
            {
                if (child.name.StartsWith("peduncle_"))
                {
                    int suffixId = ExtractSuffixId(child.name);
                    int localIdx = suffixId - 1;
                    int globalId = (bushIndex * 18) + localIdx + 1;
                    
                    peduncleGlobalIdMap[suffixId] = globalId;
                    
                    Renderer[] childRenderers = child.GetComponentsInChildren<Renderer>();
                    if (childRenderers.Length == 0) continue;

                    var peduncleData = new PeduncleData
                    {
                        gameObject = child.gameObject,
                        instanceId = globalId,
                        renderers = childRenderers
                    };
                    peduncles.Add(peduncleData);
                    
                    var segId = child.gameObject.GetComponent<StrawberrySegmentationId>() ?? child.gameObject.AddComponent<StrawberrySegmentationId>();
                    segId.instanceId = globalId;
                    segId.categoryId = 3;
                    segId.ripenessState = "peduncle";
                }
            }
            
            // 2. Process strawberries
            foreach (Transform child in worldTransform)
            {
                string childName = child.name.ToLower();
                if (childName.StartsWith("strawberry_"))
                {
                    int suffixId = ExtractSuffixId(child.name);
                    int localIdx = suffixId - 1;
                    int globalId = (bushIndex * 18) + localIdx + 1;
                    
                    Renderer[] childRenderers = child.GetComponentsInChildren<Renderer>();
                    if (childRenderers.Length == 0) continue;

                    string ripeness = "unripe";
                    if (childName.Contains("_half_ripe_") || childName.Contains("_halfripe_")) ripeness = "half_ripe";
                    else if (childName.Contains("_ripe_")) ripeness = "ripe";
                    
                    int pedId = peduncleGlobalIdMap.ContainsKey(suffixId) ? peduncleGlobalIdMap[suffixId] : -1;
                    
                    var strawberryData = new StrawberryData
                    {
                        gameObject = child.gameObject,
                        instanceId = globalId,
                        peduncleId = pedId,
                        ripenessState = ripeness,
                        renderers = childRenderers
                    };
                    strawberries.Add(strawberryData);
                    
                    var segId = child.gameObject.GetComponent<StrawberrySegmentationId>() ?? child.gameObject.AddComponent<StrawberrySegmentationId>();
                    segId.instanceId = globalId;
                    segId.categoryId = StrawberrySegmentationId.GetCategoryIdFromRipeness(ripeness);
                    segId.parentId = pedId;
                    segId.ripenessState = ripeness;
                }
            }
            Debug.Log($"Bush {bushIndex} initialized: {strawberries.Count} strawberries, {peduncles.Count} peduncles");
        }
        
        /// <summary>
        /// Extract numeric suffix from object name (e.g., "peduncle_014" -> 14)
        /// </summary>
        private int ExtractSuffixId(string name)
        {
            string[] parts = name.Split('_');
            if (parts.Length > 0)
            {
                string lastPart = parts[parts.Length - 1];
                if (int.TryParse(lastPart, out int id))
                {
                    return id;
                }
            }
            return 0;
        }
        
        /// <summary>
        /// Get combined bounds of all renderers in this bush
        /// </summary>
        public Bounds GetBounds()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool initialized = false;
            
            foreach (var strawberry in strawberries)
            {
                if (strawberry.renderers != null)
                {
                    foreach (var r in strawberry.renderers)
                    {
                        if (r == null) continue;
                        if (!initialized)
                        {
                            bounds = r.bounds;
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(r.bounds);
                        }
                    }
                }
            }
            
            foreach (var peduncle in peduncles)
            {
                if (peduncle.renderers != null)
                {
                    foreach (var r in peduncle.renderers)
                    {
                        if (r == null) continue;
                        if (!initialized)
                        {
                            bounds = r.bounds;
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(r.bounds);
                        }
                    }
                }
            }
            
            return bounds;
        }
        
        /// <summary>
        /// Reset global instance ID counter (call before generating new dataset)
        /// </summary>
        public static void ResetGlobalCounter()
        {
            globalInstanceIdCounter = 1;
        }
    }
}
