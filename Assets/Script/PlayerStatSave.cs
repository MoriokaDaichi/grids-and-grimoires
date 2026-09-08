// プレイヤーの手動ステータス振り分けの読み書き（シーン非依存・純ロジック・EditModeテスト対象）。
//
// PlayerStatus のステータス値そのものはセーブされない（毎起動、シーンの初期値から
// 研究の小ノード・製作装備ぶんを再適用して組み立てる）。手動振り分けだけはどこにも
// 再現ソースが無いので、その差分（＝ ＋ボタンで振った量 と 残りステP）をここで永続化する。

// SaveData に保存する「プレイヤー振り分け」のスナップショット。
// hp/atk/... は AddStat の累計加算量であって、ステータス値そのものではない。
public struct PlayerStatAllocation
{
    public bool saved;      // SaveData に有効な振り分けが入っていたか
    public int statsPoint;  // 残りステータスポイント
    public int hp, atk, def, spd, luc;
}

public static class PlayerStatSave
{
    public static PlayerStatAllocation Read(SaveData d)
    {
        PlayerStatAllocation a = new PlayerStatAllocation();
        if (d == null || !d.playerStatsSaved) return a; // saved = false
        a.saved = true;
        a.statsPoint = d.savedStatsPoint;
        a.hp = d.manualStatHp;
        a.atk = d.manualStatAtk;
        a.def = d.manualStatDef;
        a.spd = d.manualStatSpd;
        a.luc = d.manualStatLuc;
        return a;
    }

    // SaveData の「プレイヤー振り分け」領域だけを上書きする（他システムのフィールドは触らない）。
    public static void Write(SaveData d, PlayerStatAllocation a)
    {
        if (d == null) return;
        d.playerStatsSaved = true;
        d.savedStatsPoint = a.statsPoint;
        d.manualStatHp = a.hp;
        d.manualStatAtk = a.atk;
        d.manualStatDef = a.def;
        d.manualStatSpd = a.spd;
        d.manualStatLuc = a.luc;
    }
}
