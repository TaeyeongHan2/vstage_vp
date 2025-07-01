using UnityEngine;
using UnityEngine.Assertions;

public class HandMoveProvider : MonoBehaviour
{
    [SerializeField] CharacterController _characterController;
    [SerializeField] private ABaseInput<Vector2> _moveHandInput;
    [SerializeField] private float _maxSpeed = 5f;
    
    public void FixedUpdate()
    {
        Assert.IsNotNull(_characterController, "Character controller is null");
        
        if (_moveHandInput?.isActiveAndEnabled == true)
        {
            Vector2 inputValue = _moveHandInput.Value;
            Vector3 weightDirection = new(inputValue.x, 0f, inputValue.y);
            _characterController.Move(_maxSpeed * Time.deltaTime * weightDirection);
        }
    }
}