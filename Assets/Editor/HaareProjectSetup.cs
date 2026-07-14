using UnityEditor;
using UnityEditor.Build;

namespace Haare.Editor.Setup
{
    /// <summary>
    /// DOTween이 Asset Store 패키지가 아니라 파일로 직접 벤더링되어 있어서
    /// UniTask.DOTween의 asmdef versionDefines(com.demigiant.dotween 감지)가 트리거되지 않는다.
    /// UNITASK_DOTWEEN_SUPPORT를 직접 정의해 TweenUI.cs의 Tweener.ToUniTask() 호출을 살린다.
    /// </summary>
    [InitializeOnLoad]
    internal static class HaareProjectSetup
    {
        private const string DefineSymbol = "UNITASK_DOTWEEN_SUPPORT";

        static HaareProjectSetup()
        {
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var target = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);

            if (System.Array.IndexOf(defines, DefineSymbol) >= 0)
                return;

            var updated = new string[defines.Length + 1];
            defines.CopyTo(updated, 0);
            updated[defines.Length] = DefineSymbol;
            PlayerSettings.SetScriptingDefineSymbols(target, updated);
        }
    }
}
