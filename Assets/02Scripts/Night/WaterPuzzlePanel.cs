using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메마름 수리 퍼즐: 물 주기. 버튼을 누르고 있으면 물통의 물이 차오르고, 표시된 구간 안에서 손을 떼면 성공이다.
/// 구간 전에 떼거나 가득 차 넘치면 물이 비워지고 다시 채운다. 표시 구간은 퍼즐을 시작할 때마다 무작위로 정해진다.
/// </summary>
public class WaterPuzzlePanel : RepairPuzzlePanel
{
    [SerializeField, Tooltip("차오르는 물(Image Type = Filled, Vertical, Bottom).")] private Image water;
    [SerializeField, Tooltip("손을 떼야 하는 표시 구간. 물(Water)의 자식이고 가로로 가득 차게(Anchor X 0~1) 둔다. 세로 위치는 코드가 정한다.")] private RectTransform zone;
    [SerializeField] private HoldButton holdButton;
    [SerializeField, Tooltip("메시지 글자: 시작할 때는 안내 문구를, 성공하면 성공 문구를 보여 준다.")] private TMP_Text message;
    [SerializeField, Tooltip("시작할 때 보여 주는 안내 문구.")] private string startMessage = "꾹 눌러 물을 채우고, 표시 구간에서 손을 떼요";
    [SerializeField, Tooltip("성공했을 때 보여 주는 문구.")] private string solvedMessage = "성공!";

    [Header("조절")]
    [SerializeField, Min(0.5f), Tooltip("물통이 비어 있는 상태에서 가득 찰 때까지 걸리는 시간(초).")] private float fillSeconds = 3f;
    [SerializeField, Range(0.05f, 0.4f), Tooltip("표시 구간의 높이(물통 전체를 1로 본 비율).")] private float zoneHeight = 0.16f;
    [SerializeField, Tooltip("표시 구간의 가운데가 놓일 수 있는 범위(물통 전체를 1로 본 비율, 아래가 0).")] private Vector2 zoneCenterRange = new(0.4f, 0.85f);
    [SerializeField, Min(0f), Tooltip("성공한 뒤 패널이 닫히기까지 잠시 보여 주는 시간(초).")] private float solvedDelay = 0.8f;

    private float level;     // 지금 물의 양(0~1)
    private float zoneMin;
    private float zoneMax;
    private bool finished;

    protected override void Begin()
    {
        finished = false;
        level = 0f;
        water.fillAmount = 0f;
        message.text = startMessage;

        float center = Random.Range(zoneCenterRange.x, zoneCenterRange.y);
        zoneMin = Mathf.Clamp01(center - zoneHeight * 0.5f);
        zoneMax = Mathf.Clamp01(center + zoneHeight * 0.5f);
        zone.anchorMin = new Vector2(0f, zoneMin);
        zone.anchorMax = new Vector2(1f, zoneMax);
        zone.offsetMin = zone.offsetMax = Vector2.zero;

        holdButton.Released -= OnReleased;
        holdButton.Released += OnReleased;
    }

    private void OnDisable()
    {
        holdButton.Released -= OnReleased;
    }

    private void Update()
    {
        if (finished || !holdButton.IsHeld) return;

        level += Time.unscaledDeltaTime / fillSeconds;
        if (level >= 1f) level = 0f; // 가득 차 넘치면 비워지고 다시 채운다
        water.fillAmount = level;
    }

    /// <summary>손을 뗐다: 표시 구간 안이면 성공, 아니면 물을 비우고 다시 채운다.</summary>
    private void OnReleased()
    {
        if (finished) return;

        if (level >= zoneMin && level <= zoneMax)
        {
            finished = true;
            message.text = solvedMessage;
            StartCoroutine(SolveAfterDelay());
            return;
        }

        level = 0f;
        water.fillAmount = 0f;
    }

    private IEnumerator SolveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(solvedDelay);
        Solve();
    }
}
