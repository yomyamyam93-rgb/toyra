using UnityEngine;

/// 씬에 **무엇이 새로 생겼는지 거의 공짜로** 알아내는 자 (2026-08-06).
///
/// ★왜 필요한가 — 「새로 생긴 것을 주우려고 주기적으로 씬 전체를 훑는」 스크립트가 둘 있다
///   (`Outliner`·`Matte`). 숲에 나무가 2만 그루면 씬의 렌더러가 **7만 개**라, 훑을 것이
///   하나도 없어도 `FindObjectsByType` 한 번에 **100ms 이상**이 날아간다.
///   실측: 2초마다 `스크립트 119ms + GC 4.1MB` — "한 번씩 뚝뚝 끊긴다" 가 이것이었다.
///
/// ★수법: **뿌리들의 깊이 1 자식 수 합.** 무엇이 새로 생기면 어딘가의 자식이 되므로 이
///   숫자가 바뀐다. `childCount` 는 O(1) 이라 뿌리 몇십 개만 세면 사실상 공짜다.
///   ☆실루엣 복사본처럼 **깊은 곳에** 생기는 것은 이 숫자를 안 흔든다 — 그래서 헛걸음이
///     없는 대신, 깊은 데 조용히 생긴 것은 **가끔 강제로 훑어** 줍는다 (부르는 쪽의 몫).
public static class 씬바뀜
{
    /// 열린 씬들의 뿌리 자식 수 합. 값이 지난번과 같으면 **새로 생긴 게 없다고 본다.**
    public static int 자식수합()
    {
        int 합 = 0;
        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!sc.isLoaded) continue;
            var roots = sc.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) 합 += roots[i].transform.childCount;
        }
        return 합;
    }
}
