using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기억 흐려짐 수리 퍼즐: 기억 되짚기. 꽃잎이 차례로 빛나는 것을 보고, 같은 순서로 꽃잎을 누른다. 단계마다 빛나는 꽃잎이 늘어난다(기본 3 → 4 → 5개, 같은 꽃잎이 나올 수 있지만 연달아 나오지는 않는다).
/// 순서를 틀리면 그 단계를 처음부터 다시 본다. 마지막 단계까지 맞히면 성공이다. 이 퍼즐은 화병과 꽃을 숨기고, 패널 안에 꽃 한 송이를 크게 보여 준다(꽃잎은 진단 꽃의 그림을 쓴다).
/// 빛나는 모습은 낮 퍼즐에서 쓰는 꽃 빛 머티리얼(FlowerGlow)을 입힌 꽃잎 그림 복사본을 꽃 뒤에 깔아 두었다가, 순서가 올 때 그 꽃잎의 복사본을 켜서 보여 준다.
/// </summary>
public class MemoryPuzzlePanel : RepairPuzzlePanel
{
    private enum InputResult { Waiting, Correct, Wrong }

    private const float PetalHitAlpha = 0.1f; // 꽃잎 그림의 알파가 이보다 커야 그 꽃잎이 눌린다
    private const float GlowPadScale = 1.6f; // FlowerGlow 셰이더가 정해 둔 배율(셰이더 안의 _PadScale과 같아야 한다)

    [Header("꽃(패널 안에 크게 보이는 꽃)")]
    [SerializeField] private Image center;
    [SerializeField, Tooltip("꽃잎 6장의 버튼. 맨 위가 0번이고 시계 방향. 꽃잎 모양대로 눌리도록 코드가 각 이미지의 Alpha Hit Test Minimum Threshold를 정한다(꽃잎 그림은 Read/Write를 켜 둔다).")] private Button[] petals = new Button[FlowerData.PetalPartCount];
    [SerializeField, Tooltip("꽃잎마다 하나씩 꽃 뒤에 깔아 두는 꽃잎 그림 복사본(FlowerGlow 머티리얼). 꽃잎과 같은 순서로 6개. 평소에는 꺼 둔다. 그림과 크기는 코드가 꽃잎에 맞춘다.")] private Image[] glows = new Image[FlowerData.PetalPartCount];

    [Header("표시")]
    [SerializeField, Tooltip("단계 점마다 안에 들어 있는 채움 이미지(단계 수만큼, 단계 순서대로). 지금 단계까지의 점은 채움이 켜지고, 나머지는 꺼진다.")] private GameObject[] dotFills;
    [SerializeField] private TMP_Text message;
    [SerializeField, Tooltip("불빛이 가장 밝을 때의 색(알파는 코드가 바꾼다).")] private Color glowColor = Color.white;
    [SerializeField, Tooltip("꽃잎이 빛날 때 그 꽃잎에 입히는 따뜻한 색(곱해지는 색). 흰색이면 색을 바꾸지 않고 앞으로만 올라온다.")] private Color litTint = new(1f, 0.97f, 0.84f, 1f);

    [Header("세로형 꽃(금어초처럼 줄기를 따라 꽃송이가 달린 꽃)")]
    [SerializeField, Tooltip("세로형 꽃에서만 보이는 줄기 그림. 세로형 꽃송이 상자(VerticalHead) 안 맨 뒤에 둔다(상자와 함께 켜지고 꺼진다).")] private Image stemLeaf;

    [Header("조절")]
    [SerializeField, Tooltip("단계별로 빛나는 꽃잎 수.")] private int[] stageLengths = { 3, 4, 5 };
    [SerializeField, Min(0.1f), Tooltip("꽃잎 하나가 빛나는 시간(초).")] private float showSeconds = 0.55f;
    [SerializeField, Min(0f), Tooltip("꽃잎이 빛난 뒤 다음 꽃잎이 빛나기 전까지 쉬는 시간(초).")] private float gapSeconds = 0.25f;
    [SerializeField, Min(0.05f), Tooltip("꽃잎을 눌렀을 때 빛나는 시간(초).")] private float pressSeconds = 0.25f;
    [SerializeField, Min(0f), Tooltip("단계를 마치고 다음 단계를 보여 주기 전에 쉬는 시간(초).")] private float stagePauseSeconds = 0.8f;

    private const string VerticalHeadName = "VerticalHead";
    private Transform verticalHead;             // 세로형 꽃송이 상자와 그 안의 중심·꽃잎·불빛(이름으로 찾는다)
    private Image verticalCenter;
    private Button[] verticalPetals;
    private Image[] verticalGlows;
    private bool tallLayout;                    // 지금 꽃이 세로형 상자를 쓰는가
    private Image Center => tallLayout ? verticalCenter : center;          // 지금 꽃에 쓰는 중심·꽃잎·불빛
    private Button[] Petals => tallLayout ? verticalPetals : petals;
    private Image[] Glows => tallLayout ? verticalGlows : glows;
    private List<int> sequence;
    private int stage;
    private int inputIndex;
    private bool inputLocked = true;
    private InputResult result;
    private readonly Dictionary<Button[], int[]> petalHomes = new(); // 꽃송이 상자별로 꽃잎 이미지들이 하이라키에서 처음 놓인 자리(맨 앞으로 올렸다가 되돌릴 때 쓴다. 에디터에서 정해 둔 순서 그대로)
    private int litCount;             // 지금 빛나는(맨 앞으로 올라와 있는) 꽃잎 수

