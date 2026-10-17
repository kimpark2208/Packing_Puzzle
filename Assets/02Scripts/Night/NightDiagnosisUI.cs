using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 손님의 꽃 진단 화면. GameManager가 정해 둔 문제(CurrentNightCase)대로 꽃 그림에 아픔을 그리고, 노트의 체크리스트에서 아픔을 최대 2개까지 체크한 뒤 확정하면 다음 손님으로 넘어간다.
/// 꽃 그림은 줄기+이파리 위에 중심과 꽃잎 6장을 같은 위치로 겹친 것이고, 아픔은 그 부위들을 바꾸고 옮기고 칠해서 보여 준다:
/// 꽃잎 결손 = 꽃의 결손 꽃잎들(FlowerData.lossPetals)을 모두 결손 그림으로, 줄기 꺾임 = 꽃송이를 줄기 끝에서 옆으로 기울임, 꽃잎 흩어짐 = 꽃잎 1~4장만 꽃에 남고 나머지가 떨어져 화병 아래에 흩어짐, 메마름 = 메마름 셰이더(갈색 틴트 + 일렁이는 진한 갈색 가장자리), 기억 흐려짐 = 반투명.
/// 확정하면 체크한 아픔의 수리 퍼즐 패널(repairPanels)이 체크한 순서대로 열린다. 맞게 체크한 아픔은 퍼즐을 풀면 꽃이 낫고, 잘못 체크한 아픔은 풀어도 달라지지 않는다.
/// 퍼즐을 다 풀면 정답과 비교한 결과를 콘솔에 남기고 다음 손님으로 간다. 아직 퍼즐 패널이 없는 아픔은 건너뛴다. 문제가 없으면(이 씬만 실행했을 때) 에디터에 놓인 모습 그대로 체크만 할 수 있다.
/// </summary>
public class NightDiagnosisUI : MonoBehaviour
{
    [SerializeField, Tooltip("꽃 체크 화면 묶음(노트, 화병, 꽃). 에디터에서는 꺼 두고 작업해도 되고, 밤 퍼즐 씬에 들어오면 자동으로 켜진다.")] private GameObject flowerCheckArea;

    [Header("꽃 그림(부위별)")]
    [SerializeField] private Image stemLeaf;
    [SerializeField] private Image center;
    [SerializeField, Tooltip("꽃잎 6장. 맨 위가 0번이고 시계 방향.")] private Image[] petals = new Image[FlowerData.PetalPartCount];
    [SerializeField, Tooltip("꽃송이(중심과 꽃잎의 부모). 줄기 꺾임이면 이 오브젝트의 Pivot(줄기 끝에 맞춰 둔다)을 축으로 제자리에서 기울인다.")] private RectTransform head;
    [SerializeField, Tooltip("꽃잎 흩어짐: 떨어진 꽃잎이 놓일 자리(이 영역 안 무작위). 화병 앞(위)에 그려지도록 하이라키에서 화병 앞 그림 뒤에 둔다.")] private RectTransform petalDropArea;

    [Header("체크리스트와 확정")]
    [SerializeField, Tooltip("아픔 순서(FlowerData.Ailment)대로: 꽃잎 결손, 줄기 꺾임, 꽃잎 흩어짐, 메마름, 기억 흐려짐.")] private Toggle[] ailmentToggles;
    [SerializeField] private Button confirm;

