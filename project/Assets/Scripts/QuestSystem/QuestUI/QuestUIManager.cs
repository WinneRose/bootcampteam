using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class QuestUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject questUIPrefab;
    [SerializeField] private Transform questContainer;
    [SerializeField] private GameObject questPanel;

    [Header("Input System")]
    [SerializeField] private InputActionReference toggleQuestPanelAction;
    [SerializeField] private Button toggleButton;

    private Dictionary<QuestInstance, GameObject> questUIElements = new Dictionary<QuestInstance, GameObject>();
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
        // Subscribe to quest events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted += OnQuestStarted;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            QuestManager.Instance.OnQuestFailed += OnQuestFailed;
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


    private void OnQuestStarted(QuestInstance quest)
    {
        CreateQuestUI(quest);
    }

    private void OnQuestCompleted(QuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            StartCoroutine(AnimateQuestCompletion(quest));
        }
    }

    private void OnQuestFailed(QuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            StartCoroutine(AnimateQuestFailure(quest));
        }
    }

    private void CreateQuestUI(QuestInstance quest)
    {
        if (questUIPrefab == null || questContainer == null) return;

        GameObject questUI = Instantiate(questUIPrefab, questContainer);
        QuestUIElement questElement = questUI.GetComponent<QuestUIElement>();

        if (questElement != null)
        {
            questElement.Setup(quest);
            questUIElements[quest] = questUI;
        }
    }

    private void UpdateQuestProgress()
    {
        foreach (var kvp in questUIElements)
        {
            QuestInstance quest = kvp.Key;
            GameObject uiElement = kvp.Value;

            if (uiElement != null)
            {
                QuestUIElement questElement = uiElement.GetComponent<QuestUIElement>();
                if (questElement != null)
                {
                    questElement.UpdateProgress(quest);
                }
            }
        }
    }

    private System.Collections.IEnumerator AnimateQuestCompletion(QuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            GameObject uiElement = questUIElements[quest];
            QuestUIElement questElement = uiElement.GetComponent<QuestUIElement>();

            if (questElement != null)
            {
                questElement.ShowCompleted();
            }

            yield return new WaitForSeconds(2f);

            questUIElements.Remove(quest);
            Destroy(uiElement);
        }
    }

    private System.Collections.IEnumerator AnimateQuestFailure(QuestInstance quest)
    {
        if (questUIElements.ContainsKey(quest))
        {
            GameObject uiElement = questUIElements[quest];
            QuestUIElement questElement = uiElement.GetComponent<QuestUIElement>();

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
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            QuestManager.Instance.OnQuestFailed -= OnQuestFailed;
        }

        if (toggleQuestPanelAction != null)
        {
            toggleQuestPanelAction.action.performed -= OnToggleQuestPanel;
            toggleQuestPanelAction.action.Disable();
        }
    }
}
