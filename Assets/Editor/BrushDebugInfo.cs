
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class BrushDebugInfo
{
    [MenuItem("Tools/Debug Brush Bounds")]
    public static void DebugBounds()
    {
        GameObject bristle = GameObject.Find("BristleCurve");
        if (bristle == null) { Debug.LogError("BristleCurve not found"); return; }
        
        MeshFilter mf = bristle.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) { Debug.LogError("No mesh"); return; }
        
        Mesh mesh = mf.sharedMesh;
        Bounds b = mesh.bounds;
        Debug.Log($"Bristle mesh bounds: center={b.center}, size={b.size}");
        Debug.Log($"  min={b.min}, max={b.max}");
        Debug.Log($"  Vertex count: {mesh.vertexCount}");
        
        // Sample some vertices to understand orientation
        Vector3[] verts = mesh.vertices;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].x < minX) minX = verts[i].x;
            if (verts[i].x > maxX) maxX = verts[i].x;
            if (verts[i].y < minY) minY = verts[i].y;
            if (verts[i].y > maxY) maxY = verts[i].y;
            if (verts[i].z < minZ) minZ = verts[i].z;
            if (verts[i].z > maxZ) maxZ = verts[i].z;
        }
        
        Debug.Log($"  X range: {minX:F4} to {maxX:F4} (span={maxX-minX:F4})");
        Debug.Log($"  Y range: {minY:F4} to {maxY:F4} (span={maxY-minY:F4})");
        Debug.Log($"  Z range: {minZ:F4} to {maxZ:F4} (span={maxZ-minZ:F4})");
        
        // The axis with the largest span is the bristle length axis
        float spanX = maxX - minX;
        float spanY = maxY - minY;
        float spanZ = maxZ - minZ;
        
        if (spanY > spanX && spanY > spanZ)
            Debug.Log("  -> Bristle length axis is Y (up/down in Unity)");
        else if (spanZ > spanX && spanZ > spanY)
            Debug.Log("  -> Bristle length axis is Z (forward/back in Unity)");
        else
            Debug.Log("  -> Bristle length axis is X");
            
        // World space tip position
        Transform t = bristle.transform;
        Debug.Log($"  BristleCurve world position: {t.position}");
        Debug.Log($"  BristleCurve parent: {t.parent?.name}");
        
        // Root transform
        Transform root = t.parent;
        if (root != null)
            Debug.Log($"  Root position: {root.position}");
    }
}
#endif
