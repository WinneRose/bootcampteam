using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class NetworkedQuestUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject questUIPrefab;
    [SerializeField] private Transform questContainer;
    [SerializeField] private GameObject questPanel;

    [Header("Input System")]
    [SerializeField] private InputActionReference toggleQuestPanelAction;
    [SerializeField] private Button toggleButton;

    private Dictionary<NetworkedQuestInstance, GameObject> questUIElements = new Dictionary<NetworkedQuestInstance, GameObject>();
    private bool isPanelOpen = false;

    private void Awake()
    {
        if (toggleQuestPanelAction != null)
        {
            toggleQuestPanelAction.action.Enable();
        }
    }

    private void OnEnable()
    {
        if (toggleQuestPanelAction != null)
        {
            toggleQuestPanelAction.action.Enable();
            toggleQuestPanelAction.action.performed += OnToggleQuestPanel;
        }
    }

    private void OnDisable()
    {
        if (toggleQuestPanelAction != null)
        {
            toggleQuestPanelAction.action.performed -= OnToggleQuestPanel;
            toggleQuestPanelAction.action.Disable();
        }
    }

    private void Start()
    {
        // Subscribe to networked quest events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted += OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed += OnQuestFailed;
        }

        // Setup toggle button
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleQuestPanel);

        // Initially hide the panel
        if (questPanel != null)
        {
            questPanel.SetActive(false);
            isPanelOpen = false;
        }
    }

    private void Update()
    {
        UpdateQuestProgress();
    }

    private void OnToggleQuestPanel(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Toggle Quest Panel Input Triggered");
            ToggleQuestPanel();
        }
    }

    private void ToggleQuestPanel()
    {
        if (questPanel != null)
        {
            bool willOpen = !questPanel.activeSelf;
            questPanel.SetActive(willOpen);
            isPanelOpen = willOpen;

            Debug.Log($"Quest Panel {(willOpen ? "Opened" : "Closed")}");
        }
    }

    private void OnQuestStarted(NetworkedQuestInstance quest)
    {
        CreateQuestUI(quest);
    }

    private void OnQuestCompleted(NetworkedQuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            StartCoroutine(AnimateQuestCompletion(quest));
        }
    }

    private void OnQuestFailed(NetworkedQuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            StartCoroutine(AnimateQuestFailure(quest));
        }
    }

    private void CreateQuestUI(NetworkedQuestInstance quest)
    {
        if (questUIPrefab == null || questContainer == null) return;

        GameObject questUI = Instantiate(questUIPrefab, questContainer);
        NetworkedQuestUIElement questElement = questUI.GetComponent<NetworkedQuestUIElement>();

        if (questElement != null)
        {
            questElement.Setup(quest);
            questUIElements[quest] = questUI;
        }
    }

    private void UpdateQuestProgress()
    {
        // Create a copy of the keys to avoid modification during iteration
        var questsToUpdate = new List<NetworkedQuestInstance>(questUIElements.Keys);
        
        foreach (var quest in questsToUpdate)
        {
            if (questUIElements.ContainsKey(quest))
            {
                GameObject uiElement = questUIElements[quest];
                if (uiElement != null)
                {
                    NetworkedQuestUIElement questElement = uiElement.GetComponent<NetworkedQuestUIElement>();
                    if (questElement != null)
                    {
                        questElement.UpdateProgress(quest);
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator AnimateQuestCompletion(NetworkedQuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            GameObject uiElement = questUIElements[quest];
            NetworkedQuestUIElement questElement = uiElement.GetComponent<NetworkedQuestUIElement>();

            if (questElement != null)
            {
                questElement.ShowCompleted();
            }

            yield return new WaitForSeconds(2f);

            questUIElements.Remove(quest);
            Destroy(uiElement);
        }
    }

    private System.Collections.IEnumerator AnimateQuestFailure(NetworkedQuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            GameObject uiElement = questUIElements[quest];
            NetworkedQuestUIElement questElement = uiElement.GetComponent<NetworkedQuestUIElement>();

            if (questElement != null)
            {
                questElement.ShowFailed();
            }

            yield return new WaitForSeconds(2f);

            questUIElements.Remove(quest);
            Destroy(uiElement);
        }
    }

    private void OnDestroy()
    {
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed -= OnQuestFailed;
        }

        if (toggleQuestPanelAction != null)
        {
            toggleQuestPanelAction.action.performed -= OnToggleQuestPanel;
            toggleQuestPanelAction.action.Disable();
        }
    }
}