    [Header("수리 퍼즐")]
    [SerializeField, Tooltip("아픔별 수리 퍼즐 패널. 체크한 아픔의 패널이 열린다(없는 아픔은 건너뛴다).")] private RepairPuzzlePanel[] repairPanels;
    [SerializeField, Tooltip("확정하면 바로 숨길 것(확정 버튼 등).")] private GameObject[] hideDuringRepair;
    [SerializeField, Tooltip("확정하면 옆으로 밀려 사라지는 노트.")] private RectTransform note;
    [SerializeField, Tooltip("노트가 밀려 나가는 만큼(왼쪽은 음수).")] private Vector2 noteExit = new(-1400f, 0f);
    [SerializeField, Tooltip("노트가 사라진 뒤 퍼즐 자리를 비우려고 옆으로 옮기는 꽃 관련 오브젝트들(화병 뒤, 꽃, 화병 앞).")] private RectTransform[] flowerObjects;
    [SerializeField, Tooltip("꽃이 옮겨 가는 만큼(왼쪽은 음수).")] private Vector2 flowerShift = new(-535f, 0f);
    [SerializeField, Min(0.01f), Tooltip("노트가 사라지는 시간과 꽃이 옮겨 가는 시간(초). 각각 이만큼 걸리고, 둘이 끝난 뒤에 퍼즐이 열린다.")] private float slideSeconds = 0.45f;
    [SerializeField, Min(0f), Tooltip("퍼즐을 푼 뒤 꽃이 나은 모습을 보여 주는 시간(초).")] private float curedDelay = 0.8f;

    [Header("아픔 모습")]
    [SerializeField, Tooltip("줄기 꺾임: 꽃송이를 기울이는 각도(양수 = 왼쪽으로).")] private float bendAngle = 70f;
    [SerializeField, Tooltip("메마름: 꽃 그림(줄기, 중심, 꽃잎)에 입히는 머티리얼(UI_WiltEdge).")] private Material wiltMaterial;
    [SerializeField, Range(0f, 1f), Tooltip("기억 흐려짐: 꽃의 투명도.")] private float fadedAlpha = 0.45f;

    private static GameManager Game => GameManager.Instance;

    private NightDiagnosisCase diagnosis;
    private Image[] flowerParts; // 꽃 그림 이미지 전부(줄기, 중심, 꽃잎)
    private int[] petalSiblings; // 꽃잎이 흩어지기 전에 꽃송이 안에서 놓였던 자리(꽃잎 흩어짐을 고칠 때 되돌린다)
    private List<int> droppedPetals; // 떨어진 꽃잎 번호들
    private Vector2 flowerOffset; // 화병과 꽃이 원래 자리에서 옮겨진 만큼(퍼즐 자리 비우기, 화면 가운데로 보여 주기)

    private void Start()
    {
        flowerCheckArea.SetActive(true);
        FindFirstObjectByType<OrderPopup>(FindObjectsInactive.Include)?.SetTabVisible(false); // 주문서는 낮 손님의 주문이라 밤에는 숨긴다
        FindFirstObjectByType<LanternLight>(FindObjectsInactive.Include)?.SetLit(true); // 밤이니 전등이 켜져 있다

        foreach (RepairPuzzlePanel panel in repairPanels) panel.gameObject.SetActive(false); // 퍼즐 패널은 에디터에서 켜 두고 작업할 수 있으니, 실행하면 닫아 둔다(확정 뒤에 열린다)

        flowerParts = petals.Append(stemLeaf).Append(center).ToArray();
        diagnosis = Game != null ? Game.CurrentNightCase : null;
        if (diagnosis != null)
        {
            ShowFlower(diagnosis);
            Debug.Log($"[NightDiagnosisUI] 손님 꽃: {diagnosis.flower.DisplayName} / 정답 아픔: {string.Join(", ", diagnosis.ailments.Select(AilmentName))}");
        }

        foreach (Toggle toggle in ailmentToggles) toggle.onValueChanged.AddListener(on => OnToggled(toggle, on));
        confirm.onClick.AddListener(OnConfirm);
        RefreshConfirm();
    }

