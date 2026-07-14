using System.Threading;
using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;

using Haare.Client.Routine;

namespace Haare.Client.UI
{
    
    [RequireComponent(typeof(Slider))]
    public class CustomSlider : MonoRoutine
    {
        [SerializeField]
        private Slider _slider;

        [SerializeField] private CustomImage background;
        [SerializeField] private CustomImage fill;
        public float Value => _slider.value;

        // CustomText/CustomImage와 동일하게 Constructor()(Awake 시점, 동기)에서 채워야
        // 외부에서 Awake 직후 곧바로 Setup()을 호출해도(BindEvent 등) NullReferenceException이 나지 않는다.
        // 예전엔 Initialize()(비동기)에서만 채우고 매번 Setup(0,1,0)으로 강제 리셋해서,
        // 외부에서 지정한 min/max/value를 나중에 덮어써버리는 문제도 있었다.
        protected override void Constructor()
        {
            base.Constructor();
            if (_slider == null)
                _slider = GetComponent<Slider>();
        }

        public override async UniTask Initialize(CancellationToken cts)
        {
            await base.Initialize(cts);
            if (_slider == null)
                _slider = GetComponent<Slider>();
        }

        /// <summary>
        /// 슬라이더를 초기화합니다.
        /// </summary>
        public void Setup(float minValue, float maxValue, float currentValue)
        {
            _slider.minValue = minValue;
            _slider.maxValue = maxValue;
            _slider.value = Mathf.Clamp(currentValue, minValue, maxValue);
        }
        public void SetValue(float value)
        {
            _slider.value = Mathf.Clamp(value, _slider.minValue, _slider.maxValue);
        }
    }
}