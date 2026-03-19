using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;

public class KanjiVGStrokePlayer : MonoBehaviour
{
    [Header("SVG Source")]
    [HideInInspector] public string svgFileName = "";
    [Tooltip("Subfolder inside StreamingAssets")]
    public string svgFolder = "KanjiVG";

    [Header("Rendering")]
    public Material lineMaterial;
    public float lineWidth = 0.06f;
    public float unitsPerSvgPixel = 0.01f;
    public bool flipY = true;

    [Header("Stroke Colors")]
    public Color templateColor = new Color(1f, 1f, 1f, 0.18f);
    public Color currentStrokeColor = new Color(1f, 1f, 1f, 1f);
    public Color completedStrokeColor = new Color(0.8f, 0.8f, 0.8f, 0.65f);

    [Header("Sampling / Playback")]
    [Range(4, 64)] public int curveSteps = 24;
    public bool playOnStart = true;
    public float secondsPerStroke = 0.7f;
    public float pauseBetweenStrokes = 0.2f;

    [Header("Layer Usage")]
    public bool buildTemplateLayer = true;
    public bool buildAnimatedLayer = true;
    public bool buildStrokeNumbers = false;

    [Header("Stroke Numbers")]
    public bool showStrokeNumbers = true;
    public float numberFontSize = 2.5f;
    public Color numberColor = Color.black;
    public Vector3 numberScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Hierarchy")]
    public Transform templateParent;
    public Transform animatedParent;
    public Transform numbersParent;

    private readonly List<List<Vector3>> strokePointSets = new();
    private readonly List<LineRenderer> templateLines = new();
    private readonly List<LineRenderer> animatedLines = new();
    private readonly List<GameObject> numberObjects = new();

    private Coroutine playRoutine;
    private int currentStrokeIndex = -1;

    [System.Serializable]
    private class StrokeNumberData
    {
        public string text;
        public Vector2 position;
    }

    void Awake()
    {
        EnsureParents();
    }

    void Start()
    {
        EnsureParents();
    }

    [ContextMenu("Reload SVG")]
    public void ReloadSvg()
    {
        EnsureParents();

        StopPlayback();
        ClearChildren(templateParent);
        ClearChildren(animatedParent);
        ClearChildren(numbersParent);

        strokePointSets.Clear();
        templateLines.Clear();
        animatedLines.Clear();
        numberObjects.Clear();
        currentStrokeIndex = -1;

        LoadAndBuild();
    }

    public void LoadSvgFile(string newSvgFileName)
    {
        if (string.IsNullOrWhiteSpace(newSvgFileName))
        {
            Debug.LogError("LoadSvgFile called with empty file name.");
            return;
        }

        EnsureParents();

        svgFileName = newSvgFileName;
        ReloadSvg();

        if (playOnStart && buildAnimatedLayer)
            Play();
    }

