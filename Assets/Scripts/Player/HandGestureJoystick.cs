using UnityEngine;
using UnityEngine.Assertions;

public class HandGestureJoystick : ABaseInput<Vector2>
{
    #region Fields
    [SerializeField] private Transform _base;
    [SerializeField] private Transform _target;
    [SerializeField] private float _sensitivity = 4f;

    [SerializeField] private bool _updateBasePositionOnStartInput = true; 
    
    private Vector3 _startPosition;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        TestAssertion();
    }

    public void Update()
    {
        if (_isActive)
        {
            Vector3 currentDistance = _target.position - _base.position;
            Vector2 newValue = CalculateInputValue(currentDistance);
            Value = newValue;
        }
    }
    #endregion

    #region Methods
    public override void StartInput()
    {
        if (_updateBasePositionOnStartInput)
        {
            _base.position = _target.position;
        }
        
        base.StartInput();
    }
    
    private Vector2 CalculateInputValue(Vector3 distance)
    {
        Vector3 projDist = Vector3.ProjectOnPlane(distance, _base.up);
        Vector3 standardProjDist = Quaternion.Inverse(_base.rotation) * projDist;

        Vector2 rawValue = new(standardProjDist.x, standardProjDist.z);
        
        float maxMagnitude = 1f / _sensitivity;
        Vector2 value = _sensitivity * (rawValue.sqrMagnitude > maxMagnitude * maxMagnitude 
            ? maxMagnitude * rawValue.normalized
            : rawValue);

        return value;
    }

    private void TestAssertion()
    {
        Assert.IsNotNull(_base, "Field Base is null");
        Assert.IsNotNull(_target, "Field Target is null");
    }
    #endregion
}
