using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 누른 채 움직이면(드래그) 꼬리 이펙트(TouchTrailRig)가 포인터를 따라가게 한다. 마우스와 멀티터치를 지원한다(손가락마다 따로).
/// 색 팔레트는 터치 이펙트 머티리얼의 6색(_CenterColor ~ _CenterColor6)을 그대로 읽어서 터치 이펙트와 항상 같은 색이 된다.
/// 누르는 순간의 이펙트는 건드리지 않는다(TouchEffectSpawner 담당).
/// </summary>
public class TouchTrailInput : MonoBehaviour
{
    private const int MouseId = -1;
    private static readonly string[] ColorProperties = { "_CenterColor", "_CenterColor2", "_CenterColor3", "_CenterColor4", "_CenterColor5", "_CenterColor6" };

    [SerializeField] private TouchTrailRig rigPrefab; // 이펙트 세트 프리팹(시작할 때 maxRigs개를 미리 만들어 돌려 쓴다)
    [SerializeField] private Material paletteMaterial; // 터치 이펙트 머티리얼
    [SerializeField] private int maxRigs = 5; // 동시에 드래그할 수 있는 손가락 수
    [SerializeField] private float dragStartPixels = 12f; // 누른 자리에서 이만큼 움직여야 드래그로 본다
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float distanceFromCamera = 10f;

    private sealed class Pointer
    {
        public Vector2 startScreen;
        public TouchTrailRig rig;
    }

    private readonly Dictionary<int, Pointer> pointers = new();
    private readonly List<TouchTrailRig> pool = new();
    private readonly List<int> endedIds = new();
    private Color[] palette;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        palette = ReadPalette();
        for (int i = 0; i < maxRigs; i++)
        {
            TouchTrailRig rig = Instantiate(rigPrefab, transform);
            rig.name = rigPrefab.name + "_" + i;
            rig.Initialize(palette);
            pool.Add(rig);
        }
    }

    private Color[] ReadPalette()
    {
        var colors = new List<Color>();
        foreach (string property in ColorProperties)
        {
            if (paletteMaterial != null && paletteMaterial.HasProperty(property))
            {
                Color c = paletteMaterial.GetColor(property);
                colors.Add(new Color(c.r, c.g, c.b, 1f)); // 알파는 터치 이펙트에서 "중심 세기"라서 색만 가져온다
            }
        }
        if (colors.Count == 0) colors.Add(Color.white);
        return colors.ToArray();
    }

    private void OnDisable()
    {
        foreach (Pointer pointer in pointers.Values) pointer.rig?.End();
        pointers.Clear();
    }

    private void Update()
    {
        if (targetCamera == null) return;

        if (!UpdateTouches()) UpdateMouse();
    }

    /// <summary>손가락별로 시작/이동/끝을 처리한다. 누르고 있는 손가락이 하나라도 있으면 true.</summary>
    private bool UpdateTouches()
    {
        Touchscreen screen = Touchscreen.current;

        endedIds.Clear();
        foreach (int id in pointers.Keys)
        {
            if (id == MouseId) continue;
            bool alive = false;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.isInProgress && touch.touchId.ReadValue() == id) { alive = true; break; }
                }
            }
            if (!alive) endedIds.Add(id);
        }
        foreach (int id in endedIds) End(id);

        if (screen == null) return false;

        bool any = false;
        foreach (var touch in screen.touches)
        {
            if (!touch.isInProgress) continue;
            any = true;
            int id = touch.touchId.ReadValue();
            Vector2 pos = touch.position.ReadValue();
            if (pointers.ContainsKey(id)) Move(id, pos);
            else Begin(id, pos);
        }

        if (any && pointers.ContainsKey(MouseId)) End(MouseId); // 터치와 같은 입력이 마우스로도 들어오는 경우를 막는다
        return any;
    }

    private void UpdateMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 pos = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame) Begin(MouseId, pos);
        else if (mouse.leftButton.isPressed && pointers.ContainsKey(MouseId)) Move(MouseId, pos);

        if (mouse.leftButton.wasReleasedThisFrame || (!mouse.leftButton.isPressed && pointers.ContainsKey(MouseId))) End(MouseId);
    }

    private void Begin(int id, Vector2 screenPos) => pointers[id] = new Pointer { startScreen = screenPos };

    private void Move(int id, Vector2 screenPos)
    {
        Pointer pointer = pointers[id];
        if (pointer.rig == null)
        {
            if ((screenPos - pointer.startScreen).sqrMagnitude < dragStartPixels * dragStartPixels) return;

            pointer.rig = Acquire();
            if (pointer.rig == null) return;
            pointer.rig.Begin(ToWorld(pointer.startScreen));
        }

        pointer.rig.MoveTo(ToWorld(screenPos));
    }

    private void End(int id)
    {
        if (!pointers.TryGetValue(id, out Pointer pointer)) return;
        pointer.rig?.End();
        pointers.Remove(id);
    }

    private TouchTrailRig Acquire()
    {
        foreach (TouchTrailRig rig in pool)
        {
            if (rig.CanUse) return rig;
        }
        return null; // 전부 사용 중이면 이번 드래그는 이펙트 없음
    }

    private Vector3 ToWorld(Vector2 screenPos) => targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
}
