
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void Find()
    {
        int count = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    count++;
                    Debug.LogWarning($"MISSING SCRIPT on: '{go.name}' (path: {GetPath(go)})", go);
                }
            }
        }
        Debug.Log($"Found {count} missing script reference(s).");
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
#endif