    public override bool HidesFlower => true;

    protected override void Begin()
    {
        litCount = 0;
        ApplyPartLayout(Case != null ? Case.art : null);
        if (!petalHomes.ContainsKey(Petals)) petalHomes[Petals] = Petals.Select(p => p.transform.GetSiblingIndex()).ToArray(); // 꽃송이 상자마다 처음 한 번만(꽃잎이 올라와 있지 않은 상태. 세로형 상자에는 줄기가 끼어 있어 번호가 다르다)
        RestorePetalOrder();
        if (Case != null) ApplyFlowerArt(Case.art);
        SyncGlows();

        for (int i = 0; i < Petals.Length; i++)
        {
            int index = i;
            Petals[i].GetComponent<Image>().alphaHitTestMinimumThreshold = PetalHitAlpha; // 투명한 곳은 눌리지 않아, 겹친 꽃잎 중 보이는 꽃잎이 눌린다
            Petals[i].onClick.RemoveAllListeners();
            Petals[i].onClick.AddListener(() => OnPetalPressed(index));
            Glows[i].gameObject.SetActive(false);
        }

        stage = 0;
        RefreshDots();
        inputLocked = true;
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>꽃의 배치를 맞춘다. 둥근형은 에디터에 놓인 그대로(줄기 없이 꽃 한 송이), 세로형(금어초처럼 줄기를 따라 꽃송이가 달린 꽃)은 세로형 꽃송이 상자(VerticalHead)에 에디터에서 정해 둔 위치·기울기·배율 그대로 보여 준다.</summary>
    private void ApplyPartLayout(FlowerData art)
    {
        var roundHead = center.transform.parent; // 둥근형 꽃송이 상자(RoundHead, 인스펙터에 연결된 중심·꽃잎·불빛의 부모)
        var flower = (RectTransform)roundHead.parent;
        if (verticalHead == null)
        {
            Transform found = flower.Find(VerticalHeadName); // 세로형 꽃송이 상자(VerticalHead): 안에 Center, Glow_1~6, Petal_1~6이 둥근형과 같은 순서로 있다
            if (found != null)
            {
                verticalHead = found;
                verticalCenter = found.Find("Center").GetComponent<Image>();
                verticalPetals = PartFinder.Children<Button>(found, "Petal");
                verticalGlows = PartFinder.Children<Image>(found, "Glow");
            }
        }
        bool tall = art != null && art.partLayout == FlowerData.PartLayout.Tall && verticalHead != null;
        tallLayout = tall;
        roundHead.gameObject.SetActive(!tall);
        if (verticalHead != null) verticalHead.gameObject.SetActive(tall);

        if (stemLeaf != null && tall) // 줄기는 세로형 꽃송이 상자 안에 있다(상자와 함께 켜진다)
        {
            stemLeaf.gameObject.SetActive(true);
            stemLeaf.sprite = art.stemLeafPartSprite;
        }
    }

    /// <summary>패널의 꽃을 진단 꽃의 그림으로 채운다(꽃 중심과 꽃잎 6장).</summary>
    private void ApplyFlowerArt(FlowerData art)
    {
        Center.sprite = art.centerPartSprite;
        Center.enabled = art.centerPartSprite != null; // 중심 그림이 없는 꽃은 중심을 그리지 않는다
        for (int i = 0; i < Petals.Length; i++) Petals[i].targetGraphic.GetComponent<Image>().sprite = art.petalPartSprites[i];
    }

    /// <summary>인스펙터에서 꽃잎을 옮기거나 키운 뒤, 이 항목(컴포넌트 우클릭 메뉴)을 누르면 플레이하지 않고도 불빛이 꽃잎에 맞는지 볼 수 있다.</summary>
    [ContextMenu("불빛 맞추기(미리 보기)")]
    private void PreviewLayout() => SyncGlows();

    /// <summary>꽃잎은 에디터에 놓인 그대로 두고, 꽃잎마다 복사본(불빛)만 그 꽃잎의 위치·크기·기울기에 맞춘다.
    /// 불빛은 꽃잎과 같은 그림이고 꽃잎 이미지보다 GlowPadScale배 큰 사각형이다(FlowerGlow 셰이더가 번지는 빛이 들어갈 자리를 그렇게 정해 둔다. 꽃잎 스프라이트는 잘라 내지 않는 FullRect로 임포트한다).</summary>
    private void SyncGlows()
    {
        for (int i = 0; i < Petals.Length; i++)
        {
            var petal = (RectTransform)Petals[i].transform;
            Sprite sprite = petal.GetComponent<Image>().sprite;
            Vector2 artCenter = Vector2.Scale(SpriteBounds.ArtCenter(sprite), petal.rect.size / sprite.rect.size); // 꽃잎 이미지 안에서 그림 중심의 위치(이미지 중심 기준)

            var glow = (RectTransform)Glows[i].transform;
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 0.5f);
            glow.sizeDelta = petal.rect.size * GlowPadScale;
            glow.pivot = new Vector2(0.5f + artCenter.x / glow.sizeDelta.x, 0.5f + artCenter.y / glow.sizeDelta.y); // 피벗을 그림 중심에 둔다
            glow.localScale = petal.localScale;
            glow.rotation = petal.rotation;
            glow.position = petal.TransformPoint(petal.rect.center + artCenter); // 꽃잎 그림 중심이 있는 곳에 불빛의 피벗을 놓는다
            Glows[i].sprite = sprite;
        }
    }

