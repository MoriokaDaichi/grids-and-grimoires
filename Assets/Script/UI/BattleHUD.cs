using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 戦闘画面の表示専用コンポーネント。ゲームロジックは持たず、既存のイベントを購読して
// 敵/プレイヤーのHPバー、ダメージ数字、状態異常アイコン、バフインジケータ、ウェーブ表示を更新する。
public class BattleHUD : MonoBehaviour
{
    [Header("敵")]
    [SerializeField] private Image enemySpriteImage;
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private Image enemyHpFill;
    [SerializeField] private TMP_Text enemyHpText;
    [SerializeField] private RectTransform enemyStatusIconRoot;

    [Header("プレイヤー")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private TMP_Text playerHpText;

    [Header("バフ")]
    [SerializeField] private RectTransform buffIconRoot;

    [Header("ウェーブ / ログ")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text castLogText;
    [SerializeField] private float castLogVisibleSeconds = 1.2f;

    [Header("ダメージ数字")]
    [SerializeField] private RectTransform damageNumberRoot;
    [SerializeField] private RectTransform enemyDamageAnchor;
    [SerializeField] private RectTransform playerDamageAnchor;

    [Header("プレハブ")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private GameObject statusIconPrefab;
    [SerializeField] private GameObject buffIndicatorPrefab;

    private PlayerStatus player;
    private EnemyStatus enemy;
    private BattleManager battle;
    private DungeonManager dungeon;

    private readonly Dictionary<StatusEffectType, StatusEffectIcon> statusIcons = new Dictionary<StatusEffectType, StatusEffectIcon>();
    private readonly Dictionary<BuffStat, BuffIndicator> buffIndicators = new Dictionary<BuffStat, BuffIndicator>();
    private float castLogHideAt;

    void Awake()
    {
        player = Object.FindFirstObjectByType<PlayerStatus>();
        enemy = Object.FindFirstObjectByType<EnemyStatus>();
        battle = Object.FindFirstObjectByType<BattleManager>();
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
    }

    void OnEnable()
    {
        if (enemy != null)
        {
            enemy.OnStatusChanged += RefreshEnemyHp;
            enemy.OnStatusApplied += HandleStatusApplied;
            enemy.OnStatusExpired += HandleStatusExpired;
            enemy.OnStatusTick += HandleStatusTick;
        }
        if (player != null)
        {
            player.OnStatusChanged += RefreshPlayerHp;
            player.OnDamaged += HandlePlayerDamaged;
        }
        if (battle != null)
        {
            battle.OnAttackHit += HandleAttackHit;
            battle.OnCastFired += HandleCastFired;
            battle.OnBuffApplied += HandleBuffApplied;
            battle.OnBuffExpired += HandleBuffExpired;
        }
        if (dungeon != null)
        {
            dungeon.OnWaveChanged += HandleWaveChanged;
        }

        RefreshEnemyHp();
        RefreshPlayerHp();
        if (castLogText != null) castLogText.text = "";
    }

    void OnDisable()
    {
        if (enemy != null)
        {
            enemy.OnStatusChanged -= RefreshEnemyHp;
            enemy.OnStatusApplied -= HandleStatusApplied;
            enemy.OnStatusExpired -= HandleStatusExpired;
            enemy.OnStatusTick -= HandleStatusTick;
        }
        if (player != null)
        {
            player.OnStatusChanged -= RefreshPlayerHp;
            player.OnDamaged -= HandlePlayerDamaged;
        }
        if (battle != null)
        {
            battle.OnAttackHit -= HandleAttackHit;
            battle.OnCastFired -= HandleCastFired;
            battle.OnBuffApplied -= HandleBuffApplied;
            battle.OnBuffExpired -= HandleBuffExpired;
        }
        if (dungeon != null)
        {
            dungeon.OnWaveChanged -= HandleWaveChanged;
        }

        ClearStatusIcons();
        ClearBuffIndicators();
    }

    void Update()
    {
        if (castLogText != null && castLogText.text.Length > 0 && Time.time >= castLogHideAt)
        {
            castLogText.text = "";
        }
    }

    // --- HP ---

    private void RefreshEnemyHp()
    {
        if (enemy == null) return;
        float ratio = enemy.maxHp > 0 ? (float)enemy.hp / enemy.maxHp : 0f;
        if (enemyHpFill != null) enemyHpFill.fillAmount = Mathf.Clamp01(ratio);
        if (enemyHpText != null) enemyHpText.text = Mathf.Max(0, enemy.hp) + " / " + enemy.maxHp;
    }

    private void RefreshPlayerHp()
    {
        if (player == null) return;
        float ratio = player.hp > 0 ? (float)player.currentHp / player.hp : 0f;
        if (playerHpFill != null) playerHpFill.fillAmount = Mathf.Clamp01(ratio);
        if (playerHpText != null) playerHpText.text = Mathf.Max(0, player.currentHp) + " / " + player.hp;
    }

    // --- 状態異常 ---

    private void HandleStatusApplied(StatusEffectType type)
    {
        if (statusIconPrefab == null || enemyStatusIconRoot == null) return;
        if (statusIcons.ContainsKey(type)) return;

        GameObject go = Instantiate(statusIconPrefab, enemyStatusIconRoot, false);
        StatusEffectIcon icon = go.GetComponent<StatusEffectIcon>();
        if (icon != null)
        {
            icon.Bind(type);
            statusIcons[type] = icon;
        }
    }

    private void HandleStatusExpired(StatusEffectType type)
    {
        if (statusIcons.TryGetValue(type, out StatusEffectIcon icon))
        {
            if (icon != null) Destroy(icon.gameObject);
            statusIcons.Remove(type);
        }
    }

    private void HandleStatusTick(StatusEffectType type, int damage)
    {
        SpawnDamageNumber(damage, StatusEffectIcon.TintOf(type), enemyDamageAnchor);
    }

    // --- ダメージ数字 ---

    private void HandleAttackHit(MagicData spell, int damage)
    {
        Color c = spell != null ? spell.pieceColor : Color.white;
        c.a = 1f;
        SpawnDamageNumber(damage, c, enemyDamageAnchor);
    }

    private void HandlePlayerDamaged(int amount, int currentHpAfter)
    {
        SpawnDamageNumber(amount, new Color(0.95f, 0.3f, 0.3f), playerDamageAnchor);
    }

    private void SpawnDamageNumber(int amount, Color color, RectTransform anchor)
    {
        if (damageNumberPrefab == null || damageNumberRoot == null) return;

        GameObject go = Instantiate(damageNumberPrefab, damageNumberRoot, false);
        RectTransform rt = (RectTransform)go.transform;
        Vector2 basePos = anchor != null ? anchor.anchoredPosition : Vector2.zero;
        rt.anchoredPosition = basePos + new Vector2(Random.Range(-20f, 20f), Random.Range(-10f, 10f));

        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn != null) dn.Play(amount, color);
    }

    // --- キャストログ ---

    private void HandleCastFired(MagicData spell)
    {
        if (castLogText == null || spell == null) return;
        castLogText.text = spell.magicName + " 発動";
        castLogHideAt = Time.time + castLogVisibleSeconds;
    }

    // --- バフ ---

    private void HandleBuffApplied(BuffStat stat, float duration)
    {
        if (buffIndicators.TryGetValue(stat, out BuffIndicator existing) && existing != null)
        {
            existing.Refresh(duration);
            return;
        }
        if (buffIndicatorPrefab == null || buffIconRoot == null) return;

        GameObject go = Instantiate(buffIndicatorPrefab, buffIconRoot, false);
        BuffIndicator indicator = go.GetComponent<BuffIndicator>();
        if (indicator != null)
        {
            indicator.Bind(stat, duration);
            buffIndicators[stat] = indicator;
        }
    }

    private void HandleBuffExpired(BuffStat stat)
    {
        if (buffIndicators.TryGetValue(stat, out BuffIndicator indicator))
        {
            if (indicator != null) Destroy(indicator.gameObject);
            buffIndicators.Remove(stat);
        }
    }

    // --- ウェーブ ---

    private void HandleWaveChanged(int waveNo, int total, string enemyName)
    {
        if (waveText != null) waveText.text = waveNo + " / " + total;
        if (enemyNameText != null) enemyNameText.text = enemyName;

        if (enemySpriteImage != null)
        {
            Sprite s = null;
            int idx = waveNo - 1;
            if (dungeon != null && idx >= 0 && idx < dungeon.encounters.Count && dungeon.encounters[idx] != null)
            {
                s = dungeon.encounters[idx].sprite;
            }

            if (s != null)
            {
                enemySpriteImage.sprite = s;
                enemySpriteImage.color = Color.white;
            }
            else
            {
                enemySpriteImage.sprite = null;
                enemySpriteImage.color = new Color(0.5f, 0.5f, 0.55f); // アート未設定時のグレー矩形
            }
            enemySpriteImage.enabled = true;
        }

        ClearStatusIcons();
        RefreshEnemyHp();
    }

    private void ClearStatusIcons()
    {
        foreach (KeyValuePair<StatusEffectType, StatusEffectIcon> kv in statusIcons)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        statusIcons.Clear();
    }

    private void ClearBuffIndicators()
    {
        foreach (KeyValuePair<BuffStat, BuffIndicator> kv in buffIndicators)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        buffIndicators.Clear();
    }
}
