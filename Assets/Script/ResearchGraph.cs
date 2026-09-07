using System.Collections.Generic;
using UnityEngine;

// 研究スキルツリー（放射状）のノード定義。企画書5章の系統ツリー＋「深淵のスキルツリー」構想に沿って、
// 中心から5属性の枝が放射状に伸び、枝の途中に小さなノード（ステータス強化・マナ上限・マナ回復速度）を、
// 節目に大きなノード（魔法の解放）を配置する。
//
// ・大ノード = 魔法（id は MagicData のアセット名）。コストは MagicData.requiredMaterials。
// ・小ノード = ステータス系（id は "node_..."）。コストは各ノード定義が持つ。
// ・各ノードの親は1つ（純粋な木）。親が割り当て済みでないと解放できない。
// ・座標は極座標（ring と angleDeg）で持ち、ビュー側が anchoredPosition に変換する。
public enum ResearchStat { Hp, Atk, Def, Spd, Luc, ManaMax, ManaRegen }

public enum ResearchNodeState { Allocated, Allocatable, Locked }

public class ResearchNodeDef
{
    public string id;
    public bool isMagic;          // true = 大ノード（魔法）, false = 小ノード（ステータス系）
    public string parentId;       // null = 根（中心付近の初期解放魔法）
    public int ring;              // 中心からの環番号（0が最内）
    public float angleDeg;        // 上を0度、時計回り

    // 小ノード用
    public ResearchStat stat;
    public float statAmount;
    public List<MaterialCost> cost = new List<MaterialCost>();
    public string shortLabel;     // ノードに表示する短いラベル（小ノードのみ。大ノードはビューが魔法名から作る）
    public string title;          // 詳細パネル用
}

public static class ResearchGraph
{
    public const float Ring0Radius = 340f;
    public const float RingStep = 460f;

    private static List<ResearchNodeDef> _nodes;
    private static Dictionary<string, ResearchNodeDef> _byId;
    private static Dictionary<string, string> _prereq;

    public static IReadOnlyList<ResearchNodeDef> Nodes { get { Ensure(); return _nodes; } }

    public static ResearchNodeDef Get(string id)
    {
        Ensure();
        return id != null && _byId.TryGetValue(id, out ResearchNodeDef d) ? d : null;
    }

    // ノードID → 親ノードID（根は含めない）。ResearchRules の前提判定に使う。
    public static IReadOnlyDictionary<string, string> Prerequisites { get { Ensure(); return _prereq; } }

    // 親を辿った段数（表示順の参考）。
    public static int Depth(string id)
    {
        Ensure();
        int d = 0;
        string cur = id;
        int guard = 0;
        while (cur != null && _prereq.TryGetValue(cur, out cur) && guard++ < 64) d++;
        return d;
    }

    // 極座標 → 平面座標（上が0度、時計回り）。
    public static Vector2 Position(ResearchNodeDef def)
    {
        if (def == null) return Vector2.zero;
        float r = Ring0Radius + def.ring * RingStep;
        float rad = def.angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
    }

    // ---------------------------------------------------------------- 構築

    private class Line
    {
        public MagicAttribute attr;
        public string stem;    // 単体系ステム / 属性バフ接尾辞
        public string aoe;     // 全体系ステム
        public string status;  // 状態異常専用魔法ID
        public string buff;    // アクティブバフID
        public string buffLabel;
        public float angle;    // 枝の基準角度
    }

    private static readonly Line[] Lines =
    {
        new Line { attr = MagicAttribute.Fire,    stem = "Fire",    aoe = "Flame",    status = "StatusBurn",       buff = "BuffAtk",    buffLabel = "攻撃力バフ", angle = 0f },
        new Line { attr = MagicAttribute.Thunder, stem = "Thunder", aoe = "Bolt",     status = "StatusShock",      buff = "BuffDef",    buffLabel = "防御力バフ", angle = 72f },
        new Line { attr = MagicAttribute.Wind,    stem = "Wind",    aoe = "Storm",    status = "StatusLaceration", buff = "BuffSpd",    buffLabel = "速さバフ",   angle = 144f },
        new Line { attr = MagicAttribute.Light,   stem = "Light",   aoe = "Shine",    status = "StatusDizzy",      buff = "BuffLuc",    buffLabel = "運バフ",     angle = 216f },
        new Line { attr = MagicAttribute.Dark,    stem = "Dark",    aoe = "Darkness", status = "StatusBlind",      buff = "BuffDamage", buffLabel = "ダメージバフ", angle = 288f },
    };

