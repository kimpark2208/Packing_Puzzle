using System;
using UnityEngine;

/// <summary>
/// 밤 진단에서 체크한 아픔 하나를 고치는 수리 퍼즐 패널의 공통 부분. 아픔마다 이 클래스를 이어받은 패널이 하나씩 있고(물 주기 등),
/// NightDiagnosisUI가 체크한 아픔의 패널을 차례로 연다(Open). 퍼즐을 풀면 패널이 스스로 닫히고 풀었다고 알린다(Solve).
/// </summary>
public abstract class RepairPuzzlePanel : MonoBehaviour
{
    [SerializeField, Tooltip("이 패널이 고치는 아픔.")] private FlowerData.Ailment ailment;

    private Action onSolved;

    public FlowerData.Ailment Ailment => ailment;

    /// <summary>지금 손님의 진단 문제(꽃 그림이 필요한 퍼즐이 읽어 간다). 이 씬만 실행했을 때는 null.</summary>
    protected NightDiagnosisCase Case { get; private set; }

    /// <summary>true면 이 퍼즐이 열릴 때 화병과 꽃을 숨기고(패널이 꽃을 따로 크게 보여 준다), false면 화병과 꽃이 옆으로 옮겨 가 퍼즐 자리를 비운다.</summary>
    public virtual bool HidesFlower => false;

    /// <summary>패널을 열고 퍼즐을 시작한다. 퍼즐을 풀면 onSolved를 부른다.</summary>
    public void Open(NightDiagnosisCase diagnosis, Action onSolved)
    {
        Case = diagnosis;
        this.onSolved = onSolved;
        gameObject.SetActive(true);
        Begin();
    }

    /// <summary>퍼즐을 처음 상태로 만들고 시작한다.</summary>
    protected abstract void Begin();

    /// <summary>퍼즐을 풀었다: 패널을 닫고 알린다.</summary>
    protected void Solve()
    {
        gameObject.SetActive(false);
        Action callback = onSolved;
        onSolved = null;
        callback?.Invoke();
    }
}
