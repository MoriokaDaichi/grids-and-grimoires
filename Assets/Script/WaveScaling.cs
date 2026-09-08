// エンドレスダンジョンの深度に応じた敵ステータス倍率。EnemyStatus.Setup に渡す。
public struct WaveScaling
{
    public float hpMult;
    public float atkMult;
    public float defMult;

    public static readonly WaveScaling None = new WaveScaling(1f, 1f, 1f);

    public WaveScaling(float hp, float atk, float def)
    {
        hpMult = hp;
        atkMult = atk;
        defMult = def;
    }
}
