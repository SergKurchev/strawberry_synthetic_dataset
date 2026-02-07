using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Component attached to each strawberry or peduncle object for tracking segmentation metadata.
    /// Used during annotation generation to identify objects and their relationships.
    /// </summary>
    public class StrawberrySegmentationId : MonoBehaviour
    {
        [Tooltip("Unique instance ID for this object")]
        public int instanceId;
        
        [Tooltip("Category ID: 0=ripe, 1=unripe, 2=half_ripe, 3=peduncle")]
        public int categoryId;
        
        [Tooltip("Parent instance ID (for strawberries: peduncle ID; for peduncles: 0)")]
        public int parentId;
        
        [Tooltip("Ripeness state: ripe, unripe, half_ripe")]
        public string ripenessState;
        
        /// <summary>
        /// Get category name from category ID
        /// </summary>
        public string GetCategoryName()
        {
            switch (categoryId)
            {
                case 0: return "strawberry_ripe";
                case 1: return "strawberry_unripe";
                case 2: return "strawberry_half_ripe";
                case 3: return "peduncle";
                default: return "unknown";
            }
        }
        
        /// <summary>
        /// Determine category ID from ripeness state
        /// </summary>
        public static int GetCategoryIdFromRipeness(string ripeness)
        {
            switch (ripeness.ToLower())
            {
                case "ripe": return 0;
                case "unripe": return 1;
                case "half_ripe": return 2;
                default: return 1; // Default to unripe
            }
        }
        
        /// <summary>
        /// Generate unique color for segmentation mask based on instance and category IDs
        /// </summary>
        public Color GetSegmentationColor()
        {
            // CRITICAL: Each object type uses ONLY its designated channel
            // Strawberries (cat 0,1,2): R = instanceId, G = 0
            // Peduncles (cat 3): R = 0, G = instanceId
            // All: B = categoryId
            
            int r = 0, g = 0, b = categoryId;
            
            if (categoryId == 3) // Peduncle
            {
                g = instanceId;
            }
            else // Strawberry
            {
                r = instanceId;
            }
            
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
