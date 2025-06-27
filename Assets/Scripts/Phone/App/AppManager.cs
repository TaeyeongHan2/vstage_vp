using System.Collections.Generic;
using UnityEngine;

namespace Phone
{
    public class AppManager : MonoBehaviour
    {
        [SerializeField] private List<AApp> _phoneApps;
    
        private EAppType _currentAppType = EAppType.None;

        public void OpenPhoneApp(EAppType appType)
        {
            if (_currentAppType == appType)
                return;

            if (_currentAppType != EAppType.None)
            {
                ClosePhoneApp();
            }
        
            _currentAppType = appType;

            foreach (var app in _phoneApps)
            {
                if (app.AppType == appType)
                {
                    app.Open();
                    return;
                }
            }
        }

        public void ClosePhoneApp()
        {
            if (_currentAppType == EAppType.None)
                return;
        
            foreach (var app in _phoneApps)
            {
                if (app.AppType == _currentAppType)
                {
                    app.Close();
                    _currentAppType = EAppType.None;
                    return;
                }
            }
        }
    }
}
