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
    public const float Ring0Radius = 260f;
    public const float RingStep = 380f;

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

        // 中心付近の小ノード（マナ・汎用ステータス）。親は各属性の単体基本魔法。
        Stat("node_core_manaRegen", ResearchStat.ManaRegen, 1.5f, "Fire", 0, 36f, "MPリジェネ+1.5", "マナのリジェネ速度 +1.5", Sm(2), Sm(1));
        Stat("node_core_manaMax", ResearchStat.ManaMax, 20f, "Thunder", 0, 108f, "MP上限+20", "最大マナ +20", Sm(3));
        Stat("node_core_hp", ResearchStat.Hp, 15f, "Wind", 0, 180f, "HP+15", "最大HP +15", Sm(2), Sm(1));
        Stat("node_core_spd", ResearchStat.Spd, 1f, "Light", 0, 252f, "速さ+1", "速さ +1（発動間隔が縮む）", Sm(2));
        Stat("node_core_luc", ResearchStat.Luc, 1f, "Dark", 0, 324f, "運+1", "運 +1（会心率が上がる）", Sm(2));

        // 中心リングの第2段（各コア小ノードから1つ外へ伸ばす）
        Stat("node_core_manaRegen2", ResearchStat.ManaRegen, 1.5f, "node_core_manaRegen", 1, 36f, Label(ResearchStat.ManaRegen), Title(ResearchStat.ManaRegen), Sm(3));
        Stat("node_core_manaMax2", ResearchStat.ManaMax, 20f, "node_core_manaMax", 1, 108f, Label(ResearchStat.ManaMax), Title(ResearchStat.ManaMax), Sm(4));
        Stat("node_core_hp2", ResearchStat.Hp, 15f, "node_core_hp", 1, 180f, Label(ResearchStat.Hp), Title(ResearchStat.Hp), Sm(3));
        Stat("node_core_spd2", ResearchStat.Spd, 1f, "node_core_spd", 1, 252f, Label(ResearchStat.Spd), Title(ResearchStat.Spd), Sm(3));
        Stat("node_core_luc2", ResearchStat.Luc, 1f, "node_core_luc", 1, 324f, Label(ResearchStat.Luc), Title(ResearchStat.Luc), Sm(3));

        foreach (Line l in Lines)
        {
            float a = l.angle;

            // 根：単体基本 / 全体基本（コスト0・初期解放）
            Magic(l.stem, null, 1, a - 12f);
            Magic(l.aoe, null, 1, a + 12f);

            // 枝の途中の小ノード
            ResearchStat s1 = StatFor(l.attr, 0);
            ResearchStat s2 = StatFor(l.attr, 1);
            ResearchStat s3 = StatFor(l.attr, 2);
            Stat("node_" + l.stem + "_a", s1, Amount(s1), l.stem, 2, a - 12f, Label(s1), Title(s1), Sm(2), Sm(1));
            Stat("node_" + l.stem + "_b", s2, Amount(s2), l.aoe, 2, a + 12f, Label(s2), Title(s2), Sm(3));

            // 単体: 基本 → [小] → メガ → [小] → ギガ
            Magic("Mega" + l.stem, "node_" + l.stem + "_a", 3, a - 12f);
            Stat("node_" + l.stem + "_c", s3, Amount(s3), "Mega" + l.stem, 4, a - 12f, Label(s3), Title(s3), Md(1));
            Magic("Giga" + l.stem, "node_" + l.stem + "_c", 5, a - 12f);

            // 全体: 基本 → [小] → メガ → [小] → ギガ
            ResearchStat s4 = StatFor(l.attr, 3);
            Magic("Mega" + l.aoe, "node_" + l.stem + "_b", 3, a + 12f);
            Stat("node_" + l.stem + "_d", s4, Amount(s4), "Mega" + l.aoe, 4, a + 12f, Label(s4), Title(s4), Md(1));
            Magic("Giga" + l.aoe, "node_" + l.stem + "_d", 5, a + 12f);

            // 状態異常特化 ← 単体メガ、付与率バフ ← 状態異常特化
            Magic(l.status, "Mega" + l.stem, 4, a - 28f);
            Magic("StatusRateBuff" + l.stem, l.status, 6, a - 28f);

            // 属性バフ ← 単体メガ
            Magic("AttrBuff" + l.stem, "Mega" + l.stem, 5, a - 20f);

            // アクティブバフ ← 単体基本、パッシブ Lv1←バフ→Lv2→Lv3
            Magic(l.buff, l.stem, 3, a + 28f);
            Magic(l.buff + "PassiveLv1", l.buff, 4, a + 28f);
            Magic(l.buff + "PassiveLv2", l.buff + "PassiveLv1", 5, a + 29f);
            Magic(l.buff + "PassiveLv3", l.buff + "PassiveLv2", 6, a + 30f);
        }

        // 共通の補助魔法：中心の「効率的魔力運用」＝マナ回復ノードの第2段から伸ばす
        Magic("AddSpell", "node_core_manaRegen2", 3, 44f);
        Magic("DualSpell", "AddSpell", 5, 44f);
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
            case MagicAttribute.Fire:    return new[] { ResearchStat.Atk, ResearchStat.Hp, ResearchStat.Atk, ResearchStat.Hp }[slot];
            case MagicAttribute.Thunder: return new[] { ResearchStat.Def, ResearchStat.ManaRegen, ResearchStat.Def, ResearchStat.ManaMax }[slot];
            case MagicAttribute.Wind:    return new[] { ResearchStat.Spd, ResearchStat.Spd, ResearchStat.Luc, ResearchStat.Spd }[slot];
            case MagicAttribute.Light:   return new[] { ResearchStat.Luc, ResearchStat.ManaMax, ResearchStat.Luc, ResearchStat.Hp }[slot];
            case MagicAttribute.Dark:    return new[] { ResearchStat.ManaMax, ResearchStat.Hp, ResearchStat.ManaRegen, ResearchStat.Def }[slot];
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
