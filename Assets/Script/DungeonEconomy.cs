// ダンジョンの経済（入場料など）を扱う純粋関数。シーン非依存で EditMode テストできる。
// 数値は全て仮（企画書に記載なし）。将来、到達深度などでスケールさせる余地を関数で確保しておく。
public static class DungeonEconomy
{
    // 1回の潜行を始めるのに必要なお金（少額固定）。
    public const int BaseEntryFee = 15;

    public static int EntryFee()
    {
        return BaseEntryFee;
    }
}
