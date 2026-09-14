using UnityEngine;
using UnityEngine.InputSystem.Composites;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public class Temp_SceneLoader : MonoBehaviour
{
    Button btn;

    void Start()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(LoadScene);
    }
    
    void LoadScene()
    {
        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;
        
        if (nextScene < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextScene);
        }
    }
}
