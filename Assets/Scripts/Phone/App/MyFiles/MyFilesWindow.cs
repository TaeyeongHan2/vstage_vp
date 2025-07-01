using UnityEngine;

namespace Phone
{
    public class MyFilesWindow : MonoBehaviour, IAppWindow
    {
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
