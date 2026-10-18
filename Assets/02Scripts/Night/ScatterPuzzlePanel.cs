using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 꽃잎 흩어짐 수리 퍼즐: 꽃잎 붙이기. 줄기 위 꽃에 꽃잎 자리(어두운 윤곽)가 있고, 떨어진 꽃잎이 양옆에 흩어져 있다(진단에서 꽃에 남은 꽃잎 1~4장은 처음부터 붙어 있다).
/// 꽃잎을 끌어다 맞는 자리 근처에 놓으면 붙고, 다른 꽃잎의 자리 근처에 놓으면 처음 자리로 튕겨 돌아간다(어느 자리 근처도 아니면 놓은 곳에 그대로 있다). 끌고 있는 꽃잎이 어떤 자리에 가까워지면 그 자리가 흰 테두리로 깜빡인다(낮 퍼즐의 칸과 같다). 꽃잎 6장이 모두 붙으면 성공이다.
/// 이 퍼즐은 화병과 꽃을 숨기고, 패널 안에 줄기가 있는 꽃을 따로 보여 준다(그림은 진단 꽃의 그림을 쓴다).
/// </summary>
public class ScatterPuzzlePanel : RepairPuzzlePanel
{
    private const float PetalHitAlpha = 0.1f; // 꽃잎 그림의 알파가 이보다 커야 그 꽃잎이 잡힌다
    private const float OutlinePadScale = 1.4f; // PetalOutline 셰이더가 정해 둔 배율(셰이더 안의 _PadScale과 같아야 한다)

    [Header("꽃(패널 안에 보이는 꽃)")]
    [SerializeField] private Image stemLeaf;
    [SerializeField] private Image center;
    [SerializeField, Tooltip("꽃잎 자리 6개(꽃잎과 같은 순서). 꽃잎 그림을 어두운 윤곽으로 보여 준다. 꽃 중심과 같은 부모 안에 같은 크기로 둔다.")] private Image[] slots = new Image[FlowerData.PetalPartCount];

    [SerializeField, Tooltip("자리 테두리 이미지를 담는 층. 떨어진 꽃잎(LoosePetals)보다 위에 그려지도록 패널 안에서 맨 아래(맨 앞)에 두고, 패널을 가득 채우게(Anchor 0~1) 둔다.")] private RectTransform outlineLayer;
    [SerializeField, Tooltip("꽃잎 자리마다 하나씩 둔 테두리 이미지(PetalOutline 머티리얼, 꽃잎 자리와 같은 순서). 평소에는 꺼 둔다. 위치·그림·크기는 코드가 자리에 맞춘다.")] private Image[] outlines = new Image[FlowerData.PetalPartCount];

    [Header("떨어진 꽃잎")]
    [SerializeField, Tooltip("끌어다 놓는 꽃잎 6개(꽃잎과 같은 순서). 꽃보다 위에 그려지는 같은 부모 안에 둔다. 크기와 배율은 코드가 꽃잎 자리에 맞춘다.")] private PetalDrag[] loosePetals = new PetalDrag[FlowerData.PetalPartCount];
    [SerializeField, Tooltip("떨어진 꽃잎이 놓일 영역들. 꽃잎을 영역마다 번갈아 가며 영역 안 무작위 자리에 놓는다.")] private RectTransform[] looseAreas;

    [Header("표시")]
    [SerializeField] private TMP_Text message;
    [SerializeField] private string startMessage = "떨어진 꽃잎을 제자리에 붙여요";
    [SerializeField] private string solvedMessage = "꽃잎을 모두 붙였어요!";
    [SerializeField, Tooltip("꽃잎 자리(윤곽)의 색.")] private Color slotColor = new(0f, 0f, 0f, 0.4f);

