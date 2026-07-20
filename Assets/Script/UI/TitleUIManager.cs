using Haare.Client.UI;

namespace Script.UI
{
    // Title 씬 전용 SceneUIManager. BioSearchUIManager와 달리 패널 슬롯이 하나(TitlePanel)뿐이라
    // 추가 필드가 필요 없다 - SceneUIManager가 추상 클래스라 구체 타입 하나는 있어야 하므로
    // 존재하는 빈 서브클래스.
    public class TitleUIManager : SceneUIManager
    {
    }
}
