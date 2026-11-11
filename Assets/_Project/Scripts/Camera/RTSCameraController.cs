using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnOfShapes.CameraControl
{
    /// <summary>
    /// Ground-level rig that a child camera orbits. WASD/arrows and screen-edge pan,
    /// mouse wheel zoom, Q/E rotate. Pitch eases from steep (zoomed out) to shallow (zoomed in).
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 30f;
        [SerializeField] private float edgePanBorder = 12f;
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private Vector2 boundsMin = new Vector2(-100f, -100f);
        [SerializeField] private Vector2 boundsMax = new Vector2(100f, 100f);

        [Header("Zoom")]
        [SerializeField] private float minDistance = 12f;
        [SerializeField] private float maxDistance = 90f;
        [SerializeField] private float zoomStep = 6f;
        [SerializeField] private float minPitch = 35f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Rotate")]
        [SerializeField] private float rotateSpeed = 90f;

        [Header("Smoothing")]
        [SerializeField] private float smoothing = 10f;

        private Vector3 targetPosition;
        private float targetYaw;
        private float targetDistance;
        private float yaw;
        private float distance;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            targetPosition = transform.position;
            targetYaw = yaw = transform.eulerAngles.y;
            targetDistance = distance = (minDistance + maxDistance) * 0.5f;
        }

        public void Configure(Camera cam, Vector2 min, Vector2 max)
        {
            targetCamera = cam;
            boundsMin = min;
            boundsMax = max;
        }

        public void JumpTo(Vector3 position, float yawDegrees)
        {
            targetPosition = position;
            transform.position = position;
            targetYaw = yaw = yawDegrees;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ApplyCameraTransform();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            Vector2 pan = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;

                if (keyboard.qKey.isPressed) targetYaw += rotateSpeed * dt;
                if (keyboard.eKey.isPressed) targetYaw -= rotateSpeed * dt;
            }

            if (mouse != null)
            {
                if (edgePanEnabled && Application.isFocused)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    if (mp.x >= 0 && mp.y >= 0 && mp.x <= Screen.width && mp.y <= Screen.height)
                    {
                        if (mp.x < edgePanBorder) pan.x -= 1f;
                        else if (mp.x > Screen.width - edgePanBorder) pan.x += 1f;
                        if (mp.y < edgePanBorder) pan.y -= 1f;
                        else if (mp.y > Screen.height - edgePanBorder) pan.y += 1f;
                    }
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetDistance -= Mathf.Sign(scroll) * zoomStep;
                }
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

            if (pan.sqrMagnitude > 0f)
            {
                pan = Vector2.ClampMagnitude(pan, 1f);
                // Pan faster when zoomed out.
                float speed = panSpeed * Mathf.Lerp(0.5f, 1.5f, Mathf.InverseLerp(minDistance, maxDistance, targetDistance));
                Quaternion yawRot = Quaternion.Euler(0f, targetYaw, 0f);
                Vector3 move = yawRot * new Vector3(pan.x, 0f, pan.y);
                targetPosition += move * speed * dt;
            }

            targetPosition.x = Mathf.Clamp(targetPosition.x, boundsMin.x, boundsMax.x);
            targetPosition.z = Mathf.Clamp(targetPosition.z, boundsMin.y, boundsMax.y);

            float t = 1f - Mathf.Exp(-smoothing * dt);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            distance = Mathf.Lerp(distance, targetDistance, t);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            if (targetCamera == null) return;
            float pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.InverseLerp(minDistance, maxDistance, distance));
            Quaternion localRot = Quaternion.Euler(pitch, 0f, 0f);
            Transform camT = targetCamera.transform;
            camT.localRotation = localRot;
            camT.localPosition = localRot * new Vector3(0f, 0f, -distance);
        }
    }
}
