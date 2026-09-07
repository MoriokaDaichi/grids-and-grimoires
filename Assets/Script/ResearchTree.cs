using System.Collections.Generic;

// 研究（スキルツリー）の前提関係。企画書5章「魔法の系統ツリー構造」に沿って、
// 各属性が [単体: 基本→メガ→ギガ] [全体: 基本→メガ→ギガ] [状態異常特化] [アクティブバフ]
// [パッシブ3段階] [属性バフ / 状態異常付与率バフ] を順番に解放していく木構造を定義する。
//
// ・単体/全体の基本（ファイア/フレイム 等）はコスト0の初期解放＝木の根。
// ・魔法ID = MagicData のアセット名（MagicDataGenerator の FileId と一致）。
public static class ResearchTree
{
    private class Line
    {
        public string stem;   // 属性ステム: Fire/Thunder/Wind/Light/Dark（単体基本名・属性バフ接尾辞と共通）
        public string aoe;    // 全体基本名: Flame/Bolt/Storm/Shine/Darkness
        public string status; // 状態異常専用魔法ID: StatusBurn 等
        public string buff;   // アクティブバフID: BuffAtk 等
    }

    private static readonly Line[] Lines =
    {
        new Line { stem = "Fire",    aoe = "Flame",    status = "StatusBurn",       buff = "BuffAtk" },
        new Line { stem = "Thunder", aoe = "Bolt",     status = "StatusShock",      buff = "BuffDef" },
        new Line { stem = "Wind",    aoe = "Storm",    status = "StatusLaceration", buff = "BuffSpd" },
        new Line { stem = "Light",   aoe = "Shine",    status = "StatusDizzy",      buff = "BuffLuc" },
        new Line { stem = "Dark",    aoe = "Darkness", status = "StatusBlind",      buff = "BuffDamage" },
    };

    private static Dictionary<string, string> _cache;

    // 魔法ID → 直近の前提魔法ID。前提が無いもの（木の根）はキーに含めない。
    public static IReadOnlyDictionary<string, string> Prerequisites
    {
        get { return _cache ?? (_cache = Build()); }
    }

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>();

        foreach (Line l in Lines)
        {
            // 単体: 基本 → メガ → ギガ
            map["Mega" + l.stem] = l.stem;
            map["Giga" + l.stem] = "Mega" + l.stem;

            // 全体: 基本 → メガ → ギガ
            map["Mega" + l.aoe] = l.aoe;
            map["Giga" + l.aoe] = "Mega" + l.aoe;

            // 状態異常特化 ← 単体メガ
            map[l.status] = "Mega" + l.stem;

            // アクティブバフ ← 単体基本
            map[l.buff] = l.stem;

            // パッシブ Lv1 ← アクティブバフ、Lv2 ← Lv1、Lv3 ← Lv2
            map[l.buff + "PassiveLv1"] = l.buff;
            map[l.buff + "PassiveLv2"] = l.buff + "PassiveLv1";
            map[l.buff + "PassiveLv3"] = l.buff + "PassiveLv2";

            // 属性バフ ← 単体メガ、状態異常付与率バフ ← 状態異常特化
            map["AttrBuff" + l.stem] = "Mega" + l.stem;
            map["StatusRateBuff" + l.stem] = l.status;
        }

        // 補助魔法: デュアルスペル ← アッドスペル（アッドスペルは重い素材コストのみ）
        map["DualSpell"] = "AddSpell";

        return map;
    }

    // 木の深さ（根から何段目か）。表示の並び順に使う。
    public static int Depth(string magicId)
    {
        int depth = 0;
        string cur = magicId;
        int guard = 0;
        while (cur != null && Prerequisites.TryGetValue(cur, out cur) && guard++ < 64)
        {
            depth++;
        }
        return depth;
    }
}