    [Header("조절")]
    [SerializeField, Min(0f), Tooltip("꽃잎을 끌어 어떤 자리에 가까이 가져갔을 때, 그 자리의 흰 테두리가 깜빡이는 속도.")] private float pulseSpeed = 8f;
    [SerializeField, Min(1f), Tooltip("꽃잎을 놓았을 때, 꽃잎 그림 중심이 어떤 자리의 그림 중심에서 이 거리(화면 단위) 안이면 그 자리에 놓은 것으로 본다.")] private float snapDistance = 110f;
    [SerializeField, Range(0f, 90f), Tooltip("떨어진 꽃잎이 처음 놓일 때 무작위로 기울어지는 최대 각도.")] private float maxTilt = 35f;
    [SerializeField, Min(0.01f), Tooltip("붙거나 튕겨 돌아가는 데 걸리는 시간(초).")] private float moveSeconds = 0.18f;
    [SerializeField, Min(0f), Tooltip("다 붙인 뒤 패널이 닫히기까지 잠시 보여 주는 시간(초).")] private float solvedDelay = 0.8f;

    private const string VerticalHeadName = "VerticalHead";
    private Transform verticalHead;             // 세로형 꽃송이 상자와 그 안의 중심·꽃잎 자리(이름으로 찾는다)
    private Image verticalCenter;
    private Image[] verticalSlots;
    private bool tallLayout;                    // 지금 꽃이 세로형 상자를 쓰는가
    private Image Center => tallLayout ? verticalCenter : center;         // 지금 꽃에 쓰는 중심·꽃잎 자리
    private Image[] Slots => tallLayout ? verticalSlots : slots;
    private Vector3[] homePositions;
    private Quaternion[] homeRotations;
    private readonly bool[] attached = new bool[FlowerData.PetalPartCount]; // 처음부터 꽃에 붙어 있는 꽃잎(진단에서 떨어지지 않은 꽃잎)
    private Material silhouetteMaterial;                                     // 꽃잎 자리의 윤곽 머티리얼(붙어 있는 꽃잎 자리는 꽃잎 그림 그대로 보이도록 잠시 벗긴다)
    private int placed;

    public override bool HidesFlower => true;

    protected override void Begin()
    {
        StopAllCoroutines();
        ApplyPartLayout(Case != null ? Case.art : null);
        if (Case != null) ApplyFlowerArt(Case.art);
        message.text = startMessage;
        silhouetteMaterial ??= Slots[0].material;
        Array.Clear(attached, 0, attached.Length);
        if (Case != null) foreach (int kept in Case.keptPetals) attached[kept] = true;
        placed = attached.Count(a => a);

        for (int i = 0; i < loosePetals.Length; i++)
        {
            PetalDrag petal = loosePetals[i];
            Slots[i].color = attached[i] ? Color.white : slotColor;
            Slots[i].material = attached[i] ? null : silhouetteMaterial;
            Slots[i].gameObject.SetActive(true);
            petal.gameObject.SetActive(!attached[i]);
            LayoutOutline(i);
            ShowOutline(i, false);

            petal.Image.color = Color.white;
            petal.Image.raycastTarget = true;
            petal.Image.alphaHitTestMinimumThreshold = PetalHitAlpha; // 투명한 곳은 잡히지 않는다
            petal.Locked = false;
            petal.Moved -= OnMoved;
            petal.Moved += OnMoved;
            petal.Released -= OnReleased;
            petal.Released += OnReleased;
        }
        ScatterPetals();
    }

