using System.Collections.Generic;
using UnityEngine;

/// 무리 — **개체가 아니라 무리가 정보를 나눈다.**
///
/// ★좀비는 서로 남남이라 한 마리씩 따로 놀아도 됐다. 동물은 아니다:
///   한 마리가 놀라면 무리가 같이 놀라고, 새끼가 위협받으면 어미가 사나워진다.
///   그래서 **무리가 1급 객체**여야 한다 — 개체에 깃발을 붙여선 안 된다.
///
/// MonoBehaviour 가 아니다. 그냥 데이터 묶음이라 테스트하기도 쉽다.
public class Pack
{
    public SpeciesDef 종;
    public Vector3 집;                     // 영역의 중심 — 여기서 멀어지면 돌아온다
    public readonly List<Critter> 식구 = new List<Critter>();

    /// 무리 전체가 얼마나 놀랐나 (0~1). 한 마리가 다치면 같이 오른다
    public float 동요;
    /// 새끼가 위협받는 중인가 — 어미가 사나워진다
    public bool 새끼위험;

    public static readonly List<Pack> All = new List<Pack>();

    public Pack(SpeciesDef s, Vector3 home)
    {
        종 = s; 집 = home;
        All.Add(this);
    }

    public void 들어옴(Critter c) { if (!식구.Contains(c)) 식구.Add(c); c.무리 = this; }

    public void 나감(Critter c)
    {
        식구.Remove(c);
        if (식구.Count == 0) All.Remove(this);
    }

    /// 한 마리가 다치면 무리가 술렁인다 — 겁 많은 종은 이것만으로 흩어진다
    public void 다쳤다(Critter who, bool 새끼냐)
    {
        동요 = Mathf.Min(1f, 동요 + (새끼냐 ? 0.5f : 0.25f));
        if (새끼냐) 새끼위험 = true;
    }

    /// 한 마리가 죽으면 크게 술렁인다
    public void 죽었다(bool 새끼냐)
    {
        동요 = Mathf.Min(1f, 동요 + (새끼냐 ? 0.8f : 0.4f));
    }

    /// 시간이 지나면 가라앉는다 (아무나 한 마리가 대표로 부른다)
    public void 식힘(float dt)
    {
        동요 = Mathf.Max(0f, 동요 - dt * 0.12f);
        if (동요 < 0.15f) 새끼위험 = false;
    }

    /// 살아있는 식구 수
    public int 살아있는
    {
        get
        {
            int n = 0;
            for (int i = 식구.Count - 1; i >= 0; i--)
            {
                var c = 식구[i];
                if (c == null) { 식구.RemoveAt(i); continue; }
                if (c.Alive) n++;
            }
            return n;
        }
    }

    /// 처음 마릿수 대비 얼마나 남았나 — 절반 아래면 무리가 무너진 것으로 본다
    public int 처음마릿수 = 1;
    public float 남은비율 => 처음마릿수 <= 0 ? 0f : 살아있는 / (float)처음마릿수;
}
