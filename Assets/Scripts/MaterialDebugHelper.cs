using UnityEngine;

namespace StrawberryDataset
{
    /// <summary>
    /// Debug helper to inspect and fix materials on strawberry bushes.
    /// Attach to any GameObject and use context menu.
    /// </summary>
    public class MaterialDebugHelper : MonoBehaviour
    {
        [ContextMenu("Inspect All Materials")]
        public void InspectAllMaterials()
        {
            Debug.Log("=== Material Inspection ===");
            
            var bushes = FindObjectsOfType<StrawberryBushInstance>();
            Debug.Log($"Found {bushes.Length} bushes");
            
            foreach (var bush in bushes)
            {
                Debug.Log($"\n--- Bush: {bush.name} ---");
                
                foreach (var strawberry in bush.strawberries)
                {
                    if (strawberry.renderers != null)
                    {
                        foreach (var r in strawberry.renderers)
                        {
                            if (r == null) continue;
                            Material mat = r.sharedMaterial;
                            if (mat != null)
                            {
                                Debug.Log($"Strawberry {strawberry.gameObject.name} (Renderer {r.name}):");
                                Debug.Log($"  Material: {mat.name}");
                                Debug.Log($"  Shader: {mat.shader.name}");
                                Debug.Log($"  Color: {mat.color}");
                                
                                if (mat.HasProperty("_BaseColor"))
                                    Debug.Log($"  BaseColor: {mat.GetColor("_BaseColor")}");
                                if (mat.HasProperty("_MainTex"))
                                    Debug.Log($"  MainTex: {mat.GetTexture("_MainTex")}");
                                if (mat.HasProperty("_BaseMap"))
                                    Debug.Log($"  BaseMap: {mat.GetTexture("_BaseMap")}");
                            }
                        }
                    }
                }
            }
            
            // Check greenhouse
            var greenhouse = GameObject.Find("Greenhouse");
            if (greenhouse != null)
            {
                Debug.Log("\n--- Greenhouse ---");
                var renderers = greenhouse.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    Material mat = renderer.sharedMaterial;
                    if (mat != null)
                    {
                        Debug.Log($"{renderer.gameObject.name}:");
                        Debug.Log($"  Material: {mat.name}");
                        Debug.Log($"  Shader: {mat.shader.name}");
                        Debug.Log($"  Color: {mat.color}");
                    }
                }
            }
        }
        
        [ContextMenu("Fix Greenhouse Materials")]
        public void FixGreenhouseMaterials()
        {
            var greenhouse = GameObject.Find("Greenhouse");
            if (greenhouse == null)
            {
                Debug.LogWarning("No greenhouse found!");
                return;
            }
            
            Debug.Log("Fixing greenhouse materials...");
            
            var renderers = greenhouse.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                Material mat = renderer.sharedMaterial;
                if (mat != null && mat.shader != null)
                {
                    // Check if shader is broken (magenta)
                    if (mat.shader.name.Contains("Hidden") || !mat.shader.isSupported)
                    {
                        Debug.Log($"Fixing {renderer.gameObject.name} - shader was {mat.shader.name}");
                        
                        // Try to find working shader
                        Shader newShader = Shader.Find("Standard");
                        if (newShader == null)
                            newShader = Shader.Find("Diffuse");
                        
                        if (newShader != null)
                        {
                            Color oldColor = mat.color;
                            Material newMat = new Material(newShader);
                            newMat.color = oldColor;
                            renderer.sharedMaterial = newMat;
                            Debug.Log($"  Fixed with {newShader.name}");
                        }
                    }
                }
            }
            
            Debug.Log("Greenhouse materials fixed!");
        }
        
        [ContextMenu("List All Available Shaders")]
        public void ListAvailableShaders()
        {
            Debug.Log("=== Available Shaders ===");
            
            string[] shaderNames = new string[]
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Standard",
                "Standard (Specular setup)",
                "Diffuse",
                "Unlit/Color",
                "Unlit/Texture"
            };
            
            foreach (var shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    Debug.Log($"✓ {shaderName} - Supported: {shader.isSupported}");
                }
                else
                {
                    Debug.Log($"✗ {shaderName} - NOT FOUND");
                }
            }
        }
    }
}
