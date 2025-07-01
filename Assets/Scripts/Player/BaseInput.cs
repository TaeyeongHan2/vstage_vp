using System;
using UnityEngine;

public abstract class ABaseInput<T>: MonoBehaviour, IInput
{
    #region Events
    public event Action OnStartInput;
    public event Action OnStopInput;
    #endregion
    
    #region Fields
    public T Value { get; protected set; }
    
    protected bool _isActive = false;
    #endregion
    
    #region Methods
    public virtual void StartInput()
    {
        _isActive = true;
        OnStartInput?.Invoke();
    }

    public virtual void StopInput()
    {
        _isActive = false;
        Value = default;
        OnStopInput?.Invoke();
    }
    #endregion
}