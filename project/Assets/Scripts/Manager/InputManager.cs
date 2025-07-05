using System;
using UnityEngine;
using UnityEngine.InputSystem;
public class InputManager : MonoBehaviour
{
   public PlayerController playerController;
   public InputAction _moveAction, _jumpAction, _lookAction;

   private void Start()
   {
      _moveAction = InputSystem.actions.FindAction("Move");
      _jumpAction  = InputSystem.actions.FindAction("Jump");
      _lookAction = InputSystem.actions.FindAction("Look");
      
      Cursor.visible = false;
   }

   private void Update()
   {
      Vector2 input = _moveAction.ReadValue<Vector2>();
      playerController.Move(new Vector3(input.x, 0f, input.y)); // 

      
      Vector2 lookVector = _lookAction.ReadValue<Vector2>();
      playerController.Look(lookVector);
      
      if (_jumpAction.triggered)
      {
         playerController.Jump();
         Debug.unityLogger.Log("Jump");
      }
   }
}
