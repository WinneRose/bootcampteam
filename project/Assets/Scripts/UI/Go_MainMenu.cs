using UnityEngine;
using UnityEngine.SceneManagement;

public class Go_MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(sceneName);
    }
}
