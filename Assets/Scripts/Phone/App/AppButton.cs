using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Phone
{
    public class AppButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private EAppType _appType;

        public UnityEvent<EAppType> OnClickButton;
    
        public EAppType GetAppType() => _appType;

        private void Awake()
        {
            _button.onClick.AddListener(()=> OnClickButton?.Invoke(_appType));
        }
    }
}
