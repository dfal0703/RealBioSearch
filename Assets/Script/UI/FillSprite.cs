using UnityEngine;

namespace Script.UI
{
    // Unity UI Image는 sprite가 null이면 m_Type/m_FillAmount를 완전히 무시하고 항상 꽉 찬
    // 사각형만 그린다(Image.OnPopulateMesh 소스: "if (overrideSprite == null) { base.
    // OnPopulateMesh(toFill); return; }" - Filled 분기 자체를 안 탐). 그래서 Type.Filled로
    // 진행 바를 그리려는 모든 Image는 반드시 sprite가 있어야 한다 - 사용자 피드백("진행바
    // 차는게 전혀 안 보여")의 실제 원인이 이것이었다. 프로젝트에 참고할 흰색 스프라이트
    // 에셋이 없어 런타임에 Texture2D.whiteTexture로 하나 만들어 공유한다(Image.color 틴트가
    // 그대로 적용되므로 색상엔 영향 없음).
    public static class FillSprite
    {
        private static Sprite _instance;

        public static Sprite Get()
        {
            if (_instance == null)
            {
                var tex = Texture2D.whiteTexture;
                _instance = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            return _instance;
        }
    }
}
