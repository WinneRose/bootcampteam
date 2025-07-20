using UnityEngine;
using UnityEngine.SceneManagement;

public class Go_Game_Scene : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