    private static MaterialCost Sm(int n) { return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n }; }
    private static MaterialCost Md(int n) { return new MaterialCost { materialType = MaterialType.MediumManaCrystal, amount = n }; }

    private static void Ensure()
    {
        if (_nodes != null) return;

        _nodes = new List<ResearchNodeDef>();
        _byId = new Dictionary<string, ResearchNodeDef>();
        _prereq = new Dictionary<string, string>();

        // 属性ラインの「間」の角度へ、小ノードだけの扇（15ノード×5＝75）を配置する。
        // ライン枝と同じ ring7 までで収め、全体が円盤状になるように隙間を埋める（BuildSpoke）。
        // 偶数index=主ステータス / 奇数index=副ステータスで全ステータス種を網羅する。
        BuildSpoke("ManaRegen", 36f, ResearchStat.ManaRegen, ResearchStat.ManaMax, "Fire");
        BuildSpoke("ManaMax", 108f, ResearchStat.ManaMax, ResearchStat.ManaRegen, "Thunder");
        BuildSpoke("Vitality", 180f, ResearchStat.Hp, ResearchStat.Def, "Wind");
        BuildSpoke("Celerity", 252f, ResearchStat.Spd, ResearchStat.Luc, "Light");
        BuildSpoke("Fortune", 324f, ResearchStat.Luc, ResearchStat.Atk, "Dark");

        foreach (Line l in Lines)
        {
            float a = l.angle;

            // 根：単体基本 / 全体基本（コスト0・初期解放）
            Magic(l.stem, null, 1, a - 10f);
            Magic(l.aoe, null, 1, a + 10f);

            // 枝の途中の小ノード（単体: e→a→[メガ]→c→[ギガ] / 全体: b→[メガ]→d→[ギガ]）
            ResearchStat s0 = StatFor(l.attr, 0);
            ResearchStat s1 = StatFor(l.attr, 1);
            ResearchStat s2 = StatFor(l.attr, 2);
            ResearchStat s3 = StatFor(l.attr, 3);
            ResearchStat s4 = StatFor(l.attr, 4);

            Stat("node_" + l.stem + "_e", s4, Amount(s4), l.stem, 2, a - 10f, Label(s4), Title(s4), Sm(2));
            Stat("node_" + l.stem + "_a", s0, Amount(s0), "node_" + l.stem + "_e", 3, a - 10f, Label(s0), Title(s0), Sm(3), Sm(1));
            Magic("Mega" + l.stem, "node_" + l.stem + "_a", 4, a - 10f);
            Stat("node_" + l.stem + "_c", s2, Amount(s2), "Mega" + l.stem, 5, a - 10f, Label(s2), Title(s2), Md(1));
            Magic("Giga" + l.stem, "node_" + l.stem + "_c", 6, a - 10f);

            Stat("node_" + l.stem + "_b", s1, Amount(s1), l.aoe, 3, a + 10f, Label(s1), Title(s1), Sm(3));
            Magic("Mega" + l.aoe, "node_" + l.stem + "_b", 4, a + 10f);
            Stat("node_" + l.stem + "_d", s3, Amount(s3), "Mega" + l.aoe, 5, a + 10f, Label(s3), Title(s3), Md(1));
            Magic("Giga" + l.aoe, "node_" + l.stem + "_d", 6, a + 10f);

            // 状態異常特化 ← 単体メガ、付与率バフ ← 状態異常特化
            Magic(l.status, "Mega" + l.stem, 5, a - 20f);
            Magic("StatusRateBuff" + l.stem, l.status, 7, a - 20f);

            // 属性バフ ← 単体メガ
            Magic("AttrBuff" + l.stem, "Mega" + l.stem, 6, a - 16f);

            // アクティブバフ ← 単体基本、パッシブ Lv1←バフ→Lv2→Lv3
            Magic(l.buff, l.stem, 4, a + 20f);
            Magic(l.buff + "PassiveLv1", l.buff, 5, a + 20f);
            Magic(l.buff + "PassiveLv2", l.buff + "PassiveLv1", 6, a + 21f);
            Magic(l.buff + "PassiveLv3", l.buff + "PassiveLv2", 7, a + 22f);
        }

        // 共通の補助魔法：マナリジェネ扇の中腹から、扇の空き角へ伸ばす
        Magic("AddSpell", "nsp_ManaRegen_8", 5, 34f);
        Magic("DualSpell", "AddSpell", 6, 34f);
    }

    private static void Magic(string id, string parentId, int ring, float angleDeg)
    {
        ResearchNodeDef d = new ResearchNodeDef
        {
            id = id, isMagic = true, parentId = parentId, ring = ring, angleDeg = angleDeg,
        };
        _nodes.Add(d);
        _byId[id] = d;
        if (parentId != null) _prereq[id] = parentId;
    }

    // 属性ラインの間へ広がる小ノード15個の「扇」。centerAngle を中心に ±10° の楔へ収め、
    // ring0..ring7（ライン枝と同じ深さ）で末広がりに枝分かれさせる。index0 の親は属性基本魔法。
    // 偶数index=primary / 奇数index=secondary ステータス。
    private static readonly int[] SpokeRing = { 0, 1, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 6, 6, 7 };
    private static readonly float[] SpokeAngleOffset = { 0f, 0f, -8f, 8f, -10f, 0f, 10f, -10f, 0f, 10f, -8f, 8f, -8f, 8f, 0f };
    private static readonly int[] SpokeParentIndex = { -1, 0, 1, 1, 2, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12 };

    private static void BuildSpoke(string key, float centerAngle, ResearchStat primary, ResearchStat secondary, string rootParentMagic)
    {
        string[] ids = new string[SpokeRing.Length];
        for (int i = 0; i < SpokeRing.Length; i++)
        {
            string id = "nsp_" + key + "_" + i;
            ids[i] = id;
            ResearchStat st = (i % 2 == 0) ? primary : secondary;
            string parent = SpokeParentIndex[i] < 0 ? rootParentMagic : ids[SpokeParentIndex[i]];
            Stat(id, st, Amount(st), parent, SpokeRing[i], centerAngle + SpokeAngleOffset[i],
                Label(st), Title(st), Sm(2 + SpokeRing[i]));
        }
    }

    private static void Stat(string id, ResearchStat stat, float amount, string parentId, int ring, float angleDeg,
        string shortLabel, string title, params MaterialCost[] cost)
    {
        ResearchNodeDef d = new ResearchNodeDef
        {
            id = id, isMagic = false, parentId = parentId, ring = ring, angleDeg = angleDeg,
            stat = stat, statAmount = amount, shortLabel = shortLabel, title = title,
            cost = new List<MaterialCost>(cost),
        };
        _nodes.Add(d);
        _byId[id] = d;
        if (parentId != null) _prereq[id] = parentId;
    }

    // 属性の枝ごとに小ノードのステータス種を割り振る（全7種がまんべんなく出るように）
    private static ResearchStat StatFor(MagicAttribute attr, int slot)
    {
        switch (attr)
        {
            case MagicAttribute.Fire:    return new[] { ResearchStat.Atk, ResearchStat.Hp, ResearchStat.Atk, ResearchStat.Hp, ResearchStat.Atk }[slot];
            case MagicAttribute.Thunder: return new[] { ResearchStat.Def, ResearchStat.ManaRegen, ResearchStat.Def, ResearchStat.ManaMax, ResearchStat.Def }[slot];
            case MagicAttribute.Wind:    return new[] { ResearchStat.Spd, ResearchStat.Spd, ResearchStat.Luc, ResearchStat.Spd, ResearchStat.Luc }[slot];
            case MagicAttribute.Light:   return new[] { ResearchStat.Luc, ResearchStat.ManaMax, ResearchStat.Luc, ResearchStat.Hp, ResearchStat.ManaMax }[slot];
            case MagicAttribute.Dark:    return new[] { ResearchStat.ManaMax, ResearchStat.Hp, ResearchStat.ManaRegen, ResearchStat.Def, ResearchStat.Hp }[slot];
            default:                     return ResearchStat.Hp;
        }
    }

    private static float Amount(ResearchStat s)
    {
        switch (s)
        {
            case ResearchStat.Hp: return 15f;
            case ResearchStat.Atk: return 1f;
            case ResearchStat.Def: return 1f;
            case ResearchStat.Spd: return 1f;
            case ResearchStat.Luc: return 1f;
            case ResearchStat.ManaMax: return 20f;
            case ResearchStat.ManaRegen: return 1.5f;
            default: return 0f;
        }
    }

    public static string Label(ResearchStat s)
    {
        switch (s)
        {
            case ResearchStat.Hp: return "HP+15";
            case ResearchStat.Atk: return "攻+1";
            case ResearchStat.Def: return "防+1";
            case ResearchStat.Spd: return "速+1";
            case ResearchStat.Luc: return "運+1";
            case ResearchStat.ManaMax: return "MP上限+20";
            case ResearchStat.ManaRegen: return "MPリジェネ+1.5";
            default: return "";
        }
    }

    private static string Title(ResearchStat s)
    {
        switch (s)
        {
            case ResearchStat.Hp: return "最大HP +15";
            case ResearchStat.Atk: return "攻撃力 +1";
            case ResearchStat.Def: return "防御力 +1";
            case ResearchStat.Spd: return "速さ +1（発動間隔が縮む）";
            case ResearchStat.Luc: return "運 +1（会心率が上がる）";
            case ResearchStat.ManaMax: return "最大マナ +20";
            case ResearchStat.ManaRegen: return "マナのリジェネ速度 +1.5";
            default: return "";
        }
    }
}
