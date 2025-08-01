using UnityEngine;

public class Open_Close_Credits : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

    public void OpenPanel()
    {
        settingsPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        settingsPanel.SetActive(false);
    }

}