    private IEnumerator PlayRoutine()
    {
        for (stage = 0; stage < stageLengths.Length; stage++)
        {
            RefreshDots();
            sequence = MakeSequence(stageLengths[stage]);

            do
            {
                yield return ShowSequence();
                message.text = "같은 순서로 눌러 보세요";
                inputIndex = 0;
                result = InputResult.Waiting;
                inputLocked = false;
                yield return new WaitUntil(() => result != InputResult.Waiting);
                inputLocked = true;

                if (result == InputResult.Wrong)
                {
                    message.text = "틀렸어요! 다시 볼게요";
                    yield return GameTime.Wait(stagePauseSeconds);
                }
            }
            while (result != InputResult.Correct);

            message.text = "좋아요!";
            yield return GameTime.Wait(stagePauseSeconds);
        }

        message.text = "기억이 돌아왔어요!";
        yield return GameTime.Wait(stagePauseSeconds);
        Solve();
    }

    /// <summary>꽃잎이 순서대로 빛나는 것을 보여 준다(그동안은 누를 수 없다).</summary>
    private IEnumerator ShowSequence()
    {
        message.text = "순서를 잘 보세요";
        yield return GameTime.Wait(gapSeconds * 2f);
        foreach (int index in sequence)
        {
            yield return Glow(index, showSeconds);
            yield return GameTime.Wait(gapSeconds);
        }
    }

    /// <summary>꽃잎 index의 불빛이 켜졌다 꺼진다(seconds 동안, 앞뒤로 짧게 서서히). 빛나는 동안 그 꽃잎은 맨 앞으로 올라와 겹친 꽃잎에 가려지지 않고, 따뜻한 색이 입혀진다.</summary>
    private IEnumerator Glow(int index, float seconds)
    {
        Image glow = Glows[index];
        Image petalImage = Petals[index].GetComponent<Image>();
        glow.gameObject.SetActive(true);
        litCount++;
        Petals[index].transform.SetAsLastSibling();

        float fade = Mathf.Min(0.12f, seconds * 0.4f);
        for (float time = 0f; time < seconds; time += GameTime.Delta)
        {
            float strength = Mathf.Clamp01(Mathf.Min(time / fade, (seconds - time) / fade));
            Color color = glowColor;
            color.a = glowColor.a * strength;
            glow.color = color;
            petalImage.color = Color.Lerp(Color.white, litTint, strength);
            yield return null;
        }

        glow.gameObject.SetActive(false);
        petalImage.color = Color.white;
        litCount--;
        if (litCount <= 0) RestorePetalOrder(); // 빛나는 꽃잎이 없으면 원래 겹침 순서로 되돌린다
    }

    /// <summary>꽃잎 이미지들을 처음 놓였던 자리로 되돌린다(꽃잎끼리, 그리고 불빛·꽃 중심과의 순서가 에디터에서 정해 둔 그대로가 된다).</summary>
    private void RestorePetalOrder()
    {
        int[] petalHome = petalHomes[Petals];
        foreach (int i in Enumerable.Range(0, Petals.Length).OrderBy(k => petalHome[k])) Petals[i].transform.SetSiblingIndex(petalHome[i]); // 낮은 자리부터 차례로 넣어야 나머지가 밀리지 않는다
    }

    private void OnPetalPressed(int index)
    {
        if (inputLocked) return;

        StartCoroutine(Glow(index, pressSeconds));
        if (index != sequence[inputIndex])
        {
            result = InputResult.Wrong;
            inputLocked = true;
            return;
        }

        inputIndex++;
        if (inputIndex >= sequence.Count)
        {
            result = InputResult.Correct;
            inputLocked = true;
        }
    }

    /// <summary>같은 꽃잎이 연달아 나오지 않는 순서를 만든다.</summary>
    private List<int> MakeSequence(int length)
    {
        var list = new List<int>();
        int previous = -1;
        for (int i = 0; i < length; i++)
        {
            int next;
            do next = Random.Range(0, Petals.Length);
            while (next == previous);
            list.Add(next);
            previous = next;
        }
        return list;
    }

    /// <summary>지금 단계까지의 점은 채움을 켜고, 나머지는 끈다.</summary>
    private void RefreshDots()
    {
        for (int i = 0; i < dotFills.Length; i++) dotFills[i].SetActive(i <= stage);
    }
}
