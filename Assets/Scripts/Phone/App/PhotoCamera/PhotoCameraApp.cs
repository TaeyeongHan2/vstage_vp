using UnityEngine;
using UnityEngine.Serialization;

namespace Phone
{
    public class PhotoCameraApp : AApp
    {
        [SerializeField] private PhotoCameraWindow window;
        [SerializeField] private Camera photoCamera;

        [SerializeField] private RenderTexture photoRenderTexture;

        private void Start()
        {
            window.gameObject.SetActive(false);
            photoCamera.gameObject.SetActive(false);
            
            // 카메라에 렌더텍스처 연결
            photoCamera.targetTexture = photoRenderTexture;

            // 핸드폰 UI에 해당 텍스처 연결
            window.Screen.texture = photoRenderTexture;
        }
        
        public override void Open()
        {
            window.Open();
            photoCamera.gameObject.SetActive(true);
        }

        public override void Close()
        {
            window.Close();
            photoCamera.gameObject.SetActive(false);
        }
    }
}
