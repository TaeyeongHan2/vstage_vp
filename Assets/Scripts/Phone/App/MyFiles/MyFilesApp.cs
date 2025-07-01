using UnityEngine;

namespace Phone
{
    public class MyFilesApp : AApp
    {
        [SerializeField] private MyFilesWindow myFilesWindow;
    
        public override void Open()
        {
            myFilesWindow.Open();
        }

        public override void Close()
        {
            myFilesWindow.Close();
        }
    }
}
