using UnityEngine;
using UnityEngine.UI;

namespace Phone
{
    public class PhotoCameraWindow : MonoBehaviour, IAppWindow
    {
        [SerializeField] private RawImage screen;
        public RawImage Screen => screen;
        
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
