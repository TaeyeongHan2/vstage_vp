using UnityEngine;

namespace ManagerScene
{
    public class ManagerSceneServices : SceneSingleton<ManagerSceneServices>
    {
        [SerializeField] SceneController _sceneController;
        public static SceneController SceneController => Instance._sceneController;
    }
}