    private void Update()
    {
        float alpha = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f);
        foreach (Image outline in outlines)
        {
            if (!outline.gameObject.activeSelf) continue;

            Color color = outline.color;
            color.a = alpha;
            outline.color = color;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>꽃의 배치를 맞춘다. 둥근형 꽃송이 상자(RoundHead, 인스펙터에 연결된 중심·꽃잎 자리의 부모)와 세로형 꽃송이 상자(VerticalHead, 이름으로 찾는다)가 Flower 안에 따로 있고,
    /// 맞는 쪽만 켠다. 위치·기울기·배율은 에디터에서 정해 둔 그대로 쓴다.</summary>
    private void ApplyPartLayout(FlowerData art)
    {
        var roundHead = center.transform.parent;
        var flower = (RectTransform)roundHead.parent;
        if (verticalHead == null)
        {
            Transform found = flower.Find(VerticalHeadName); // 안에 Center와 Slot_1~6이 둥근형과 같은 순서로 있다
            if (found != null)
            {
                verticalHead = found;
                verticalCenter = found.Find("Center").GetComponent<Image>();
                verticalSlots = PartFinder.Children<Image>(found, "Slot");
            }
        }

        tallLayout = art != null && art.partLayout == FlowerData.PartLayout.Tall && verticalHead != null;
        roundHead.gameObject.SetActive(!tallLayout);
        if (verticalHead != null) verticalHead.gameObject.SetActive(tallLayout);
    }

    /// <summary>패널의 꽃을 진단 꽃의 그림으로 채운다(줄기, 중심, 꽃잎 자리, 떨어진 꽃잎).</summary>
    private void ApplyFlowerArt(FlowerData art)
    {
        stemLeaf.sprite = art.stemLeafPartSprite;
        Center.sprite = art.centerPartSprite;
        Center.enabled = art.centerPartSprite != null; // 중심 그림이 없는 꽃은 중심을 그리지 않는다
        for (int i = 0; i < Slots.Length; i++) Slots[i].sprite = loosePetals[i].Image.sprite = art.petalPartSprites[i];
    }

    /// <summary>인스펙터에서 영역이나 기울기를 바꾼 뒤, 이 항목(컴포넌트 우클릭 메뉴)을 누르면 플레이하지 않고도 떨어진 꽃잎의 모습을 미리 볼 수 있다.</summary>
    [ContextMenu("떨어진 꽃잎 배치(미리 보기)")]
    private void PreviewScatter()
    {
        for (int i = 0; i < Slots.Length; i++) Slots[i].color = slotColor;
        ScatterPetals();
    }

    /// <summary>떨어진 꽃잎(붙어 있지 않은 꽃잎)을 꽃잎 자리와 같은 크기로 맞추고, 영역들에 번갈아 가며 무작위 자리·각도로 흩어 놓는다(그림 중심이 고른 자리에 오게).</summary>
    private void ScatterPetals()
    {
        homePositions = new Vector3[loosePetals.Length];
        homeRotations = new Quaternion[loosePetals.Length];

        int[] order = Enumerable.Range(0, loosePetals.Length).Where(i => !attached[i]).OrderBy(_ => Random.value).ToArray();
        for (int k = 0; k < order.Length; k++)
        {
            int i = order[k];
            RectTransform rect = loosePetals[i].Rect;
            RectTransform slot = Slots[i].rectTransform;
            RectTransform area = looseAreas[k % looseAreas.Length];

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = slot.rect.size;
            rect.localScale = Vector3.one * (slot.lossyScale.x / rect.parent.lossyScale.x);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-maxTilt, maxTilt));

            Rect bounds = area.rect;
            Vector3 target = area.TransformPoint(new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax)));
            rect.position += target - rect.TransformPoint(ArtCenter(loosePetals[i].Image));

            homePositions[i] = rect.position;
            homeRotations[i] = rect.rotation;
        }
    }

    /// <summary>꽃잎을 끌고 있다: 가까이 간 자리(놓으면 반응하는 자리)가 있으면 그 자리의 테두리가 깜빡인다.</summary>
    private void OnMoved(PetalDrag drag)
    {
        int nearest = NearestSlot(drag);
        for (int j = 0; j < Slots.Length; j++) ShowOutline(j, j == nearest);
    }

    /// <summary>꽃잎을 놓았다: 가장 가까운 자리가 맞는 자리면 붙고, 다른 자리면 처음 자리로 튕겨 돌아간다.</summary>
    private void OnReleased(PetalDrag drag)
    {
        for (int j = 0; j < Slots.Length; j++) ShowOutline(j, false);

        int index = Array.IndexOf(loosePetals, drag);
        int nearest = NearestSlot(drag);
        if (nearest < 0) return;

        drag.Locked = true;
        StartCoroutine(nearest == index ? Attach(drag, index) : Bounce(drag, index));
    }

    /// <summary>꽃잎의 그림 중심이 snapDistance 안에 들어온 자리 중 가장 가까운 자리(아직 안 붙은 자리만). 없으면 -1.</summary>
    private int NearestSlot(PetalDrag drag)
    {
        Vector2 here = PanelPoint(drag.Rect, ArtCenter(drag.Image));
        int nearest = -1;
        float best = snapDistance;
        for (int j = 0; j < Slots.Length; j++)
        {
            if (!Slots[j].gameObject.activeSelf || attached[j]) continue;

            float distance = Vector2.Distance(here, PanelPoint(Slots[j].rectTransform, ArtCenter(Slots[j])));
            if (distance >= best) continue;

            best = distance;
            nearest = j;
        }
        return nearest;
    }

    private void ShowOutline(int index, bool on)
    {
        outlines[index].gameObject.SetActive(on);
    }

    /// <summary>자리의 테두리 이미지를 자리와 같은 그림으로, 자리보다 OutlinePadScale배 크게 자리 위에 겹쳐 놓는다. 테두리 층(outlineLayer)이 꽃잎들보다 위에 그려지므로, 붙은 꽃잎이나 끌고 있는 꽃잎에 가려지지 않는다.</summary>
    private void LayoutOutline(int index)
    {
        RectTransform slot = Slots[index].rectTransform;
        RectTransform rect = outlines[index].rectTransform;
        rect.SetParent(outlineLayer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = slot.rect.size * OutlinePadScale;
        rect.localScale = Vector3.one * (slot.lossyScale.x / outlineLayer.lossyScale.x);
        rect.SetPositionAndRotation(slot.position, slot.rotation);
        outlines[index].sprite = Slots[index].sprite;
    }

    private IEnumerator Attach(PetalDrag drag, int index)
    {
        RectTransform slot = Slots[index].rectTransform;
        yield return MoveTo(drag.Rect, slot.position, slot.rotation);
        Slots[index].gameObject.SetActive(false);
        drag.Image.raycastTarget = false;

        placed++;
        if (placed < loosePetals.Length) yield break;

        message.text = solvedMessage;
        yield return new WaitForSecondsRealtime(solvedDelay);
        Solve();
    }

    private IEnumerator Bounce(PetalDrag drag, int index)
    {
        yield return MoveTo(drag.Rect, homePositions[index], homeRotations[index]);
        drag.Locked = false;
    }

    private IEnumerator MoveTo(RectTransform rect, Vector3 position, Quaternion rotation)
    {
        Vector3 fromPosition = rect.position;
        Quaternion fromRotation = rect.rotation;
        for (float time = 0f; time < moveSeconds; time += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / moveSeconds);
            rect.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, t), Quaternion.Slerp(fromRotation, rotation, t));
            yield return null;
        }
        rect.SetPositionAndRotation(position, rotation);
    }

    /// <summary>꽃잎 이미지 안에서 그림 중심의 위치(이미지 중심 기준, 이미지의 로컬 단위).</summary>
    private static Vector2 ArtCenter(Image image)
    {
        Sprite sprite = image.sprite;
        return Vector2.Scale(SpriteBounds.ArtCenter(sprite), image.rectTransform.rect.size / sprite.rect.size);
    }

    /// <summary>이미지 안의 점(로컬)을 패널 기준 위치로 바꾼다. 거리를 쟀을 때 화면 단위가 된다.</summary>
    private Vector2 PanelPoint(RectTransform rect, Vector2 local) => transform.InverseTransformPoint(rect.TransformPoint(local));
}