    public void Play()
    {
        if (!buildAnimatedLayer || animatedLines.Count == 0)
            return;

        StopPlayback();
        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void StopPlayback()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    public void ShowOnlyStroke(int strokeIndex)
    {
        if (!buildAnimatedLayer || strokeIndex < 0 || strokeIndex >= animatedLines.Count)
            return;

        currentStrokeIndex = strokeIndex;

        for (int i = 0; i < animatedLines.Count; i++)
        {
            animatedLines[i].gameObject.SetActive(i == strokeIndex);
            animatedLines[i].startColor = currentStrokeColor;
            animatedLines[i].endColor = currentStrokeColor;
        }
    }

    public void ShowAllAsTemplateOnly()
    {
        for (int i = 0; i < animatedLines.Count; i++)
            animatedLines[i].gameObject.SetActive(false);
    }

    public void ShowTemplateOnly()
    {
        if (templateParent != null)
            templateParent.gameObject.SetActive(buildTemplateLayer);

        if (animatedParent != null)
            animatedParent.gameObject.SetActive(false);

        if (numbersParent != null)
            numbersParent.gameObject.SetActive(false);
    }

    public void ShowInstructionOnly()
    {
        if (templateParent != null)
            templateParent.gameObject.SetActive(false);

        if (animatedParent != null)
            animatedParent.gameObject.SetActive(buildAnimatedLayer);

        if (numbersParent != null)
            numbersParent.gameObject.SetActive(buildStrokeNumbers && showStrokeNumbers);
    }

    public void ShowTemplateAndInstruction()
    {
        if (templateParent != null)
            templateParent.gameObject.SetActive(buildTemplateLayer);

        if (animatedParent != null)
            animatedParent.gameObject.SetActive(buildAnimatedLayer);

        if (numbersParent != null)
            numbersParent.gameObject.SetActive(buildStrokeNumbers && showStrokeNumbers);
    }

    public int GetStrokeCount()
    {
        return strokePointSets.Count;
    }

    public void MarkStrokeCompleted(int strokeIndex)
    {
        if (!buildAnimatedLayer || strokeIndex < 0 || strokeIndex >= animatedLines.Count)
            return;

        animatedLines[strokeIndex].gameObject.SetActive(true);
        animatedLines[strokeIndex].startColor = completedStrokeColor;
        animatedLines[strokeIndex].endColor = completedStrokeColor;
    }

    private void LoadAndBuild()
    {
        if (string.IsNullOrWhiteSpace(svgFileName))
        {
            Debug.LogWarning("KanjiVGStrokePlayer: svgFileName is empty.");
            return;
        }

        string fullPath = Path.Combine(Application.streamingAssetsPath, svgFolder, svgFileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"KanjiVG SVG not found: {fullPath}");
            return;
        }

        string xmlText = File.ReadAllText(fullPath);

        List<string> strokePaths = ExtractStrokePathData(xmlText);
        List<StrokeNumberData> strokeNumbers = ExtractStrokeNumbers(xmlText);

        Debug.Log($"Stroke paths found: {strokePaths.Count}");
        Debug.Log($"Stroke numbers found: {strokeNumbers.Count}");
        Debug.Log($"buildStrokeNumbers={buildStrokeNumbers}, showStrokeNumbers={showStrokeNumbers}");


        if (strokePaths.Count == 0)
        {
            Debug.LogError("No stroke paths found in SVG.");
            return;
        }

        foreach (string d in strokePaths)
        {
            List<Vector2> sampled2D = SvgPathSampler.SamplePath(d, curveSteps);
            List<Vector3> sampled3D = ConvertSvgPoints(sampled2D);
            strokePointSets.Add(sampled3D);
        }

        if (buildTemplateLayer)
            BuildTemplateLines();

        if (buildAnimatedLayer)
            BuildAnimatedLines();

        if (buildStrokeNumbers)
            BuildStrokeNumberLabels(strokeNumbers);

        CenterAllLayers();

        if (numbersParent != null)
            numbersParent.gameObject.SetActive(buildStrokeNumbers && showStrokeNumbers);
    }

    private List<string> ExtractStrokePathData(string xmlText)
    {
        var results = new List<string>();
        XDocument doc = XDocument.Parse(xmlText);

        var allGroups = doc.Descendants().Where(e => e.Name.LocalName == "g").ToList();

        XElement strokePathsGroup = allGroups.FirstOrDefault(g =>
        {
            var idAttr = g.Attribute("id");
            if (idAttr == null) return false;
            return idAttr.Value.ToLower().Contains("strokepaths");
        });

        IEnumerable<XElement> pathElements;

        if (strokePathsGroup != null)
            pathElements = strokePathsGroup.Descendants().Where(e => e.Name.LocalName == "path");
        else
            pathElements = doc.Descendants().Where(e => e.Name.LocalName == "path");

        foreach (var p in pathElements)
        {
            var dAttr = p.Attribute("d");
            if (dAttr != null && !string.IsNullOrWhiteSpace(dAttr.Value))
                results.Add(dAttr.Value);
        }

        return results;
    }

