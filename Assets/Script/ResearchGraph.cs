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

// 特性ノード（大ノード。魔法ではない）。研究の深部フロンティアが Atk 一色で
// 投資するほどグラスキャノン化する（検証レポート 2026-09-10 O1）ための対策で、
// 防御5種（Thorns〜LastStand）＋攻撃5種（SpellPower〜Execute）を用意する。
// None = 特性ノードではない（＝魔法 or ステータス小ノード）。
public enum ResearchPerk
{
    None,
    // 防御特性（PlayerStatus が被弾時に適用）
    Thorns, FlatWard, PercentWard, WaveBarrier, LastStand,
    // 攻撃特性（BattleManager が攻撃魔法の発動時に適用）
    SpellPower, CritPower, CastHaste, ArmorPierce, Execute,
}

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

    // 防御特性ノード用（isMagic=false だが大きく表示する。魔法サブセットには入らない）。
    public ResearchPerk perk = ResearchPerk.None;
    public float perkAmount;      // 効果量（割合系は「%ポイント」＝10 なら 10%）
    public string detail;         // 詳細パネル本文（小ノードは null＝title を流用）
    public bool IsPerk { get { return perk != ResearchPerk.None; } }
}

public static class ResearchGraph
{
    // 円盤の半径。ノードは全て overlap しない（ResearchGraphTests.NoTwoNodesOverlap で担保）
    // ので、余白を詰めてパン量を減らす方向で調整している（最外 ring7 ≈ 半径 2680）。
    public const float Ring0Radius = 300f;
    public const float RingStep = 340f;

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
        //
        // 検証レポート 2026-09-10 O1：Hp/Def 寄せのスコアリングにしても、深部フロンティアが
        // 「Atk カラム＋ManaRegen/ManaMax 扇」しか出さず、壁（累積被弾 > 回復）に対して研究で Def/Hp を
        // これ以上伸ばせない。対策として 108° の扇を マナ主体 → 防御主体（主 Def / 副 Hp）に付け替える。
        // マナの投資先は 36° 扇（主 ManaRegen / 副 ManaMax）とライン小ノード（Light_e/b・Dark_a・Thunder_d 等）で残す。
        BuildSpoke("ManaRegen", 36f, ResearchStat.ManaRegen, ResearchStat.ManaMax, "Fire");
        BuildSpoke("Bulwark", 108f, ResearchStat.Def, ResearchStat.Hp, "Thunder");
        BuildSpoke("Vitality", 180f, ResearchStat.Hp, ResearchStat.Def, "Wind");
        BuildSpoke("Celerity", 252f, ResearchStat.Spd, ResearchStat.Luc, "Light");
        // 再検証3〜5 D6：Atk 小ノードが Fire 枝の2個＋この扇の“奇数index（＝副）”しか無く、
        // しかも扇の Atk に辿り着くには Luc ノードを経由するので、min-max プレイでは Atk が
        // ほぼ伸びない（3〜5周で +2〜3）。この扇を Atk 主・Luc 副に入れ替え、根（Dark 基本魔法・
        // コスト0）直下から小結晶だけで Atk 小ノードを繋げられるようにする。
        BuildSpoke("Fortune", 324f, ResearchStat.Atk, ResearchStat.Luc, "Dark");

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
            Stat("node_" + l.stem + "_c", s2, Amount(s2), "Mega" + l.stem, 5, a - 10f, Label(s2), Title(s2), Sm(9));
            Magic("Giga" + l.stem, "node_" + l.stem + "_c", 6, a - 10f);

            Stat("node_" + l.stem + "_b", s1, Amount(s1), l.aoe, 3, a + 10f, Label(s1), Title(s1), Sm(3));
            Magic("Mega" + l.aoe, "node_" + l.stem + "_b", 4, a + 10f);
            Stat("node_" + l.stem + "_d", s3, Amount(s3), "Mega" + l.aoe, 5, a + 10f, Label(s3), Title(s3), Sm(9));
            Magic("Giga" + l.aoe, "node_" + l.stem + "_d", 6, a + 10f);

            // 状態異常特化 ← 単体メガ、付与率バフ ← 状態異常特化
            Magic(l.status, "Mega" + l.stem, 5, a - 20f);
            Magic("StatusRateBuff" + l.stem, l.status, 7, a - 20f);

            // 属性バフ ← 単体メガ（a-15° の攻撃特性列に場所を譲るため a-20° の status 列へ寄せる）
            Magic("AttrBuff" + l.stem, "Mega" + l.stem, 6, a - 20f);

            // アクティブバフ ← 単体基本、パッシブ Lv1←バフ→Lv2→Lv3
            Magic(l.buff, l.stem, 4, a + 20f);
            Magic(l.buff + "PassiveLv1", l.buff, 5, a + 20f);
            Magic(l.buff + "PassiveLv2", l.buff + "PassiveLv1", 6, a + 21f);
            Magic(l.buff + "PassiveLv3", l.buff + "PassiveLv2", 7, a + 22f);

