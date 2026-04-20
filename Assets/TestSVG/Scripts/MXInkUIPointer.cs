using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MXInkUIPointer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MXInkStylusHandler stylusHandler;
    [SerializeField] private Transform rayOriginTransform;
    [SerializeField] private Transform rayForwardTransform;

    [Header("Ray Settings")]
    [SerializeField] private float maxDistance = 3f;
    [SerializeField] private LayerMask pointerTargetMask;

    [Header("Visual Ray")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private bool showDebugRay = true;

    [Header("Click")]
    [SerializeField] private bool useFrontButtonForClick = true;

    [SerializeField] private Transform pointerTip;
    [SerializeField] private float pointerTipOffset = 0.002f;

    private GameObject currentHover;
    private bool wasPressedLastFrame;

    void Update()
    {
        if (stylusHandler == null)
            return;

        if (!stylusHandler.IsStylusActive)
        {
            ClearHover();
            UpdateLine(Vector3.zero, Vector3.zero, false);
            wasPressedLastFrame = false;
            return;
        }

        Vector3 rayOrigin = rayOriginTransform != null
            ? rayOriginTransform.position
            : stylusHandler.InkingPose.position;

        Vector3 rayDirection = rayForwardTransform != null
            ? rayForwardTransform.forward
            : (stylusHandler.InkingPose.rotation * Vector3.forward);

        Debug.DrawRay(rayOrigin, rayDirection * maxDistance, Color.green);

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, maxDistance, pointerTargetMask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        GameObject bestButtonObject = null;
        RaycastHit? bestButtonHit = null;

        foreach (RaycastHit h in hits)
        {
            Debug.Log($"[MXInkUIPointer] RaycastAll hit: {h.collider.name}, dist={h.distance}");

            GameObject candidateButton = FindButtonObject(h.collider.gameObject);
            if (candidateButton != null)
            {
                bestButtonObject = candidateButton;
                bestButtonHit = h;
                break;
            }
        }

        bool isPressed = useFrontButtonForClick && stylusHandler.FrontPressed;
        bool pressedThisFrame = isPressed && !wasPressedLastFrame;

        if (bestButtonObject != null && bestButtonHit.HasValue)
        {
            HandleHover(bestButtonObject);
            UpdateLine(rayOrigin, bestButtonHit.Value.point, true);

            Debug.Log($"[MXInkUIPointer] Button target: {bestButtonObject.name}, hitCollider={bestButtonHit.Value.collider.name}, pressed={isPressed}");

            if (pressedThisFrame)
            {
                TryClick(bestButtonObject);
            }
        }
        else
        {
            ClearHover();
            UpdateLine(Vector3.zero, Vector3.zero, false);
            Debug.Log($"[MXInkUIPointer] No valid pointer target, origin={rayOrigin}, dir={rayDirection}, maxDistance={maxDistance}");
        }

        wasPressedLastFrame = isPressed;
    }

    private void HandleHover(GameObject buttonObject)
    {
        if (buttonObject == currentHover)
            return;

        ClearHover();
        currentHover = buttonObject;

        if (currentHover != null && EventSystem.current != null)
        {
            ExecuteEvents.Execute(currentHover, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Debug.Log($"[MXInkUIPointer] Hover enter: {currentHover.name}");
        }
    }

    private void ClearHover()
    {
        if (currentHover != null && EventSystem.current != null)
        {
            ExecuteEvents.Execute(currentHover, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            Debug.Log($"[MXInkUIPointer] Hover exit: {currentHover.name}");
        }

        currentHover = null;
    }

    private void TryClick(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            Debug.Log("[MXInkUIPointer] TryClick called with null buttonObject.");
            return;
        }

        Button btn = buttonObject.GetComponent<Button>();
        if (btn == null)
        {
            Debug.Log($"[MXInkUIPointer] Resolved object {buttonObject.name}, but no Button component exists.");
            return;
        }

        if (!btn.interactable)
        {
            Debug.Log($"[MXInkUIPointer] Button {buttonObject.name} is not interactable.");
            return;
        }

        if (EventSystem.current != null)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(buttonObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(buttonObject, eventData, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(buttonObject, eventData, ExecuteEvents.pointerUpHandler);
        }

        btn.onClick.Invoke();
        Debug.Log($"[MXInkUIPointer] Button clicked: {buttonObject.name}");
    }

    private GameObject FindButtonObject(GameObject obj)
    {
        if (obj == null) return null;

        Button button = obj.GetComponent<Button>();
        if (button != null) return button.gameObject;

        button = obj.GetComponentInParent<Button>();
        if (button != null) return button.gameObject;

        return null;
    }

    private void UpdateLine(Vector3 start, Vector3 end, bool valid)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = showDebugRay && valid;

            if (showDebugRay && valid)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
            }
        }

        if (pointerTip != null)
        {
            pointerTip.gameObject.SetActive(valid);

            if (valid)
            {
                Vector3 dir = (end - start).normalized;
                pointerTip.position = end - (dir * pointerTipOffset);
            }
        }
    }
}