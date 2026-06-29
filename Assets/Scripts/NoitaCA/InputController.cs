using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NoitaCA
{
    public sealed class InputController : MonoBehaviour
    {
        [SerializeField] private int brushSize = 2;
        [SerializeField] private int minBrushSize = 1;
        [SerializeField] private int maxBrushSize = 16;
        [SerializeField] private float zoomSpeed = 1.2f;
        [SerializeField] private float minOrthographicSize = 1.5f;
        [SerializeField] private float maxOrthographicSize = 40f;
        [SerializeField] private MaterialType selectedMaterial = MaterialType.Water;

        private PixelGrid grid;
        private PixelWorldRenderer worldRenderer;
        private Camera targetCamera;

        public int BrushSize => brushSize;
        public MaterialType SelectedMaterial => selectedMaterial;

        public void Initialize(PixelGrid worldGrid, PixelWorldRenderer renderer, Camera cameraToControl)
        {
            grid = worldGrid;
            worldRenderer = renderer;
            targetCamera = cameraToControl;
            brushSize = Mathf.Clamp(brushSize, minBrushSize, maxBrushSize);
        }

        public void Tick()
        {
            if (grid == null || worldRenderer == null || targetCamera == null)
            {
                return;
            }

            float scroll = ReadScrollDelta();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (IsZoomModifierHeld())
                {
                    ZoomCamera(scroll);
                }
                else
                {
                    ResizeBrush(scroll);
                }
            }

            ReadMaterialHotkeys();

            if (IsPrimaryButtonPressed())
            {
                PaintAtPointer(selectedMaterial);
            }

            if (IsSecondaryButtonPressed())
            {
                PaintAtPointer(MaterialType.Air);
            }
        }

        private void PaintAtPointer(MaterialType materialType)
        {
            Vector3 screenPosition = ReadPointerScreenPosition();
            screenPosition.z = Mathf.Abs(targetCamera.transform.position.z - worldRenderer.transform.position.z);

            Vector3 worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
            Vector2Int cell = worldRenderer.WorldToCell(worldPosition);
            grid.PaintCircle(cell.x, cell.y, brushSize, materialType);
        }

        private void ReadMaterialHotkeys()
        {
            if (WasKeyPressed(KeyCode.Alpha1))
            {
                selectedMaterial = MaterialType.Sand;
            }
            else if (WasKeyPressed(KeyCode.Alpha2))
            {
                selectedMaterial = MaterialType.Water;
            }
            else if (WasKeyPressed(KeyCode.Alpha3))
            {
                selectedMaterial = MaterialType.Smoke;
            }
            else if (WasKeyPressed(KeyCode.Alpha4))
            {
                selectedMaterial = MaterialType.Fire;
            }
            else if (WasKeyPressed(KeyCode.Alpha5))
            {
                selectedMaterial = MaterialType.Stone;
            }
            else if (WasKeyPressed(KeyCode.Alpha6))
            {
                selectedMaterial = MaterialType.Wood;
            }
            else if (WasKeyPressed(KeyCode.Alpha0))
            {
                selectedMaterial = MaterialType.Air;
            }
        }

        private void ResizeBrush(float scroll)
        {
            int direction = scroll > 0f ? 1 : -1;
            brushSize = Mathf.Clamp(brushSize + direction, minBrushSize, maxBrushSize);
        }

        private void ZoomCamera(float scroll)
        {
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - scroll * zoomSpeed,
                minOrthographicSize,
                maxOrthographicSize);
        }

        private static Vector3 ReadPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                return new Vector3(position.x, position.y, 0f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        private static bool IsPrimaryButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        private static bool IsSecondaryButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static float ReadScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y / 120f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private static bool IsZoomModifierHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftCtrlKey.isPressed
                    || Keyboard.current.rightCtrlKey.isPressed
                    || Keyboard.current.leftCommandKey.isPressed
                    || Keyboard.current.rightCommandKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(KeyCode.LeftControl)
                || UnityEngine.Input.GetKey(KeyCode.RightControl)
                || UnityEngine.Input.GetKey(KeyCode.LeftCommand)
                || UnityEngine.Input.GetKey(KeyCode.RightCommand);
#else
            return false;
#endif
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switch (keyCode)
                {
                    case KeyCode.Alpha0:
                        return Keyboard.current.digit0Key.wasPressedThisFrame;
                    case KeyCode.Alpha1:
                        return Keyboard.current.digit1Key.wasPressedThisFrame;
                    case KeyCode.Alpha2:
                        return Keyboard.current.digit2Key.wasPressedThisFrame;
                    case KeyCode.Alpha3:
                        return Keyboard.current.digit3Key.wasPressedThisFrame;
                    case KeyCode.Alpha4:
                        return Keyboard.current.digit4Key.wasPressedThisFrame;
                    case KeyCode.Alpha5:
                        return Keyboard.current.digit5Key.wasPressedThisFrame;
                    case KeyCode.Alpha6:
                        return Keyboard.current.digit6Key.wasPressedThisFrame;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }
    }
}
