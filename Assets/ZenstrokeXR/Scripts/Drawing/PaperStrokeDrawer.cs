using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ZenstrokeXR.Input;
using ZenstrokeXR.Lessons;
using ZenstrokeXR.Validation;

namespace ZenstrokeXR.Drawing
{
    public class PaperStrokeDrawer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MxInkStylusHandler stylusHandler;
        [SerializeField] private DrawingSurface drawingSurface;
        [SerializeField] private StrokeValidator strokeValidator;

        [Header("Containers")]
        [SerializeField] private Transform templateContainer;
        [SerializeField] private Transform strokeContainer;
        [SerializeField] private Transform inkPool;

        [Header("Stroke Rendering")]
        [SerializeField] private Material strokeMaterial;
        [SerializeField] private float minWidth = 0.001f;
        [SerializeField] private float maxWidth = 0.004f;
        [SerializeField] private Color activeStrokeColor = Color.black;
        [SerializeField] private Color lockedStrokeColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color failedStrokeColor = Color.red;

        [Header("Template Rendering")]
        [SerializeField] private Material templateMaterial;
        [SerializeField] private float templateWidth = 0.006f;
        [SerializeField] private Color templateColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        [SerializeField] private Color activeTemplateColor = new Color(0.05f, 0.35f, 0.9f, 0.95f);

        [Header("Performance")]
        [SerializeField] private float minPointDistance = 0.001f;
        [SerializeField] private int maxPointsPerStroke = 500;
        [SerializeField] private int lineRendererPoolSize = 20;

        [Header("Stroke Animation")]
        [SerializeField] private Transform animationPreviewContainer;
        [SerializeField] private float animationPreviewSize = 0.18f;
        [SerializeField] private Color animationColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private float animationDrawSpeed = 0.03f;
        [SerializeField] private float animationPauseBetweenStrokes = 0.6f;
        [SerializeField] private float animationStrokeWidth = 0.005f;

        [Header("Feedback")]
        [SerializeField] private float failFlashDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Runtime state
        private LineRenderer activeLineRenderer;
        private Coroutine animationCoroutine;
        private bool isAnimating;
        private readonly List<LineRenderer> animationStrokes = new List<LineRenderer>();
        private readonly List<Vector3> currentStrokeWorldPoints = new List<Vector3>(256);
        private readonly List<Vector2> currentStrokeNormalized = new List<Vector2>(256);
        private readonly List<float> currentStrokePressures = new List<float>(256);
        private readonly List<LineRenderer> lockedStrokes = new List<LineRenderer>();
        private readonly List<LineRenderer> templateStrokes = new List<LineRenderer>();
        private readonly Queue<LineRenderer> pool = new Queue<LineRenderer>();

        private Vector3 lastRecordedPoint;
        private int highlightedTemplateIndex = -1;

        [SerializeField] private bool highlightPaperDuringPreview = false;

        private void Awake()
        {
            InitPool();
        }

        private void OnEnable()
        {
            if (stylusHandler != null)
            {
                stylusHandler.OnDrawStart += HandleDrawStart;
                stylusHandler.OnDrawPoint += HandleDrawPoint;
                stylusHandler.OnDrawEnd += HandleDrawEnd;
            }

            var mgr = KanjiLessonManager.Instance;
            if (mgr != null)
            {
                mgr.OnKanjiChanged += HandleKanjiChanged;
                mgr.OnStrokeStepChanged += HandleStrokeStepChanged;
                mgr.OnValidationResult += HandleValidationResult;
            }
        }

        private void OnDisable()
        {
            if (stylusHandler != null)
            {
                stylusHandler.OnDrawStart -= HandleDrawStart;
                stylusHandler.OnDrawPoint -= HandleDrawPoint;
                stylusHandler.OnDrawEnd -= HandleDrawEnd;
            }

            var mgr = KanjiLessonManager.Instance;
            if (mgr != null)
            {
                mgr.OnKanjiChanged -= HandleKanjiChanged;
                mgr.OnStrokeStepChanged -= HandleStrokeStepChanged;
                mgr.OnValidationResult -= HandleValidationResult;
            }
        }

