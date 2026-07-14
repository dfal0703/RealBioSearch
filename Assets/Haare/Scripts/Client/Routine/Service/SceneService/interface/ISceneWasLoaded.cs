using UnityEngine.EventSystems;

namespace Haare.Client.Routine.Service.SceneService
{
    public interface ISceneWasLoaded : IEventSystemHandler
    {
        void OnSceneWasLoaded(object argument);
    }

}