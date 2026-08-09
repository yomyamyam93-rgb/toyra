using UnityEditor;
using UnityEngine;

/// 씬에 `무기` 오브젝트를 실물로 만든다.
///
/// ★★「손·무기는 반드시 씬에 실존시킨다」 (CLAUDE.md). 런타임 생성만 하면 편집 창에
///   아무것도 없어서 **끌어다 놓고 보면서 맞출 수가 없다** — 숫자를 타이핑하는 수밖에 없다.
///   실제로 2026-08-09 에 사용자가 "사람 레이어 아래 넣어서 그게 없네" 로 다시 지적했다.
///
/// 한 번 누르면 끝이다. `HeroAttack.MakeWeapon` 은 이미 있으면 그걸 찾아 쓴다.
public static class 무기실물
{
    [MenuItem("Tools/토이라/무기 만들기")]
    public static void 만들기()
    {
        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[무기] 씬에 HeroAttack 이 없다."); return; }

        var 있는것 = hero.transform.Find("무기");
        if (있는것 != null) { Selection.activeGameObject = 있는것.gameObject; Debug.Log("[무기] 이미 있다 — 선택해 뒀다."); return; }

        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "무기";
        Undo.RegisterCreatedObjectUndo(g, "무기 만들기");
        Undo.SetTransformParent(g.transform, hero.transform, "무기 만들기");
        g.transform.localScale = new Vector3(hero.굵기, hero.굵기, hero.길이);
        Object.DestroyImmediate(g.GetComponent<Collider>());

        var mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.62f, 0.5f, 0.35f) };

        Selection.activeGameObject = g;
        Debug.Log("[무기] 만들었다. 인스펙터에서 HeroAttack 의 「직접조절」을 켜면 씬 뷰에서 끌어 옮길 수 있고, 옮긴 만큼이 쥔자리·기울임에 담긴다.");
    }
}
