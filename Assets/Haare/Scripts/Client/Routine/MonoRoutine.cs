using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Haare.Client.Core;
using Haare.Util.Logger;

namespace Haare.Client.Routine
{
    /// <summary>
    ///  Gameobject에 할당이 가능한 Routine
    /// 일반적인 MonoBehaviour 와 동일한 역할입니다.
    /// </summary>
    public class MonoRoutine : MonoBehaviour ,IRoutine
    {
        public CancellationTokenSource _cts { get; private set; }
        
        public virtual bool isRegistered => true;
        public virtual bool isInSceneOnly { get; protected set; } = true;
        public bool isInitialized { get; private set; }
        public Func<CancellationToken,UniTask> Oninitialize { get; protected set; } = async (cts) => await UniTask.CompletedTask;
        public Func<UniTask> Onfinalize { get; protected set;} = async () => await UniTask.CompletedTask;
        public Subject<Unit> Onupdate { get; protected set; } = new Subject<Unit>();
        public Subject<Unit> OnLateupdate { get; protected set; } = new Subject<Unit>();
        public Subject<Unit> OnFixedupdate { get; protected set; } = new Subject<Unit>();
        
        public CompositeDisposable disposables = new CompositeDisposable();
        private bool _isFinalized = false; 

        private void Awake()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            if (!isRegistered) { return; } 
    
            // 비동기 초기화 로직을 안전하게 호출
            InitializeAsync(_cts.Token).Forget();
        }
        protected async UniTask InitializeAsync(CancellationToken cts)
        {
            try
            {
                Constructor();

                await UniTask.WaitUntil(
                    () => Processor.Instance.isInitialized
                    , cancellationToken : cts);

                await Processor.Instance.Register(this,cts);

                Processor.Instance.PROCESSING.Subscribe(_ =>
                {
                    if (_)
                    {
                        OnRestartProcess();
                    }
                    else
                    {
                        OnStopProcess();
                    }
                }).AddTo(disposables);;


                Processor.Instance.Onupdate.Subscribe(_ => { UpdateProcess(); }).AddTo(disposables);;
                Processor.Instance.OnLateupdate.Subscribe(_ => { LateUpdateProcess(); }).AddTo(disposables);;
                Processor.Instance.OnFixedupdate.Subscribe(_ => { FixedUpdateProcess(); }).AddTo(disposables);;
            }
            catch (OperationCanceledException exception)
            {
                LogHelper.Log(LogHelper.CANCELLED, 
                    $"{this.GetType()} initialization was canceled. by {exception}");
            }
            catch (Exception ex)
            {
                LogHelper.Error(LogHelper.FRAMEWORK, $"An error occurred during {this.GetType()} initialization: {ex}");
            }
        }
        
        
        protected virtual void Constructor() {
            
        }
        public virtual async UniTask Initialize(CancellationToken cts)
        {
            await Oninitialize(cts);
            isInitialized = true;
        }
        protected virtual void OnStopProcess()
        {
            
        }
        protected virtual void OnRestartProcess()
        {
            
        }
        protected virtual void UpdateProcess() {
            Onupdate.OnNext( Unit.Default );
        }
        protected virtual void LateUpdateProcess() {
            OnLateupdate.OnNext( Unit.Default );
        }  
        protected virtual void FixedUpdateProcess() {
            OnFixedupdate.OnNext( Unit.Default );
        }
        
        public virtual async UniTask Finalize()
        {
            if (_isFinalized) return;
            _isFinalized = true;
            disposables.Clear();
            await Onfinalize();
        
        }
        private void OnApplicationQuit()
        {
            disposables.Clear();
        }
        private void OnDestroy()
        {
            if (_isFinalized) return;

            if (Processor.isCreated)
            {
                Processor.Instance.UnRegister(this);
            }
            _cts.Cancel();

            // Addressables.InstantiateAsync로 만들어진 오브젝트(패널 등)가 SceneUIManager.
            // ClosePeekPanel()이 아니라 씬 언로드(예: ssh -> Title 전환) 때문에 그냥 파괴되는
            // 경로가 있다 - 그런 경우 Addressables 쪽 핸들 추적은 안 풀린 채로 남아서, Editor
            // Play를 멈출 때 Addressables.PlayModeStateChangedCleanup이 이미 없어진 오브젝트를
            // 가리키는 핸들을 정리하려다 "Attempting to use an invalid operation handle"
            // 예외를 던진다(2026-07-20, errors.md). ReleaseInstance는 이 GameObject가 애초에
            // Addressables 추적 대상이 아니면 조용히 false만 반환하고 아무 부작용이 없으므로
            // (AddressablesImpl.ReleaseInstance - m_resultToHandle 사전 조회 실패 시 return
            // false), MonoRoutine 전체에 걸어둬도 안전하다 - 어차피 파괴되는 중이므로 예외가
            // 나더라도 게임 동작에 영향은 없어 조용히 삼킨다.
            try
            {
                Addressables.ReleaseInstance(gameObject);
            }
            catch
            {
                // ignored - 이미 파괴 중인 오브젝트에 대한 정리 시도라 실패해도 무해하다.
            }
        }
    }
}