using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

using Haare.Client.Core;
using Haare.Client.Core.Singleton;
using Haare.Client.UI;
using Haare.Util.Logger;

public class HaareClient
{
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
    static async void Main() {

        // RuntimeInitializeOnLoadMethod는 스크립트 재컴파일 등으로 인한 도메인 리로드 시
        // 플레이 모드가 아닌데도 호출될 수 있다. DontDestroyOnLoad는 플레이 모드에서만
        // 허용되므로(RegisterProcesses 참고) 여기서 미리 걸러준다.
        if (!Application.isPlaying) return;

        LogHelper.Log(LogHelper.FRAMEWORK,"Start Haare Framework");
        await Task.Delay( 1 );
        await Processor.WaitForCreation();
        
        await Processor.Instance.Constructor(InitializePlugin, RegisterProcesses);
        
    }

    static async UniTask InitializePlugin() {
        // SDK 로드
        await UniTask.Delay( 0 );
    }
    

    static async UniTask RegisterProcesses()
    {
        LogHelper.LogTask(LogHelper.FRAMEWORK,"RegisterProcesses");
        
        // 씬 전역에서 사용하는 object
        // activeInputHandler가 New Input System 전용(1)이라 레거시 StandaloneInputModule은 동작하지 않는다 -> InputSystemUIInputModule 사용.
        // 씬에 이미 있으면(수동 배치 등) 중복 생성하지 않는다.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObj.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Object.DontDestroyOnLoad(eventSystemObj);
        }

        if (Object.FindObjectOfType<AudioListener>() == null)
        {
            var audioObj = new GameObject("AudioListener", typeof(AudioListener));
            Object.DontDestroyOnLoad(audioObj);
        }

        
        LogHelper.LogTask(LogHelper.FRAMEWORK,"RegisterProcesses -> end");
        await UniTask.CompletedTask;
    }
    
}