    private List<StrokeNumberData> ExtractStrokeNumbers(string xmlText)
    {
        var results = new List<StrokeNumberData>();
        XDocument doc = XDocument.Parse(xmlText);

        var allGroups = doc.Descendants().Where(e => e.Name.LocalName == "g").ToList();

        XElement strokeNumbersGroup = allGroups.FirstOrDefault(g =>
        {
            var idAttr = g.Attribute("id");
            if (idAttr == null) return false;
            return idAttr.Value.ToLower().Contains("strokenumbers");
        });

        if (strokeNumbersGroup == null)
            return results;

        var textElements = strokeNumbersGroup.Descendants()
            .Where(e => e.Name.LocalName == "text");

        foreach (var t in textElements)
        {
            string textValue = t.Value?.Trim();
            if (string.IsNullOrWhiteSpace(textValue))
                continue;

            Vector2? pos = null;

            var transformAttr = t.Attribute("transform");
            if (transformAttr != null)
                pos = ParseTransformPosition(transformAttr.Value);

            Debug.Log($"Stroke number text='{textValue}', transform='{transformAttr?.Value}', x='{t.Attribute("x")?.Value}', y='{t.Attribute("y")?.Value}'");
            if (!pos.HasValue)
            {
                var xAttr = t.Attribute("x");
                var yAttr = t.Attribute("y");

                if (xAttr != null && yAttr != null &&
                    float.TryParse(xAttr.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(yAttr.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    pos = new Vector2(x, y);
                }
            }

            if (pos.HasValue)
            {
                results.Add(new StrokeNumberData
                {
                    text = textValue,
                    position = pos.Value
                });
            }
        }

        return results;
    }

    private Vector2? ParseTransformPosition(string transformValue)
    {
        if (string.IsNullOrWhiteSpace(transformValue))
            return null;

        transformValue = transformValue.Trim();

        // translate(x y) or translate(x,y)
        if (transformValue.StartsWith("translate(") && transformValue.EndsWith(")"))
        {
            string inner = transformValue.Substring("translate(".Length);
            inner = inner.Substring(0, inner.Length - 1);

            string[] parts = inner
                .Replace(",", " ")
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tx) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ty))
            {
                return new Vector2(tx, ty);
            }
        }

