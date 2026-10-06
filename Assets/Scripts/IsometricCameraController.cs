using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// InputSystem_Actions에는 카메라 이동/줌 액션이 없다.
// 기존 액션 에셋은 바꾸지 않고, 이미 활성화된 Input System의 키보드와 마우스를 읽는다.
public class IsometricCameraController : MonoBehaviour
{
    const float ScrollPixelsPerNotch = 120f;

    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float zoomSpeed = 1.5f;
    [SerializeField] float minOrthographicSize = 4f;
    [SerializeField] float maxOrthographicSize = 14f;
    [SerializeField] float minWorldX = -16f;
    [SerializeField] float maxWorldX = 16f;
    [SerializeField] float minWorldZ = -16f;
    [SerializeField] float maxWorldZ = 16f;

    Camera targetCamera;
    static readonly List<RaycastResult> scrollHits = new List<RaycastResult>();

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    void Update()
    {
        Move();
        Zoom();
    }

    void Move()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (input.sqrMagnitude < 0.0001f)
        {
            return;
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude > 0.0001f)
        {
            right.Normalize();
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();
        }

        Vector3 nextPosition = transform.position;
        // 영업 배속과 Pause는 시뮬레이션 시계만 바꾸고, 카메라 이동 속도는 유지한다.
        nextPosition += (right * input.x + forward * input.y) * (moveSpeed * Time.unscaledDeltaTime);
        nextPosition.x = Mathf.Clamp(nextPosition.x, Mathf.Min(minWorldX, maxWorldX), Mathf.Max(minWorldX, maxWorldX));
        nextPosition.z = Mathf.Clamp(nextPosition.z, Mathf.Min(minWorldZ, maxWorldZ), Mathf.Max(minWorldZ, maxWorldZ));
        transform.position = nextPosition;
    }

    void Zoom()
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        // Input System 1.19의 Mouse.scroll은 픽셀 델타라 휠 한 칸이 보통 120이다.
        // 이미 1 단위로 들어오는 환경도 있어, 큰 값만 120으로 나눈다.
        // zoomSpeed는 휠 한 칸당 Orthographic Size 변화량이다.
        float notches = Mathf.Abs(scroll) > 10f ? scroll / ScrollPixelsPerNotch : scroll;
        if (PointerOverScrollRect())
        {
            return;
        }

        float lower = Mathf.Min(minOrthographicSize, maxOrthographicSize);
        float upper = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize - notches * zoomSpeed, lower, upper);
    }

    static bool PointerOverScrollRect()
    {
        if (EventSystem.current == null || Mouse.current == null)
        {
            return false;
        }

        scrollHits.Clear();
        var eventData = new PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();
        EventSystem.current.RaycastAll(eventData, scrollHits);
        for (int index = 0; index < scrollHits.Count; index++)
        {
            if (scrollHits[index].gameObject != null
                && scrollHits[index].gameObject.GetComponentInParent<ScrollRect>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
