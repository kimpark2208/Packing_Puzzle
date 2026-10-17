using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 손님의 꽃 진단 화면. GameManager가 정해 둔 문제(CurrentNightCase)대로 꽃 그림에 아픔을 그리고, 노트의 체크리스트에서 아픔을 최대 2개까지 체크한 뒤 확정하면 다음 손님으로 넘어간다.
/// 꽃 그림은 줄기+이파리 위에 중심과 꽃잎 6장을 같은 위치로 겹친 것이고, 아픔은 그 부위들을 바꾸고 옮기고 칠해서 보여 준다:
/// 꽃잎 결손 = 꽃의 결손 꽃잎들(FlowerData.lossPetals)을 모두 결손 그림으로, 줄기 꺾임 = 꽃송이를 줄기 끝에서 옆으로 기울임, 꽃잎 흩어짐 = 꽃잎이 전부 떨어져 화병 아래에 흩어짐, 메마름 = 갈색, 기억 흐려짐 = 반투명.
/// 아직 수리 퍼즐은 없다(체크하고 확정하면 정답과 비교한 결과만 콘솔에 남기고 다음 손님으로 간다). 문제가 없으면(이 씬만 실행했을 때) 에디터에 놓인 모습 그대로 체크만 할 수 있다.
/// </summary>
public class NightDiagnosisUI : MonoBehaviour
{
    [Header("꽃 그림(부위별)")]
    [SerializeField] private Image stemLeaf;
    [SerializeField] private Image center;
    [SerializeField, Tooltip("꽃잎 6장. 맨 위가 0번이고 시계 방향.")] private Image[] petals = new Image[FlowerData.PetalPartCount];
    [SerializeField, Tooltip("꽃송이(중심과 꽃잎의 부모). 줄기 꺾임이면 아래 값대로 Pivot과 위치를 옮기고 기울인다.")] private RectTransform head;
    [SerializeField, Tooltip("꽃잎 흩어짐: 떨어진 꽃잎이 놓일 자리(이 영역 안 무작위). 화병 앞(위)에 그려지도록 하이라키에서 화병 앞 그림 뒤에 둔다.")] private RectTransform petalDropArea;

    [Header("체크리스트와 확정")]
    [SerializeField, Tooltip("아픔 순서(FlowerData.Ailment)대로: 꽃잎 결손, 줄기 꺾임, 꽃잎 흩어짐, 메마름, 기억 흐려짐.")] private Toggle[] ailmentToggles;
    [SerializeField] private Button confirm;

    [Header("아픔 모습")]
    [SerializeField, Tooltip("줄기 꺾임: 꽃송이를 기울이는 각도(양수 = 왼쪽으로).")] private float bendAngle = 70f;
    [SerializeField, Tooltip("줄기 꺾임: 기울일 때 꽃송이의 Pivot(꽃송이 그림 안의 위치. (0.5, 0) = 아래 가운데).")] private Vector2 bendPivot = new(0.5f, 0f);
    [SerializeField, Tooltip("줄기 꺾임: 기울일 때 꽃송이 Pivot의 위치(꽃 그림 가운데 기준, 꽃 그림 캔버스 단위).")] private Vector2 bendPosition = new(0f, 200f);
    [SerializeField, Tooltip("메마름: 꽃 전체에 입히는 갈색.")] private Color wiltColor = new(0.62f, 0.47f, 0.34f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("기억 흐려짐: 꽃의 투명도.")] private float fadedAlpha = 0.45f;

    private static GameManager Game => GameManager.Instance;

    private void Start()
    {
        NightDiagnosisCase diagnosis = Game != null ? Game.CurrentNightCase : null;
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
        for (int i = 0; i < petals.Length; i++) petals[i].sprite = art.petalPartSprites[i];

        Color tint = diagnosis.ailments.Contains(FlowerData.Ailment.Wilt) ? wiltColor : Color.white;
        if (diagnosis.ailments.Contains(FlowerData.Ailment.FadedMemory)) tint.a = fadedAlpha;
        foreach (Image part in petals.Append(stemLeaf).Append(center)) part.color = tint;

        if (diagnosis.ailments.Contains(FlowerData.Ailment.PetalLoss))
        {
            foreach (FlowerData.LossPetal loss in art.lossPetals) petals[loss.petalIndex].sprite = loss.sprite; // 결손 꽃잎은 한 장이 아니다: 정해진 꽃잎이 모두 결손된다
        }
        if (diagnosis.ailments.Contains(FlowerData.Ailment.StemBend)) BendHead();
        if (diagnosis.ailments.Contains(FlowerData.Ailment.PetalScatter)) DropPetals();
    }

    /// <summary>꽃송이를 줄기 끝에서 옆으로 기울인다.</summary>
    private void BendHead()
    {
        head.anchorMin = head.anchorMax = new Vector2(0.5f, 0.5f);
        head.pivot = bendPivot;
        head.anchoredPosition = bendPosition;
        head.localRotation = Quaternion.Euler(0f, 0f, bendAngle);
    }

    /// <summary>꽃잎이 전부 떨어져 petalDropArea 안 무작위 자리에 무작위 각도로 놓인다(꽃 중심만 줄기 끝에 남는다).</summary>
    private void DropPetals()
    {
        Rect area = petalDropArea.rect;
        foreach (Image petal in petals)
        {
            var rect = (RectTransform)petal.transform;
            Sprite sprite = petal.sprite;
            Vector2 artCenter = sprite != null ? (Vector2)sprite.bounds.center * sprite.pixelsPerUnit : Vector2.zero; // 꽃잎 그림 캔버스 안에서 이 꽃잎 그림의 중심 위치

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
        if (Game != null) Game.FinishNightDiagnosis(picked);
        else Debug.Log($"[NightDiagnosisUI] 체크: {string.Join(", ", picked.Select(AilmentName))} (게임 매니저가 없어 다음 손님으로 넘어가지 않습니다)");
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