            // 円盤の外周（ring4〜7）は既存枝が status 側（a-20〜a-10）と buff/扇 側（a+20〜a+46）へ
            // 寄っていて、属性ライン中央（a-10〜a+10）と Mega全体〜バフの間（a+10〜a+20）がぽっかり空く。
            // そこへ主ステータス小ノードの放射カラムを4本挿してノード密度を均す（4カラム×4リング×5ライン＝80）。
            // 各カラムは小結晶コストで ring4→7 を一直線に繋ぎ、親は ring3 の単体側小ノード node_*_a。
            // 角度は ±10° の Mega 魔法（半径75）から 4.2°以上（＝92px以上）離れるよう中央帯 a-5〜a+5.5 に寄せる。
            // 検証レポート 2026-09-10 O1（Atk 一色でグラスキャノン化）＆ユーザー要求「Def/Luc も Atk と
            // 同じくらい伸ばす」→ カラムを Atk / Def / Luc / Hp の4種に割り振る（各20ノード＝小結晶だけで到達可）。
            BuildStatColumns(l.stem, a);

            // 検証レポート 2026-09-10 O1：深部フロンティアが Atk 一色。全体メガ（a+10）とパッシブバフ列
            // （a+20〜）の隙間 a+15° へ「防御特性」の大ノード列（ring5→7・親＝全体メガ）、単体メガ（a-10）と
            // status 列（a-20）の隙間 a-15° へ「攻撃特性」の大ノード列（ring5→7・親＝単体メガ）を挿す。
            BuildTraitColumn(l, offensive: false);
            BuildTraitColumn(l, offensive: true);
        }

        // 共通の補助魔法：マナリジェネ扇の中腹から、扇の空き角へ伸ばす
        Magic("AddSpell", "nsp_ManaRegen_8", 5, 34f);
        Magic("DualSpell", "AddSpell", 6, 34f);
    }

    // 属性ラインの中央の空き角（a-5〜a+5.5）へ、主ステータス小ノードの放射カラムを4本ぶん敷く。
    // カラムごとに ring4→ring7 を直列（親＝1つ内側の同カラムノード、根は ring3 の node_*_a）。
    // カラムの種を Atk / Def / Luc / Hp に割り振り、いずれも小結晶だけで各20ノード到達できるようにする
    // （O1 の「Atk 一色」＆ユーザー要求「Def/Luc も Atk と同じくらい伸ばす」への対応）。
    private static readonly float[] StatColAngleOffsets = { -5f, -1.5f, 2f, 5.5f };
    private static readonly ResearchStat[] StatColKinds =
        { ResearchStat.Atk, ResearchStat.Def, ResearchStat.Luc, ResearchStat.Hp };

    private static void BuildStatColumns(string stem, float baseAngle)
    {
        for (int ci = 0; ci < StatColAngleOffsets.Length; ci++)
        {
            ResearchStat st = StatColKinds[ci];
            string parent = "node_" + stem + "_a";
            for (int ring = 4; ring <= 7; ring++)
            {
                string id = "node_col_" + stem + "_c" + ci + "_r" + ring;
                Stat(id, st, Amount(st), parent, ring, baseAngle + StatColAngleOffsets[ci],
                    Label(st), Title(st), Sm(2 + ring));
                parent = id;
            }
        }
    }

    // 属性ラインの外周へ「特性」の大ノード列を ring5→7 で1本ぶん直列に敷く。
    // 防御 = a+15°（全体メガ〜パッシブバフ列の隙間、親＝全体メガ Mega{aoe}）。
    // 攻撃 = a-15°（単体メガ〜status列の隙間、親＝単体メガ Mega{stem}）。
    // ring4 は隣の Mega 魔法（半径75）が近すぎて大ノードが入らないので ring5 から。
    private static void BuildTraitColumn(Line l, bool offensive)
    {
        ResearchPerk perk; float[] tiers; string tag, shortTag, body;
        TraitFor(l.attr, offensive, out perk, out tiers, out tag, out shortTag, out body);

        string idPrefix = (offensive ? "atr_" : "perk_") + l.stem + "_r";
        string parent = offensive ? "Mega" + l.stem : "Mega" + l.aoe;
        float angle = l.angle + (offensive ? -15f : 15f);
        for (int ring = 5; ring <= 7; ring++)
        {
            int t = ring - 5;
            string roman = Roman(t + 1);
            MaterialCost[] cost = ring >= 6
                ? new[] { Sm(2 + ring), Md(1) }
                : new[] { Sm(2 + ring) };
            Perk(idPrefix + ring, parent, ring, angle, perk, tiers[t],
                shortTag + " " + roman, tag + " " + roman,
                string.Format(body, tiers[t]), cost);
            parent = idPrefix + ring;
        }
    }

    private static void TraitFor(MagicAttribute attr, bool offensive, out ResearchPerk perk,
        out float[] tiers, out string tag, out string shortTag, out string body)
    {
        if (offensive) OffensiveTraitFor(attr, out perk, out tiers, out tag, out shortTag, out body);
        else DefensiveTraitFor(attr, out perk, out tiers, out tag, out shortTag, out body);
    }

    private static void DefensiveTraitFor(MagicAttribute attr, out ResearchPerk perk, out float[] tiers,
        out string tag, out string shortTag, out string body)
    {
        switch (attr)
        {
            case MagicAttribute.Fire:
                perk = ResearchPerk.Thorns; tiers = new[] { 15f, 20f, 25f };
                tag = "報復のトゲ"; shortTag = "トゲ";
                body = "被弾時、受けた一撃の {0}% を攻撃してきた敵へ反射する。";
                break;
            case MagicAttribute.Thunder:
                perk = ResearchPerk.FlatWard; tiers = new[] { 3f, 4f, 5f };
                tag = "鉄壁"; shortTag = "鉄壁";
                body = "敵の攻撃で受けるダメージを常に {0} 軽減する（防御力の素引きに上乗せ）。";
                break;
            case MagicAttribute.Wind:
                perk = ResearchPerk.PercentWard; tiers = new[] { 6f, 8f, 10f };
                tag = "見切り"; shortTag = "見切";
                body = "敵の攻撃で受けるダメージを {0}% 軽減する。";
                break;
            case MagicAttribute.Light:
                perk = ResearchPerk.WaveBarrier; tiers = new[] { 8f, 12f, 16f };
                tag = "聖盾"; shortTag = "聖盾";
                body = "各ウェーブ開始時、最大HPの {0}% ぶんのバリアを張る（HPより先に削れる）。";
                break;
            default: // Dark
                perk = ResearchPerk.LastStand; tiers = new[] { 15f, 20f, 25f };
                tag = "不屈"; shortTag = "不屈";
                body = "現在HPが最大HPの35%以下のとき、受けるダメージを {0}% 軽減する。";
                break;
        }
    }

    private static void OffensiveTraitFor(MagicAttribute attr, out ResearchPerk perk, out float[] tiers,
        out string tag, out string shortTag, out string body)
    {
        switch (attr)
        {
            case MagicAttribute.Fire:
                perk = ResearchPerk.SpellPower; tiers = new[] { 8f, 12f, 16f };
                tag = "魔力増幅"; shortTag = "増幅";
                body = "攻撃魔法のダメージを {0}% 上げる。";
                break;
            case MagicAttribute.Thunder:
                perk = ResearchPerk.CritPower; tiers = new[] { 15f, 25f, 35f };
                tag = "痛撃"; shortTag = "痛撃";
                body = "会心ダメージの倍率を +{0}%（基礎 150% に上乗せ）。";
                break;
            case MagicAttribute.Wind:
                perk = ResearchPerk.CastHaste; tiers = new[] { 4f, 6f, 8f };
                tag = "詠唱加速"; shortTag = "加速";
                body = "魔法の発動間隔を {0}% 縮める。";
                break;
            case MagicAttribute.Light:
                perk = ResearchPerk.ArmorPierce; tiers = new[] { 3f, 5f, 8f };
                tag = "貫通"; shortTag = "貫通";
                body = "攻撃魔法が敵の防御力を {0} 無視する。";
                break;
            default: // Dark
                perk = ResearchPerk.Execute; tiers = new[] { 20f, 30f, 40f };
                tag = "処刑"; shortTag = "処刑";
                body = "HPが25%以下の敵へ与えるダメージを {0}% 上げる。";
                break;
        }
    }

    private static string Roman(int n)
    {
        switch (n) { case 1: return "I"; case 2: return "II"; case 3: return "III"; case 4: return "IV"; default: return n.ToString(); }
    }

    private static void Perk(string id, string parentId, int ring, float angleDeg,
        ResearchPerk perk, float amount, string shortLabel, string title, string detail, params MaterialCost[] cost)
    {
        ResearchNodeDef d = new ResearchNodeDef
        {
            id = id, isMagic = false, parentId = parentId, ring = ring, angleDeg = angleDeg,
            perk = perk, perkAmount = amount,
            shortLabel = shortLabel, title = title, detail = detail,
            cost = new List<MaterialCost>(cost),
        };
        _nodes.Add(d);
        _byId[id] = d;
        if (parentId != null) _prereq[id] = parentId;
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
            // 再検証3〜5 D6：Atk 小ノードは Fire 枝＋Fortune 扇の一部しか小結晶で辿れず（他は Luc を
            // 経由するか中結晶ゲートの奥）、min-max でも実質 4本前後で頭打ち。1本あたりを +2 にして
            // 「辿れるぶんだけ振っても Atk が深部スケーリングにある程度ついていく」ようにする（数値は仮）。
            case ResearchStat.Atk: return 2f;
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
            case ResearchStat.Atk: return "攻+2";
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
