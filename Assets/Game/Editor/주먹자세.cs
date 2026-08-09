using UnityEditor;
using UnityEngine;

/// 씬에서 잡아 놓은 손 자세를 「주먹」으로 기억시킨다.
///
/// ★★각도를 코드가 정하지 않는 이유 (2026-08-09): 뼈 축은 리그마다 다르다. "손가락을 몇 도
///   접으면 주먹" 을 짐작하면 반드시 틀린다 — 이 프로젝트가 팔·턱·다리에서 이미 여러 번 당했다.
///   대신 **사람이 눈으로 잡은 자세를 그대로 뜬다.** 리깅이 바뀌어도 다시 누르면 그만이다.
///
/// 쓰는 법:
///   ① `RightHand` 밑에 손가락 뼈가 있는 몸을 씬에 켜 둔다
///   ② 씬에서 손가락 뼈를 돌려 주먹 모양을 만든다
///   ③ 이 메뉴를 누른다 → 그 자세가 `HeroAttack` 에 저장된다
///   ④ 손가락 뼈를 원래대로 되돌린다 (저장된 건 값이라 안 지워진다)
public static class 주먹자세
{
    [MenuItem("Tools/토이라/지금 손 자세를 주먹으로 저장")]
    public static void 저장()
    {
        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[주먹] 씬에 HeroAttack 이 없다."); return; }
        Undo.RecordObject(hero, "주먹 자세 저장");
        string 말 = hero.주먹으로저장();
        EditorUtility.SetDirty(hero);
        Debug.Log("[주먹] " + 말);
    }
}
