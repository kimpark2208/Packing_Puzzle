using UnityEngine;

/// <summary>NPC가 메인 화면의 어느 캐릭터 오브젝트로 나오는가(메인 씬의 DayCharacterArea 안). 체구에 따라 나뉜다.</summary>
public enum NpcCharacterType
{
    [InspectorName("노말 캐릭터 (NomalChar: 체구가 작은 캐릭터, 부위를 조합한 모습)")] Normal,
    [InspectorName("큰 캐릭터 (BigChar: 체구가 큰 캐릭터, 한 장짜리 큰 그림)")] Big,
}

/// <summary>이름이 있는 NPC(가구 상인, 세이렌, 구재민 …). 대사 CSV의 대화ID는 `id_` 로 시작한다(예: merchant_d1_intro).</summary>
[CreateAssetMenu(fileName = "NpcData", menuName = "Puzzle/NpcData", order = 6)]
public class NpcData : ScriptableObject
{
    [Tooltip("영문 ID. 대화ID의 앞부분과 같다(merchant, siren, jaemin, santa, supplies, hero).")] public string id;
    public string displayName;
    [Tooltip("메인 화면에서 이 NPC를 어느 캐릭터 오브젝트로 보여 줄지. 체구가 작은 캐릭터는 노말(NomalChar), 체구가 큰 캐릭터는 큰 캐릭터(BigChar)를 고른다.")] public NpcCharacterType characterType;
    [Tooltip("큰 캐릭터(BigChar)에 보여 줄 그림. 큰 캐릭터인데 비워 두면 무작위 손님 모습을 임시로 쓴다.")] public Sprite portrait;
}