    /// <summary>꽃 그림을 문제의 꽃으로 채우고 아픔을 그린다.</summary>
    private void ShowFlower(NightDiagnosisCase diagnosis)
    {
        FlowerData art = diagnosis.art;
        stemLeaf.sprite = art.stemLeafPartSprite;
        center.sprite = art.centerPartSprite;
        center.enabled = art.centerPartSprite != null; // 중심 그림이 없는 꽃은 빈 Image(흰 사각형)가 보이지 않게 끈다
        for (int i = 0; i < petals.Length; i++) petals[i].sprite = art.petalPartSprites[i];

        Color tint = Color.white;
        if (diagnosis.ailments.Contains(FlowerData.Ailment.FadedMemory)) tint.a = fadedAlpha;
        bool wilt = diagnosis.ailments.Contains(FlowerData.Ailment.Wilt);
        foreach (Image part in flowerParts)
        {
            part.color = tint;
            if (wilt) part.material = wiltMaterial;
        }

        if (diagnosis.ailments.Contains(FlowerData.Ailment.PetalLoss))
        {
            foreach (FlowerData.LossPetal loss in art.lossPetals) petals[loss.petalIndex].sprite = loss.sprite; // 결손 꽃잎은 한 장이 아니다: 정해진 꽃잎이 모두 결손된다
        }
        if (diagnosis.ailments.Contains(FlowerData.Ailment.StemBend)) BendHead();
        if (diagnosis.ailments.Contains(FlowerData.Ailment.PetalScatter)) DropPetals(diagnosis.keptPetals);
    }

#if UNITY_EDITOR
    /// <summary>에디터 창(Tools/Night/Flower Preview)용: Play 없이, 막 불러온 씬의 꽃 그림에 문제의 꽃과 아픔을 그려 본다. 그림이 비어 있는 부위는 숨긴다.</summary>
    public void EditorPreview(NightDiagnosisCase previewCase)
    {
        flowerParts = petals.Append(stemLeaf).Append(center).ToArray();
        ShowFlower(previewCase);
        foreach (Image part in flowerParts) part.enabled = part.sprite != null;
    }
#endif

    /// <summary>꽃송이를 줄기 끝에서 옆으로 기울인다(에디터에 놓인 꽃송이의 Pivot을 축으로 제자리에서 돌린다. 인스펙터에서 Rotation을 주는 것과 같다).</summary>
    private void BendHead() => head.localRotation = Quaternion.Euler(0f, 0f, bendAngle);