        // Subscribe after KanjiLessonManager.Start fires initial events
        private void Start()
        {
            // Re-subscribe in case Instance wasn't ready in OnEnable
            var mgr = KanjiLessonManager.Instance;
            if (mgr != null)
            {
                mgr.OnKanjiChanged -= HandleKanjiChanged;
                mgr.OnStrokeStepChanged -= HandleStrokeStepChanged;
                mgr.OnValidationResult -= HandleValidationResult;

                mgr.OnKanjiChanged += HandleKanjiChanged;
                mgr.OnStrokeStepChanged += HandleStrokeStepChanged;
                mgr.OnValidationResult += HandleValidationResult;
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.aKey.wasPressedThisFrame)
            {
                if (isAnimating)
                    StopStrokeAnimation();
                else
                    PlayStrokeAnimation();
            }
        }

        // ─── Stroke Order Animation ───

        public void PlayStrokeAnimation()
        {
            var mgr = KanjiLessonManager.Instance;
            if (mgr == null || mgr.CurrentKanji == null) return;

            StopStrokeAnimation();
            animationCoroutine = StartCoroutine(StrokeAnimationRoutine());
        }

        public void StopStrokeAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            isAnimating = false;
            ClearAnimationStrokes();
        }

        private Vector3 NormalizedToPreviewLocal(Vector2 normalized)
        {
            float x = (normalized.x - 0.5f) * animationPreviewSize;
            float y = -(normalized.y - 0.5f) * animationPreviewSize;
            return new Vector3(x, y, -0.001f);
        }

        private IEnumerator StrokeAnimationRoutine()
        {

            var mgr = KanjiLessonManager.Instance;
            if (mgr == null || mgr.CurrentKanji == null) yield break;

            if (animationPreviewContainer == null)
            {
                Log("No animationPreviewContainer assigned — skipping animation");
                yield break;
            }
            var targetContainer = animationPreviewContainer;
            float width = animationStrokeWidth;
            var fadeColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);

            isAnimating = true;
            ClearAnimationStrokes();

            // Reset to stroke 0 so templates show fresh on paper
            mgr.ResetCurrentKanji();
            yield return null;

            var kanji = mgr.CurrentKanji;
            Log($"Playing stroke animation for {kanji.Character}");

            for (int s = 0; s < kanji.StrokeCount; s++)
            {
                // Highlight this template stroke on the paper
                //HighlightTemplateStroke(s);

                var points = kanji.GetStrokePoints(s);
                if (points.Count < 2) continue;

                // Create animated stroke LineRenderer
                var lr = GetFromPool();
                lr.transform.SetParent(targetContainer, false);
                lr.material = templateMaterial;
                lr.startColor = animationColor;
                lr.endColor = animationColor;
                lr.startWidth = width;
                lr.endWidth = width;
                lr.positionCount = 0;
                animationStrokes.Add(lr);

                // Progressively draw each point
                for (int i = 0; i < points.Count; i++)
                {
                    if (!isAnimating) yield break;

                    Vector3 local = NormalizedToPreviewLocal(points[i]);
                    local.z = -0.001f;

                    lr.positionCount = i + 1;
                    lr.SetPosition(i, local);

                    yield return new WaitForSeconds(animationDrawSpeed);
                }

                // Fade after drawing
                lr.startColor = fadeColor;
                lr.endColor = fadeColor;

                yield return new WaitForSeconds(animationPauseBetweenStrokes);
            }

            // Finish: wait then reset for practice
            yield return new WaitForSeconds(1f);
            ClearAnimationStrokes();
            mgr.ResetCurrentKanji();
            isAnimating = false;
            animationCoroutine = null;
            Log("Stroke animation complete — ready to practice");
        }

        private void ClearAnimationStrokes()
        {
            foreach (var lr in animationStrokes)
            {
                if (lr != null)
                    ReturnToPool(lr);
            }
            animationStrokes.Clear();
        }

        // ─── Pool Management ───

        private void InitPool()
        {
            for (int i = 0; i < lineRendererPoolSize; i++)
            {
                var lr = CreateLineRenderer();
                lr.gameObject.SetActive(false);
                lr.transform.SetParent(inkPool, false);
                pool.Enqueue(lr);
            }
        }

        private LineRenderer GetFromPool()
        {
            LineRenderer lr;
            if (pool.Count > 0)
            {
                lr = pool.Dequeue();
            }
            else
            {
                lr = CreateLineRenderer();
                Log("Pool exhausted — created new LineRenderer");
            }

            lr.positionCount = 0;
            lr.gameObject.SetActive(true);
            return lr;
        }

        private void ReturnToPool(LineRenderer lr)
        {
            lr.positionCount = 0;
            lr.gameObject.SetActive(false);
            lr.transform.SetParent(inkPool, false);
            pool.Enqueue(lr);
        }

        private LineRenderer CreateLineRenderer()
        {
            var go = new GameObject("StrokeLine");
            var lr = go.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, strokeMaterial);
            return lr;
        }

        private void ConfigureLineRenderer(LineRenderer lr, Material mat)
        {
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.generateLightingData = false;
        }

        // ─── Drawing Lifecycle ───

        private void HandleDrawStart()
        {
            if (isAnimating) return;

            activeLineRenderer = GetFromPool();
            activeLineRenderer.transform.SetParent(strokeContainer, false);
            activeLineRenderer.material = strokeMaterial;
            activeLineRenderer.startColor = activeStrokeColor;
            activeLineRenderer.endColor = activeStrokeColor;
            activeLineRenderer.startWidth = minWidth;
            activeLineRenderer.endWidth = minWidth;

            currentStrokeWorldPoints.Clear();
            currentStrokeNormalized.Clear();
            currentStrokePressures.Clear();
            lastRecordedPoint = Vector3.positiveInfinity;

            Log("Stroke started");
        }

        private void HandleDrawPoint(Vector3 worldPos, float pressure)
        {
            if (activeLineRenderer == null) return;
            if (currentStrokeWorldPoints.Count >= maxPointsPerStroke) return;

            // Convert to local space for LineRenderer (useWorldSpace=false)
            // All children of PaperObject share the same local space
            Vector3 localPos = drawingSurface.WorldToLocal(worldPos);
            // Slight offset to render above surface
            localPos.z = -0.001f;

            // Min distance check
            if (currentStrokeWorldPoints.Count > 0)
            {
                float dist = Vector3.Distance(localPos, lastRecordedPoint);
                if (dist < minPointDistance) return;
            }

            // Record point
            currentStrokeWorldPoints.Add(localPos);
            currentStrokePressures.Add(pressure);
            lastRecordedPoint = localPos;

            // Also record normalized coordinates for validation
            if (drawingSurface.TryGetNormalizedPoint(worldPos, out Vector2 norm))
                currentStrokeNormalized.Add(norm);

            // Update LineRenderer
            int count = currentStrokeWorldPoints.Count;
            activeLineRenderer.positionCount = count;
            activeLineRenderer.SetPosition(count - 1, localPos);

            // Update width from pressure
            UpdateWidthCurve();
        }

        private void HandleDrawEnd()
        {
            if (activeLineRenderer == null) return;

            Log($"Stroke ended with {currentStrokeNormalized.Count} normalized points");

            var mgr = KanjiLessonManager.Instance;
            if (mgr == null || strokeValidator == null)
            {
                ReturnToPool(activeLineRenderer);
                activeLineRenderer = null;
                return;
            }

            // Get template for current expected stroke
            var templatePoints = mgr.CurrentKanji?.GetStrokePoints(mgr.CurrentStrokeIndex);
            if (templatePoints == null || templatePoints.Count == 0)
            {
                Log("No template available for current stroke");
                ReturnToPool(activeLineRenderer);
                activeLineRenderer = null;
                return;
            }

            // Make a copy for validation (Resample modifies in-place)
            var drawnCopy = new List<Vector2>(currentStrokeNormalized);
            var templateCopy = new List<Vector2>(templatePoints);

            bool passed = strokeValidator.ValidateStroke(drawnCopy, templateCopy);
            mgr.ReportStrokeResult(passed);

            // activeLineRenderer is handled by HandleValidationResult
        }

        private void HandleValidationResult(bool passed)
        {
            if (activeLineRenderer == null) return;

            if (passed)
            {
                LockCurrentStroke();
            }
            else
            {
                StartCoroutine(FlashAndRemoveStroke());
            }
        }

        private void LockCurrentStroke()
        {
            if (activeLineRenderer == null) return;

            activeLineRenderer.startColor = lockedStrokeColor;
            activeLineRenderer.endColor = lockedStrokeColor;
            lockedStrokes.Add(activeLineRenderer);
            activeLineRenderer = null;

            Log("Stroke locked (correct)");
        }

        private IEnumerator FlashAndRemoveStroke()
        {
            if (activeLineRenderer == null) yield break;

            var lr = activeLineRenderer;
            activeLineRenderer = null;

            // Flash red
            lr.startColor = failedStrokeColor;
            lr.endColor = failedStrokeColor;

            yield return new WaitForSeconds(failFlashDuration);

            ReturnToPool(lr);
            Log("Stroke removed (incorrect)");
        }

        private void UpdateWidthCurve()
        {
            if (activeLineRenderer == null || currentStrokePressures.Count == 0) return;

            int count = currentStrokePressures.Count;
            var keys = new Keyframe[count];

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                float width = Mathf.Lerp(minWidth, maxWidth, currentStrokePressures[i]);
                keys[i] = new Keyframe(t, width);
            }

            activeLineRenderer.widthCurve = new AnimationCurve(keys);
        }

        // ─── Template Rendering ───

        private void HandleKanjiChanged(KanjiData kanji)
        {
            ClearAllStrokes();
            ClearTemplateStrokes();

            if (kanji == null) return;

            RenderKanjiTemplate(kanji);
            Log($"Template rendered for {kanji.Character} ({kanji.StrokeCount} strokes)");
        }

        private void HandleStrokeStepChanged(int strokeIndex)
        {
            HighlightTemplateStroke(strokeIndex);
        }

        private void RenderKanjiTemplate(KanjiData kanji)
        {
            for (int s = 0; s < kanji.StrokeCount; s++)
            {
                var points = kanji.GetStrokePoints(s);
                if (points.Count < 2) continue;

                var lr = GetFromPool();
                lr.transform.SetParent(templateContainer, false);
                lr.material = templateMaterial;
                lr.startColor = templateColor;
                lr.endColor = templateColor;
                lr.startWidth = templateWidth;
                lr.endWidth = templateWidth;

                lr.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 local = drawingSurface.NormalizedToLocal(points[i]);
                    local.z = -0.0005f; // Slightly behind ink layer
                    lr.SetPosition(i, local);
                }

                templateStrokes.Add(lr);
            }
        }

        private void HighlightTemplateStroke(int strokeIndex)
        {
            for (int i = 0; i < templateStrokes.Count; i++)
            {
                if (templateStrokes[i] == null) continue;

                bool isActive = (i == strokeIndex);
                bool isCompleted = (i < strokeIndex);

                if (isCompleted)
                {
                    templateStrokes[i].startColor = new Color(templateColor.r, templateColor.g, templateColor.b, 0.1f);
                    templateStrokes[i].endColor = new Color(templateColor.r, templateColor.g, templateColor.b, 0.1f);
                }
                else if (isActive)
                {
                    templateStrokes[i].startColor = activeTemplateColor;
                    templateStrokes[i].endColor = activeTemplateColor;
                    templateStrokes[i].startWidth = templateWidth * 1.5f;
                    templateStrokes[i].endWidth = templateWidth * 1.5f;
                }
                else
                {
                    templateStrokes[i].startColor = templateColor;
                    templateStrokes[i].endColor = templateColor;
                    templateStrokes[i].startWidth = templateWidth;
                    templateStrokes[i].endWidth = templateWidth;
                }
            }

            highlightedTemplateIndex = strokeIndex;
        }

        private void ClearTemplateStrokes()
        {
            foreach (var lr in templateStrokes)
            {
                if (lr != null)
                    ReturnToPool(lr);
            }
            templateStrokes.Clear();
            highlightedTemplateIndex = -1;
        }

        private void ClearAllStrokes()
        {
            if (activeLineRenderer != null)
            {
                ReturnToPool(activeLineRenderer);
                activeLineRenderer = null;
            }

            foreach (var lr in lockedStrokes)
            {
                if (lr != null)
                    ReturnToPool(lr);
            }
            lockedStrokes.Clear();

            currentStrokeWorldPoints.Clear();
            currentStrokeNormalized.Clear();
            currentStrokePressures.Clear();
        }

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[PaperStrokeDrawer] {msg}");
        }
    }
}
