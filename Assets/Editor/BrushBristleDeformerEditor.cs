
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor preview for bristle deformation.
/// Adds a slider in the Inspector to preview bend/splay/press without entering Play mode.
/// </summary>
[CustomEditor(typeof(BrushBristleDeformer))]
public class BrushBristleDeformerEditor : Editor
{
    private float _previewBend = 0f;
    private float _previewSplay = 0f;
    private float _previewPress = 0f;
    private Vector3 _previewBendDir = Vector3.right;
    private bool _showPreview = false;
    
    private static readonly int _BendAmount = Shader.PropertyToID("_BendAmount");
    private static readonly int _BendDirection = Shader.PropertyToID("_BendDirection");
    private static readonly int _SplayAmount = Shader.PropertyToID("_SplayAmount");
    private static readonly int _PressAmount = Shader.PropertyToID("_PressAmount");
    private static readonly int _BristleHeight = Shader.PropertyToID("_BristleHeight");
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
        
        _showPreview = EditorGUILayout.Toggle("Enable Preview", _showPreview);
        
        if (_showPreview)
        {
            EditorGUI.BeginChangeCheck();
            
            _previewBend = EditorGUILayout.Slider("Preview Bend", _previewBend, 0f, 1f);
            _previewSplay = EditorGUILayout.Slider("Preview Splay", _previewSplay, 0f, 1f);
            _previewPress = EditorGUILayout.Slider("Preview Press", _previewPress, 0f, 1f);
            _previewBendDir = EditorGUILayout.Vector3Field("Bend Direction", _previewBendDir);
            
            if (EditorGUI.EndChangeCheck() || _showPreview)
            {
                BrushBristleDeformer deformer = (BrushBristleDeformer)target;
                if (deformer.bristleRenderer != null)
                {
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    deformer.bristleRenderer.GetPropertyBlock(block);
                    
                    block.SetFloat(_BendAmount, _previewBend * deformer.maxBendAngle);
                    block.SetVector(_BendDirection, _previewBendDir.normalized);
                    block.SetFloat(_SplayAmount, _previewSplay * deformer.maxSplay);
                    block.SetFloat(_PressAmount, _previewPress);
                    block.SetFloat(_BristleHeight, 0.48f);
                    
                    deformer.bristleRenderer.SetPropertyBlock(block);
                    SceneView.RepaintAll();
                }
            }
            
            if (GUILayout.Button("Reset Preview"))
            {
                _previewBend = 0f;
                _previewSplay = 0f;
                _previewPress = 0f;
                
                BrushBristleDeformer deformer = (BrushBristleDeformer)target;
                if (deformer.bristleRenderer != null)
                {
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    deformer.bristleRenderer.SetPropertyBlock(block);
                    SceneView.RepaintAll();
                }
            }
        }
    }
}
#endif
