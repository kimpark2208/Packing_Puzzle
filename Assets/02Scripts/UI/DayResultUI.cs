using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루 결산 화면(03Result)의 장부(영수증). 오늘 번 돈, 쓴 돈, 순수입과 평점(별), 평점 보상(부적, 재화 배수)을 보여 주고,
/// "다음 날로 넘어가기"를 누르면 보상을 지급하고 다음 날 낮이 시작된다.
/// 이미 받은 돈은 퍼즐을 끝낼 때마다 들어와 있으므로, 여기서는 최종 수령액(순수입 × 배수)에서 순수입을 뺀 보너스만 더 지급한다.
/// 장부 오브젝트는 씬에 미리 배치되어 있고 이 스크립트는 글자와 그림만 채운다.
/// </summary>
public class DayResultUI : MonoBehaviour
{
    [Header("장부 머리")]
    [SerializeField] private TMP_Text orderNumberText; // "No. 0001"
    [SerializeField] private TMP_Text titleText;       // "1일차 정산"

    [Header("평점 (별 5개)")]
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite starFull;
    [SerializeField] private Sprite starEmpty;

    [Header("평점 보상 (받는 것이 없으면 숨긴다)")]
    [SerializeField] private GameObject charmReward;
    [SerializeField] private TMP_Text charmRewardText;
    [SerializeField] private GameObject moneyReward;
    [SerializeField] private TMP_Text moneyRewardText;

    [Header("금액")]
    [SerializeField] private TMP_Text earnedText;
    [SerializeField] private TMP_Text spentText;
    [SerializeField] private TMP_Text netText;
    [SerializeField] private TMP_Text finalText;

    [SerializeField] private Button goNextButton;

    private int charmsToGrant;
    private int bonusMoney;
    private bool claimed;

    private void Start()
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        int day = wallet != null ? wallet.CurrentDay : 1;
        int earned = wallet != null ? wallet.TodayEarned : 0;
        int spent = wallet != null ? wallet.TodaySpent : 0;
        int starCount = DayRating.Stars(wallet != null ? wallet.TodayAverageScore : 0f);
        float multiplier = DayRating.MoneyMultiplier(starCount);

        int net = earned - spent;
        int final = net > 0 ? Mathf.RoundToInt(net * multiplier) : net; // 번 돈보다 쓴 돈이 많은 날은 배수를 적용하지 않는다
        bonusMoney = final - net;
        charmsToGrant = DayRating.CharmReward(starCount);

        if (orderNumberText != null) orderNumberText.text = $"No. {day:0000}";
        if (titleText != null) titleText.text = $"{day}일차 정산";

        for (int i = 0; i < stars.Length; i++) stars[i].sprite = i < starCount ? starFull : starEmpty;

        SetReward(charmReward, charmRewardText, charmsToGrant > 0, $"x{charmsToGrant}");
        SetReward(moneyReward, moneyRewardText, multiplier > 1f, $"x{multiplier:0.#}");

        if (earnedText != null) earnedText.text = $"+ {earned}";
        if (spentText != null) spentText.text = spent > 0 ? $"- {spent}" : "0";
        if (netText != null) netText.text = net.ToString();
        if (finalText != null) finalText.text = final.ToString();

        if (goNextButton != null) goNextButton.onClick.AddListener(OnGoNext);
    }

    private static void SetReward(GameObject root, TMP_Text text, bool show, string label)
    {
        if (root != null) root.SetActive(show);
        if (text != null) text.text = label;
    }

    private void OnGoNext()
    {
        if (claimed) return; // 두 번 눌러 보상이 두 번 지급되지 않게
        claimed = true;
        if (goNextButton != null) goNextButton.interactable = false;

        CurrencyManager wallet = CurrencyManager.Instance;
        if (wallet != null)
        {
            if (bonusMoney > 0) wallet.AddMoney(bonusMoney);
            if (charmsToGrant > 0) wallet.AddCharms(charmsToGrant);
        }
        if (GameManager.Instance != null) GameManager.Instance.StartNextDay();
    }
}
