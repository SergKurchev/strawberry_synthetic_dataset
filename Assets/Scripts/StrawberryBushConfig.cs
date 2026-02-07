using UnityEngine;

namespace StrawberryBushTool
{
    /// <summary>
    /// Configuration for the Strawberry Bush Generator tool.
    /// Stores paths to the FBX model, material, and texture assets.
    /// </summary>
    [CreateAssetMenu(fileName = "StrawberryBushConfig", menuName = "Tools/Strawberry Bush Config")]
    public class StrawberryBushConfig : ScriptableObject
    {
        [Header("Asset Paths")]
        [Tooltip("Path to the FBX model containing the strawberry bush")]
        public string fbxModelPath = "Assets/generated_scene_edited_final.fbx";
        
        [Tooltip("Path to the material for the strawberry bush")]
        public string materialPath = "Assets/Materials/Material_0.002 1.mat";
        
        [Tooltip("Path to the base texture/map for the material")]
        public string texturePath = "Assets/Materials/Image_0.002.png";
        
        [Header("Prefab Settings")]
        [Tooltip("Name of the template prefab to create")]
        public string templatePrefabName = "StrawberryBushTemplate";
        
        [Header("Objects to Exclude")]
        [Tooltip("Names of objects to exclude from the FBX hierarchy (e.g., Camera, Light)")]
        public string[] excludedObjectNames = new string[] { "Camera", "Light" };
    }
}
