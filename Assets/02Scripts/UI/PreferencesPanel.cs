using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 버튼을 누르면 설정 팝업을 열고, 닫기 버튼으로 닫는다(데이메인 밖의 씬에서 쓰는 상시 표시용).</summary>
public class PreferencesPanel : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject popup;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        popup.SetActive(false);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
    }

    private void Open()
    {
        popup.SetActive(true);
        DayClock.Instance?.SetPaused(true); // 설정 창이 열려 있는 동안 시간이 멈춘다
    }

    private void Close()
    {
        popup.SetActive(false);
        DayClock.Instance?.SetPaused(false);
    }
}
