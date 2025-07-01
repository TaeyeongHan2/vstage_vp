using UnityEngine;

namespace Phone
{
    public abstract class AApp : MonoBehaviour
    {
        [SerializeField] private EAppType appType = EAppType.None;
    
        public EAppType AppType => appType;
    
        public abstract void Open();
        public abstract void Close();
    }
}
