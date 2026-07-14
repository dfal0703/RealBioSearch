using UnityEngine.SceneManagement;

namespace Haare.Client.Routine.Service.SceneService
{
    public class SceneLoadRequest
    {
        public SceneName Scene { get; }
        public LoadSceneMode Mode { get; }
        public object Argument { get; }

        public SceneLoadRequest(SceneName scene, LoadSceneMode mode, object argument = null)
        {
            Scene = scene;
            Mode = mode;
            Argument = argument;
        }
    }
}