using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// 장난감 펫 늘어놓기 — 옛 프로젝트에서 가져온 모델을 **한 화면에 쭉 세워** 놓고 고른다.
///
/// ★혼자 보면 반드시 크기와 인상을 잘못 잡는다. 양 끝을 나란히 놓고 봐야 한다
///   (옛 프로젝트에서 이걸 안 해서 두 번 틀렸다).
/// ★사람 1.8m 기둥을 같이 세운다 — 옆에 비교 대상이 없으면 큰지 작은지 알 수가 없다.
///
/// 씬을 더럽히지 않는다: 「치우기」 한 번이면 통째로 사라진다.
public static class ToyLineup
{
    const string Folder = "Assets/Game/Models/toys";
    const string Root = "장난감_진열";

    [MenuItem("Tools/토이라기/장난감 펫 세워보기", priority = 30)]
    public static void Build()
    {
        Clear();

        var paths = AssetDatabase.FindAssets("t:GameObject", new[] { Folder })
                                 .Select(AssetDatabase.GUIDToAssetPath)
                                 .Where(p => p.EndsWith(".glb") || p.EndsWith(".fbx"))
                                 .OrderBy(p => p)
                                 .ToList();
        if (paths.Count == 0)
        {
            Debug.LogWarning($"[진열] {Folder} 에 모델이 없다.");
            return;
        }

        var root = new GameObject(Root);
        Undo.RegisterCreatedObjectUndo(root, "장난감 진열");
        root.transform.position = Vector3.zero;

        // 한 줄에 10마리씩, 간격은 넉넉하게
        const float 간격 = 4f, 줄간격 = 5f;
        int 한줄 = 10;

        // 맨 앞에 사람 키 기둥 — 크기를 잴 자
        기둥(root.transform, new Vector3(-간격, 0f, 0f));

        for (int i = 0; i < paths.Count; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (pf == null) continue;

            var pos = new Vector3((i % 한줄) * 간격, 0f, -(i / 한줄) * 줄간격);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            g.transform.position = pos;
            g.name = System.IO.Path.GetFileNameWithoutExtension(paths[i]);

            맞춤(g, 2f);          // 전부 키 2m 로 맞춰 세운다 — 원래 크기는 제각각이다
        }

        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"[진열] {paths.Count}마리 세움 — 씬 창에서 둘러보세요. 「장난감 진열 치우기」로 지웁니다.");
    }

    [MenuItem("Tools/토이라기/장난감 진열 치우기", priority = 31)]
    public static void Clear()
    {
        var old = GameObject.Find(Root);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    /// 모델마다 내보낸 크기가 제각각이라, 키를 맞춰야 나란히 비교가 된다
    static void 맞춤(GameObject g, float 키)
    {
        g.transform.localScale = Vector3.one;
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (키 / b.size.y);

        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.position += Vector3.up * (g.transform.position.y - b.min.y);
    }

    /// 사람 1.8m — 옆에 세워 두지 않으면 큰지 작은지 알 수가 없다
    static void 기둥(Transform parent, Vector3 at)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "사람_1.8m";
        g.transform.SetParent(parent, true);
        g.transform.localScale = new Vector3(0.5f, 1.8f, 0.35f);
        g.transform.position = at + Vector3.up * 0.9f;
        var col = g.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }
}