        // matrix(a b c d e f) -> position is e,f
        if (transformValue.StartsWith("matrix(") && transformValue.EndsWith(")"))
        {
            string inner = transformValue.Substring("matrix(".Length);
            inner = inner.Substring(0, inner.Length - 1);

            string[] parts = inner
                .Replace(",", " ")
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 6 &&
                float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mx) &&
                float.TryParse(parts[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float my))
            {
                return new Vector2(mx, my);
            }
        }

        return null;
    }

    private List<Vector3> ConvertSvgPoints(List<Vector2> svgPoints)
    {
        var output = new List<Vector3>(svgPoints.Count);

        foreach (var p in svgPoints)
        {
            float x = p.x * unitsPerSvgPixel;
            float y = (flipY ? -p.y : p.y) * unitsPerSvgPixel;
            output.Add(new Vector3(x, y, 0f));
        }

        return output;
    }

    private void BuildTemplateLines()
    {
        for (int i = 0; i < strokePointSets.Count; i++)
        {
            LineRenderer lr = CreateLine($"Template_Stroke_{i + 1}", templateParent, templateColor);
            SetLinePoints(lr, strokePointSets[i]);
            templateLines.Add(lr);
        }
    }

    private void BuildAnimatedLines()
    {
        for (int i = 0; i < strokePointSets.Count; i++)
        {
            LineRenderer lr = CreateLine($"Animated_Stroke_{i + 1}", animatedParent, currentStrokeColor);
            SetLinePoints(lr, strokePointSets[i]);
            lr.gameObject.SetActive(false);
            animatedLines.Add(lr);
        }
    }

    //private void BuildStrokeNumberLabels(List<StrokeNumberData> numbers)
    //{
    //    if (!showStrokeNumbers || numbers == null || numbers.Count == 0)
    //        return;

    //    foreach (var n in numbers)
    //    {
    //        GameObject go = new GameObject($"StrokeNumber_{n.text}");
    //        go.transform.SetParent(numbersParent, false);

    //        float x = n.position.x * unitsPerSvgPixel;
    //        float y = (flipY ? -n.position.y : n.position.y) * unitsPerSvgPixel;
    //        go.transform.localPosition = new Vector3(x, y, 0f);
    //        go.transform.localScale = numberScale;

    //        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
    //        tmp.text = n.text;
    //        tmp.fontSize = numberFontSize;
    //        tmp.color = numberColor;
    //        tmp.alignment = TextAlignmentOptions.Center;
    //        tmp.enableWordWrapping = false;
    //        tmp.raycastTarget = false;

    //        numberObjects.Add(go);
    //    }
    //}
    private void BuildStrokeNumberLabels(List<StrokeNumberData> numbers)
    {
        if (!showStrokeNumbers || numbers == null || numbers.Count == 0)
            return;

        foreach (var n in numbers)
        {
            GameObject go = new GameObject($"StrokeNumber_{n.text}");
            go.transform.SetParent(numbersParent, false);

            float x = n.position.x * unitsPerSvgPixel;
            float y = (flipY ? -n.position.y : n.position.y) * unitsPerSvgPixel;

            // Push a little toward the camera so it is not hidden by the line
            go.transform.localPosition = new Vector3(x, y, -0.02f);

            // Make it much larger for debugging
            go.transform.localScale = Vector3.one * 0.05f;

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = n.text;
            tmp.fontSize = 10f;
            tmp.color = Color.red;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            // Make bounds larger and easier to see
            tmp.rectTransform.sizeDelta = new Vector2(5f, 5f);

            numberObjects.Add(go);

            Debug.Log($"Created number {n.text} at local position {go.transform.localPosition}");
        }
    }

    private LineRenderer CreateLine(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = lineMaterial != null ? lineMaterial : DefaultLineMaterial();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;
        lr.alignment = LineAlignment.TransformZ;
        lr.textureMode = LineTextureMode.Stretch;
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        return lr;
    }

    private Material DefaultLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        return new Material(shader);
    }

    private void SetLinePoints(LineRenderer lr, List<Vector3> points)
    {
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }

    private IEnumerator PlayRoutine()
    {
        if (numbersParent != null)
            numbersParent.gameObject.SetActive(buildStrokeNumbers && showStrokeNumbers);

        for (int i = 0; i < animatedLines.Count; i++)
            animatedLines[i].gameObject.SetActive(false);

        for (int i = 0; i < animatedLines.Count; i++)
        {
            currentStrokeIndex = i;
            yield return AnimateSingleStroke(animatedLines[i], strokePointSets[i], secondsPerStroke);

            animatedLines[i].startColor = completedStrokeColor;
            animatedLines[i].endColor = completedStrokeColor;

            yield return new WaitForSeconds(pauseBetweenStrokes);
        }

        currentStrokeIndex = animatedLines.Count - 1;
    }

    private IEnumerator AnimateSingleStroke(LineRenderer lr, List<Vector3> points, float duration)
    {
        lr.gameObject.SetActive(true);

        if (points.Count < 2)
        {
            lr.positionCount = points.Count;
            if (points.Count == 1)
                lr.SetPosition(0, points[0]);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            int visibleCount = Mathf.Clamp(Mathf.CeilToInt(t * points.Count), 2, points.Count);
            lr.positionCount = visibleCount;

            for (int i = 0; i < visibleCount; i++)
                lr.SetPosition(i, points[i]);

            lr.startColor = currentStrokeColor;
            lr.endColor = currentStrokeColor;

            yield return null;
        }

        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }

    private void CenterAllLayers()
    {
        var allPoints = new List<Vector3>();

        foreach (var stroke in strokePointSets)
            allPoints.AddRange(stroke);

        if (allPoints.Count == 0)
            return;

        Vector3 min = allPoints[0];
        Vector3 max = allPoints[0];

        foreach (var p in allPoints)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 center = (min + max) * 0.5f;

        if (templateParent != null)
            templateParent.localPosition = -center;

        if (animatedParent != null)
            animatedParent.localPosition = -center;

        if (numbersParent != null)
            numbersParent.localPosition = -center;
    }

    private void EnsureParents()
    {
        if (templateParent == null)
        {
            GameObject go = new GameObject("TemplateLayer");
            go.transform.SetParent(transform, false);
            templateParent = go.transform;
        }

        if (animatedParent == null)
        {
            GameObject go = new GameObject("AnimatedLayer");
            go.transform.SetParent(transform, false);
            animatedParent = go.transform;
        }

        if (numbersParent == null)
        {
            GameObject go = new GameObject("StrokeNumbersLayer");
            go.transform.SetParent(transform, false);
            numbersParent = go.transform;
        }
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(parent.GetChild(i).gameObject);
            else
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    public List<Vector3> GetStrokePointsCopy(int strokeIndex)
    {
        if (strokeIndex < 0 || strokeIndex >= strokePointSets.Count)
            return null;

        return new List<Vector3>(strokePointSets[strokeIndex]);
    }

    public List<List<Vector3>> GetAllStrokePointsCopy()
    {
        var copy = new List<List<Vector3>>();

        for (int i = 0; i < strokePointSets.Count; i++)
            copy.Add(new List<Vector3>(strokePointSets[i]));

        return copy;
    }
}