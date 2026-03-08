using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PaperDrawingSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform drawingRect;
    [SerializeField] private SimpleStrokeDrawer strokeDrawer;
    [SerializeField] private TMPro.TextMeshProUGUI resultText;
    [SerializeField] private KanjiStrokeValidator kanjiStrokeValidator;

    private bool isDrawing;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            strokeDrawer.ClearAllStrokes();
        }
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (kanjiStrokeValidator != null && kanjiStrokeValidator.IsKanjiComplete())
            return;

        if (resultText != null)
            resultText.text = "";

        isDrawing = true;

        if (TryGetLocalPoint(eventData, out Vector2 localPoint))
        {
            strokeDrawer.BeginStroke(localPoint);
            Debug.Log($"Start Draw: {localPoint}");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDrawing) return;

        if (TryGetLocalPoint(eventData, out Vector2 localPoint))
        {
            strokeDrawer.AddPoint(localPoint);
            Debug.Log($"Drawing: {localPoint}");
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (TryGetLocalPoint(eventData, out Vector2 localPoint))
        {
            strokeDrawer.AddPoint(localPoint);
            strokeDrawer.EndStroke();
            Debug.Log($"End Draw: {localPoint}");
        }
    }

    private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        if (drawingRect == null)
        {
            Debug.LogError("Drawing Rect is not assigned.");
            localPoint = default;
            return false;
        }

        bool gotPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            drawingRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        if (!gotPoint)
            return false;

        return drawingRect.rect.Contains(localPoint);
    }
}