using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZenstrokeXR.Input
{
    public class MxInkStylusHandler : MonoBehaviour
    {
        [Header("VR Configuration")]
        [SerializeField] private OVRInput.Controller preferredHand = OVRInput.Controller.RTouch;
        [SerializeField] private Transform handAnchor; // RightHandAnchor from OVRCameraRig

        [Header("Pressure")]
        [SerializeField] private float tipPressThreshold = 0.1f;

        [Header("Mouse Fallback")]
        [SerializeField] private Camera fallbackCamera;
        [SerializeField] private LayerMask drawingSurfaceLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Events
        public event Action OnDrawStart;
        public event Action<Vector3, float> OnDrawPoint; // world pos, pressure
        public event Action OnDrawEnd;
        public event Action OnFrontButtonPressed;
        public event Action OnMiddleButtonPressed;
        public event Action OnBackButtonPressed;

        // State
        private bool isOvrAvailable;
        private bool isDrawing;
        private float currentPressure;
        private Vector3 currentPosition;

        // Properties
        public bool IsDrawing => isDrawing;
        public float CurrentPressure => currentPressure;
        public Vector3 StylusPosition => currentPosition;
        public bool IsVRMode => isOvrAvailable;

        private void Awake()
        {
            isOvrAvailable = CheckOVRAvailable();
            Log($"Input mode: {(isOvrAvailable ? "VR (OVRInput)" : "Mouse fallback")}");
        }

        private void Update()
        {
            if (isOvrAvailable)
                UpdateVR();
            else
                UpdateMouse();
        }

        private bool CheckOVRAvailable()
        {
            try
            {
                return OVRManager.isHmdPresent;
            }
            catch
            {
                return false;
            }
        }

        // ─── VR Path ───

        private void UpdateVR()
        {
            // Read pose from hand anchor
            if (handAnchor != null)
                currentPosition = handAnchor.position;
            else
                currentPosition = OVRInput.GetLocalControllerPosition(preferredHand);

            // Read stylus pressure
            currentPressure = OVRInput.Get(OVRInput.Axis1D.PrimaryStylusForce, preferredHand);

            // Drawing state machine
            bool tipPressed = currentPressure > tipPressThreshold;

            if (tipPressed && !isDrawing)
            {
                isDrawing = true;
                OnDrawStart?.Invoke();
                Log("VR draw start");
            }
            else if (tipPressed && isDrawing)
            {
                OnDrawPoint?.Invoke(currentPosition, currentPressure);
            }
            else if (!tipPressed && isDrawing)
            {
                isDrawing = false;
                OnDrawEnd?.Invoke();
                Log("VR draw end");
            }

            // Cluster buttons
            if (OVRInput.GetDown(OVRInput.RawButton.A, preferredHand))
                OnFrontButtonPressed?.Invoke();
            if (OVRInput.GetDown(OVRInput.RawButton.B, preferredHand))
                OnMiddleButtonPressed?.Invoke();
            if (OVRInput.GetDown(OVRInput.RawButton.X, preferredHand))
                OnBackButtonPressed?.Invoke();
        }

        // ─── Mouse Fallback ───

        private void UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Camera cam = fallbackCamera != null ? fallbackCamera : Camera.main;
            if (cam == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            bool hitSurface = Physics.Raycast(ray, out RaycastHit hit, 100f, drawingSurfaceLayer);

            if (hitSurface)
            {
                currentPosition = hit.point;
                currentPressure = 1.0f; // No pressure from mouse

                if (mouse.leftButton.wasPressedThisFrame && !isDrawing)
                {
                    isDrawing = true;
                    OnDrawStart?.Invoke();
                    Log("Mouse draw start");
                }

                if (mouse.leftButton.isPressed && isDrawing)
                {
                    OnDrawPoint?.Invoke(currentPosition, currentPressure);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && isDrawing)
            {
                isDrawing = false;
                OnDrawEnd?.Invoke();
                Log("Mouse draw end");
            }

            // Keyboard fallback for cluster buttons
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.fKey.wasPressedThisFrame)
                OnFrontButtonPressed?.Invoke();
            if (keyboard.spaceKey.wasPressedThisFrame)
                OnMiddleButtonPressed?.Invoke();
            if (keyboard.bKey.wasPressedThisFrame)
                OnBackButtonPressed?.Invoke();
        }

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[MxInkStylus] {msg}");
        }
    }
}
