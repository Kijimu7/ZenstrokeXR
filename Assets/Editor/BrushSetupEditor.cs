
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to set up the bristle deformation system on the brush.
/// Run from menu: Tools > Setup Brush Deformation
/// </summary>
public class BrushSetupEditor
{
    [MenuItem("Tools/Setup Brush Deformation")]
    public static void SetupBrushDeformation()
    {
        // Find the brush root
        GameObject brushRoot = GameObject.Find("JapaneseCalligraphyBrush");
        if (brushRoot == null)
        {
            Debug.LogError("Could not find JapaneseCalligraphyBrush in scene!");
            return;
        }

        // Find the bristle child
        Transform bristleChild = brushRoot.transform.Find("BristleCurve");
        if (bristleChild == null)
        {
            Debug.LogError("Could not find BristleCurve child!");
            return;
        }

        // Create bristle deform material
        Shader bristleShader = Shader.Find("Custom/BristleDeform");
        Material bristleMat;
        
        if (bristleShader != null)
        {
            bristleMat = new Material(bristleShader);
            bristleMat.name = "BristleDeform";
            
            // Set default properties
            bristleMat.SetFloat("_Roughness", 0.6f);
            bristleMat.SetFloat("_BristleStiffness", 2.0f);
            bristleMat.SetFloat("_BristleTipSoftness", 0.3f);
            bristleMat.SetFloat("_BristleHeight", 0.48f);
            bristleMat.SetColor("_Color", new Color(0.15f, 0.08f, 0.03f, 1f));
            
            // Assign existing bristle texture if available
            string[] texGuids = AssetDatabase.FindAssets("BristleHair_Diffuse t:Texture2D");
            if (texGuids.Length > 0)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuids[0]);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    bristleMat.SetTexture("_MainTex", tex);
                    Debug.Log($"Assigned bristle texture: {texPath}");
                }
            }
            
            // Save material asset
            string matDir = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(matDir))
                AssetDatabase.CreateFolder("Assets", "Materials");
            
            AssetDatabase.CreateAsset(bristleMat, matDir + "/BristleDeform.mat");
            Debug.Log("Created BristleDeform material");
        }
        else
        {
            Debug.LogWarning("Custom/BristleDeform shader not found. Using URP/Lit as fallback.");
            bristleMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        // Assign material to bristle renderer
        MeshRenderer bristleRenderer = bristleChild.GetComponent<MeshRenderer>();
        if (bristleRenderer != null)
        {
            bristleRenderer.sharedMaterial = bristleMat;
            Debug.Log("Assigned deform material to bristle renderer");
        }

        // Add BrushBristleDeformer component
        BrushBristleDeformer deformer = brushRoot.GetComponent<BrushBristleDeformer>();
        if (deformer == null)
            deformer = brushRoot.AddComponent<BrushBristleDeformer>();
        
        deformer.bristleRenderer = bristleRenderer;
        deformer.brushTipDirection = Vector3.forward; // Z-up from Blender
        deformer.tipOffsetDistance = 1.25f;
        deformer.maxBendAngle = 45f;
        deformer.deformSpeed = 12f;
        deformer.recoverySpeed = 8f;
        deformer.maxSplay = 0.4f;
        deformer.contactDistance = 0.15f;
        
        // Add MXInkBrushController
        // Add MXInkBrushController
        MXInkBrushController mxController = brushRoot.GetComponent<MXInkBrushController>();
        if (mxController == null)
            mxController = brushRoot.AddComponent<MXInkBrushController>();

        mxController.bristleDeformer = deformer;
        
        EditorUtility.SetDirty(brushRoot);
        AssetDatabase.SaveAssets();
        
        Debug.Log("=== Brush Deformation Setup Complete! ===");
        Debug.Log("Components added: BrushBristleDeformer, VRBrushController");
        Debug.Log("Material: BristleDeform (Custom/BristleDeform shader)");
        Debug.Log("To test: Add a surface with a Collider, enter Play mode, right-click to position, left-click to press.");
    }
}
#endif
