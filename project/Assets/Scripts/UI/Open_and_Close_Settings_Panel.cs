using UnityEngine;

public class Open_and_Close_Settings_Panel : MonoBehaviour
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
