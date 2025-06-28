using StarterAssets;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class ClientPlayerMove : NetworkBehaviour
{
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private StarterAssetsInputs m_StarterAssetsInputs;
    [SerializeField] private ThirdPersonController m_ThirdPersonController;

    void Awake()
    {
        m_ThirdPersonController.enabled = false;
        m_PlayerInput.enabled = false;
        m_StarterAssetsInputs.enabled = false;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            m_ThirdPersonController.enabled = true;
            m_PlayerInput.enabled = true;
            m_StarterAssetsInputs.enabled = true;
        }
        if (IsServer)
        {
            m_ThirdPersonController.enabled = true;
        }
    }
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint)
    {
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.LookInput(look);
    }
    private void LateUpdate()
    {
        if (!IsOwner)
            return;
        
        UpdateInputServerRpc(m_StarterAssetsInputs.move, m_StarterAssetsInputs.look, m_StarterAssetsInputs.jump, m_StarterAssetsInputs.sprint);
    }
}