    /// <summary>kept에 없는 꽃잎들이 떨어져 petalDropArea 안 무작위 자리에 무작위 각도로 놓인다(kept의 꽃잎은 꽃에 그대로 남는다).</summary>
    private void DropPetals(List<int> kept)
    {
        petalSiblings = petals.Select(p => p.transform.GetSiblingIndex()).ToArray();
        droppedPetals = Enumerable.Range(0, petals.Length).Where(i => !kept.Contains(i)).ToList();
        Rect area = petalDropArea.rect;
        foreach (int index in droppedPetals)
        {
            Image petal = petals[index];
            var rect = (RectTransform)petal.transform;
            Vector2 artCenter = SpriteBounds.ArtCenter(petal.sprite); // 꽃잎 그림 캔버스 안에서 이 꽃잎 그림의 중심 위치

            Vector2 size = rect.rect.size;
            rect.SetParent(petalDropArea, true);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector3 target = petalDropArea.TransformPoint(new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)));
            rect.position += target - rect.TransformPoint(artCenter);
        }
    }

    private int CheckedCount => ailmentToggles.Count(t => t.isOn);

    /// <summary>아픔은 최대 2개까지만 체크할 수 있다. 세 번째를 체크하면 그 체크는 취소된다.</summary>
    private void OnToggled(Toggle toggle, bool on)
    {
        if (on && CheckedCount > NightDiagnosisCase.MaxAilments) toggle.SetIsOnWithoutNotify(false);
        RefreshConfirm();
    }

    /// <summary>하나도 체크하지 않았으면 확정할 수 없다.</summary>
    private void RefreshConfirm() => confirm.interactable = CheckedCount >= 1;

    private void OnConfirm()
    {
        var picked = new List<FlowerData.Ailment>();
        for (int i = 0; i < ailmentToggles.Length; i++)
        {
            if (ailmentToggles[i].isOn) picked.Add((FlowerData.Ailment)i);
        }
        confirm.interactable = false; // 확정한 뒤에는 다시 누르지 못한다
        StartCoroutine(RepairRoutine(picked));
    }

    /// <summary>체크한 아픔의 퍼즐을 차례로 푼다. 다 풀면 결과를 남기고 다음 손님으로 간다.</summary>
    private IEnumerator RepairRoutine(List<FlowerData.Ailment> picked)
    {
        foreach (GameObject hidden in hideDuringRepair) hidden.SetActive(false);

        // 노트가 왼쪽으로 사라진 다음, 퍼즐마다 꽃이 왼쪽으로 옮겨 가거나(물 주기 등) 꽃이 숨겨진(기억 되짚기 등) 뒤에 퍼즐이 열린다
        yield return Slide(new[] { note }, noteExit);
        note.gameObject.SetActive(false);

        foreach (FlowerData.Ailment ailment in picked)
        {
            RepairPuzzlePanel panel = repairPanels.FirstOrDefault(p => p != null && p.Ailment == ailment);
            if (panel == null)
            {
                Debug.Log($"[NightDiagnosisUI] {AilmentName(ailment)} 수리 퍼즐은 아직 없어 건너뜁니다.");
                yield return ShowFlowerCentered(); // 고치지 못해도 꽃을 화면 가운데에서 잠시 보여 준다
                continue;
            }

            if (panel.HidesFlower) SetFlowerVisible(false);
            else yield return MoveFlowerTo(flowerShift); // 가운데에 있던 꽃이 퍼즐 자리를 비우려고 옆으로 옮겨 간다

            bool solved = false;
            panel.Open(diagnosis, () => solved = true);
            yield return new WaitUntil(() => solved);

            if (diagnosis != null && diagnosis.ailments.Contains(ailment)) Cure(ailment); // 잘못 체크한 아픔은 풀어도 달라지지 않는다
            yield return ShowFlowerCentered(); // 숨겼던 꽃을 다시 보여 준다(나은 모습, 고쳐지지 않는 경우에도 화면 가운데에서)
        }

        if (Game != null) Game.FinishNightDiagnosis(picked);
        else Debug.Log($"[NightDiagnosisUI] 체크: {string.Join(", ", picked.Select(AilmentName))} (게임 매니저가 없어 다음 손님으로 넘어가지 않습니다)");
    }

    /// <summary>화병과 꽃을 화면 가운데에서 curedDelay 동안 보여 준다(보이던 꽃은 가운데로 미끄러져 오고, 숨겨 둔 꽃은 가운데에 바로 나타난다).</summary>
    private IEnumerator ShowFlowerCentered()
    {
        Vector2 center = flowerOffset + new Vector2(-FlowerScreenX(), 0f);
        if (flowerObjects[0].gameObject.activeSelf)
        {
            yield return MoveFlowerTo(center);
        }
        else
        {
            foreach (RectTransform flowerObject in flowerObjects) flowerObject.anchoredPosition += center - flowerOffset;
            flowerOffset = center;
            SetFlowerVisible(true);
        }
        yield return new WaitForSecondsRealtime(curedDelay);
    }

    /// <summary>화병(flowerObjects의 첫 번째)의 가운데가 화면 가운데에서 얼마나 떨어져 있는지(캔버스 x, 오른쪽이 양수).</summary>
    private float FlowerScreenX()
    {
        RectTransform pot = flowerObjects[0];
        var canvas = (RectTransform)pot.GetComponentInParent<Canvas>().rootCanvas.transform;
        return canvas.InverseTransformPoint(pot.TransformPoint(pot.rect.center)).x;
    }

    /// <summary>꽃 관련 오브젝트들을 원래 자리에서 offset만큼 옮긴 자리로 부드럽게 보낸다(지금 어디에 있든, 이미 그 자리면 가만히 둔다).</summary>
    private IEnumerator MoveFlowerTo(Vector2 offset)
    {
        Vector2 delta = offset - flowerOffset;
        if (delta == Vector2.zero) yield break;

        flowerOffset = offset;
        yield return Slide(flowerObjects, delta);
    }

    /// <summary>오브젝트들을 delta만큼 부드럽게 옮긴다(slideSeconds 동안).</summary>
    private IEnumerator Slide(RectTransform[] objects, Vector2 delta)
    {
        Vector2[] from = objects.Select(o => o.anchoredPosition).ToArray();
        for (float time = 0f; time < slideSeconds; time += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / slideSeconds);
            for (int i = 0; i < objects.Length; i++) objects[i].anchoredPosition = from[i] + delta * t;
            yield return null;
        }
        for (int i = 0; i < objects.Length; i++) objects[i].anchoredPosition = from[i] + delta;
    }

    /// <summary>화병과 꽃(옮길 수 있는 꽃 관련 오브젝트들)을 보이거나 숨긴다.</summary>
    private void SetFlowerVisible(bool visible)
    {
        foreach (RectTransform flowerObject in flowerObjects) flowerObject.gameObject.SetActive(visible);
    }

    /// <summary>수리 퍼즐을 푼 아픔의 모습을 꽃 그림에서 없앤다(아직 퍼즐이 없는 아픔은 다루지 않는다).</summary>
    private void Cure(FlowerData.Ailment ailment)
    {
        if (ailment == FlowerData.Ailment.Wilt)
        {
            foreach (Image part in flowerParts) part.material = null; // 메마름 셰이더를 걷어 낸다
        }
        else if (ailment == FlowerData.Ailment.FadedMemory)
        {
            foreach (Image part in flowerParts) part.color = new Color(part.color.r, part.color.g, part.color.b, 1f); // 다시 또렷해진다
        }
        else if (ailment == FlowerData.Ailment.PetalScatter)
        {
            GatherPetals();
        }
        else if (ailment == FlowerData.Ailment.StemBend)
        {
            StraightenHead();
        }
        else if (ailment == FlowerData.Ailment.PetalLoss)
        {
            foreach (FlowerData.LossPetal loss in diagnosis.art.lossPetals) petals[loss.petalIndex].sprite = diagnosis.art.petalPartSprites[loss.petalIndex]; // 결손됐던 꽃잎이 온전한 그림으로 돌아온다
        }
    }

    /// <summary>꺾였던 꽃송이를 다시 똑바로 돌려 꽃이 펴진다.</summary>
    private void StraightenHead() => head.localRotation = Quaternion.identity;

    /// <summary>떨어진 꽃잎을 꽃송이 안 제자리로 되돌린다(꽃잎 그림은 꽃송이를 가득 채우게 놓여 있다).</summary>
    private void GatherPetals()
    {
        foreach (int index in droppedPetals)
        {
            var rect = (RectTransform)petals[index].transform;
            rect.SetParent(head, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one; // 흩어질 때 부모가 바뀌며 꽃 배율이 꽃잎 배율로 옮겨 가 있다
        }
        foreach (int i in droppedPetals.OrderBy(k => petalSiblings[k])) petals[i].transform.SetSiblingIndex(petalSiblings[i]); // 낮은 자리부터 차례로 넣어야 나머지가 밀리지 않는다
    }

    public static string AilmentName(FlowerData.Ailment ailment) => ailment switch
    {
        FlowerData.Ailment.PetalLoss => "꽃잎 결손",
        FlowerData.Ailment.StemBend => "줄기 꺾임",
        FlowerData.Ailment.PetalScatter => "꽃잎 흩어짐",
        FlowerData.Ailment.Wilt => "메마름",
        _ => "기억 흐려짐",
    };
}
