using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 화면의 모습을 메인 화면의 상황(MainState)에 맞춰 바꾼다: 낮 손님 = 낮, 낮 종료 = 노을, 밤 손님/밤 종료 = 밤.
/// 배경, 걸이, 책상, 전화기, 계산대, 말풍선처럼 모양이 같은 이미지는 오브젝트 하나의 스프라이트만 바꾼다(스프라이트는 인스펙터에 넣어 둔다).
/// 낮 손님 캐릭터와 밤 손님 캐릭터처럼 구성이 다른 오브젝트는 낮에만/밤에만 보이게 켜고 끈다. 오브젝트는 이름으로 찾는다(SceneFind).
/// </summary>
public class MainLook : MonoBehaviour
{
    [Serializable]
    private struct LookSprite
    {
        public string imageName; // 씬에서 이 이름의 Image를 찾는다
        public Sprite day;
        public Sprite sunset;
        public Sprite night;
    }

    [SerializeField] private string[] dayOnlyNames = { "DayCharacterArea" };      // 낮 손님과 낮 종료(노을)에서만 보인다
    [SerializeField] private string[] nightOnlyNames = { "NightCharacterArea" };  // 밤 손님과 밤 종료에서만 보인다
    [SerializeField] private LookSprite[] images;

    public void Apply(MainState state)
    {
        bool night = state == MainState.NightCustomer || state == MainState.NightEnding;
        SetActive(dayOnlyNames, !night);
        SetActive(nightOnlyNames, night);

        foreach (LookSprite entry in images)
        {
            Sprite sprite = state == MainState.DayCustomer ? entry.day : state == MainState.DayEnding ? entry.sunset : entry.night;
            if (sprite == null) continue; // 비워 둔 모습은 지금 그림을 그대로 둔다
            foreach (Image image in SceneFind.All<Image>(gameObject.scene, entry.imageName)) image.sprite = sprite;
        }
    }

    private void SetActive(string[] names, bool active)
    {
        foreach (string objectName in names)
        {
            foreach (Transform t in SceneFind.All<Transform>(gameObject.scene, objectName)) t.gameObject.SetActive(active);
        }
    }
}
