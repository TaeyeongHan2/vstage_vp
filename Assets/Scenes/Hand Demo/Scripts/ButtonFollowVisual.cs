using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ButtonFollowVisual : MonoBehaviour
{
    [SerializeField] private Transform _visualTarget;
    [SerializeField] private Vector3 _localAxis;
    [SerializeField] private float _resetSpeed = 5f;
    [SerializeField] private float _followAngleTreshold = 80f;

    private bool _freeze = false;

    private Vector3 _initialLocalPos;
    
    private Vector3 _offset;
    private Transform _pokeAttachTransform;
    
    private XRBaseInteractable _interactable;
    private bool _isFollowing = false;
    
    void Start()
    {
        _initialLocalPos = _visualTarget.localPosition;
        
        _interactable = GetComponent<XRBaseInteractable>();
        _interactable.hoverEntered.AddListener(Follow);
        _interactable.hoverExited.AddListener(Reset);
        _interactable.selectEntered.AddListener(Freeze);
    }
    
    public void Follow(BaseInteractionEventArgs hover)
    {
        if (hover.interactorObject is XRPokeInteractor)
        {
            var interactor = (XRPokeInteractor)hover.interactorObject;
            
            _pokeAttachTransform = interactor.attachTransform;
            _offset = _visualTarget.position - _pokeAttachTransform.position;

            float pokeAngle = Vector3.Angle(_offset, _visualTarget.TransformDirection(_localAxis));

            if (pokeAngle < _followAngleTreshold)
            {
                _isFollowing = true;
                _freeze = false;
            }
        }
    }
    
    public void Reset(BaseInteractionEventArgs hover)
    {
        if (hover.interactorObject is XRPokeInteractor)
        {
            _isFollowing = false;
            _freeze = false;
        }
    }
    
    public void Freeze(BaseInteractionEventArgs hover)
    {
        if (hover.interactorObject is XRPokeInteractor)
        {
            _freeze = true;
        }
    }
    
    void Update()
    {
        if (_freeze) return;
        
        if (_isFollowing)
        {
            Vector3 localTargetPosition = _visualTarget.InverseTransformPoint(_pokeAttachTransform.position + _offset);
            Vector3 constrainedocaTargetPosition = Vector3.Project(localTargetPosition, _localAxis);
            
            _visualTarget.position = _visualTarget.TransformPoint(constrainedocaTargetPosition);
        }
        else
        {
            _visualTarget.localPosition = Vector3.Lerp(_visualTarget.localPosition, _initialLocalPos, Time.deltaTime * _resetSpeed);
        }
    }
}
