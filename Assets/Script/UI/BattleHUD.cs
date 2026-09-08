using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 戦闘画面の表示専用コンポーネント。ゲームロジックは持たず、イベントを購読して
// 敵/プレイヤーのHPバー、ダメージ数字、状態異常アイコン、バフインジケータ、ウェーブ表示を更新する。
// 複数敵ウェーブでは「先頭の生存個体」を代表として表示し、残り体数を別途表示する（N体分の個別ウィジェットは今後）。
public class BattleHUD : MonoBehaviour
{
    [Header("敵（代表個体）")]
    [SerializeField] private Image enemySpriteImage;
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private Image enemyHpFill;
    [SerializeField] private TMP_Text enemyHpText;
    [SerializeField] private TMP_Text enemyCountText;
    [SerializeField] private RectTransform enemyStatusIconRoot;

    [Header("プレイヤー")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private TMP_Text playerHpText;
    [SerializeField] private Image playerManaFill;
    [SerializeField] private TMP_Text playerManaText;

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
    private EnemyRoster roster;
    private BattleManager battle;
    private DungeonManager dungeon;

    private EnemyStatus primary; // 現在表示中の代表個体

    private readonly Dictionary<StatusEffectType, StatusEffectIcon> statusIcons = new Dictionary<StatusEffectType, StatusEffectIcon>();
    private readonly Dictionary<BuffStat, BuffIndicator> buffIndicators = new Dictionary<BuffStat, BuffIndicator>();
    private float castLogHideAt;

    void Awake()
    {
        player = Object.FindFirstObjectByType<PlayerStatus>();
        roster = Object.FindFirstObjectByType<EnemyRoster>();
        battle = Object.FindFirstObjectByType<BattleManager>();
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
    }

    void OnEnable()
    {
        if (roster != null) roster.OnRosterChanged += HandleRosterChanged;
        if (player != null)
        {
            player.OnStatusChanged += RefreshPlayerHp;
            player.OnDamaged += HandlePlayerDamaged;
            player.OnManaChanged += RefreshPlayerMana;
        }
        if (battle != null)
        {
            battle.OnAttackHit += HandleAttackHit;
            battle.OnCastFired += HandleCastFired;
            battle.OnBuffApplied += HandleBuffApplied;
            battle.OnBuffExpired += HandleBuffExpired;
            battle.OnManaStarved += HandleManaStarved;
        }
        if (dungeon != null)
        {
            dungeon.OnWaveChanged += HandleWaveChanged;
        }

        HandleRosterChanged();
        RefreshPlayerHp();
        RefreshPlayerMana();
        if (castLogText != null) castLogText.text = "";
    }

    void OnDisable()
    {
        if (roster != null) roster.OnRosterChanged -= HandleRosterChanged;
        BindPrimary(null);

        if (player != null)
        {
            player.OnStatusChanged -= RefreshPlayerHp;
            player.OnDamaged -= HandlePlayerDamaged;
            player.OnManaChanged -= RefreshPlayerMana;
        }
        if (battle != null)
        {
            battle.OnAttackHit -= HandleAttackHit;
            battle.OnCastFired -= HandleCastFired;
            battle.OnBuffApplied -= HandleBuffApplied;
            battle.OnBuffExpired -= HandleBuffExpired;
            battle.OnManaStarved -= HandleManaStarved;
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

    // --- 代表個体のバインド ---

    private void HandleRosterChanged()
    {
        EnemyStatus next = roster != null ? roster.FirstAlive() : null;
        BindPrimary(next);

        int alive = roster != null ? roster.AliveCount() : 0;
        if (enemyCountText != null)
        {
            bool show = alive > 1;
            enemyCountText.gameObject.SetActive(show);
            if (show) enemyCountText.text = "残り " + alive + " 体";
        }
    }

    private void BindPrimary(EnemyStatus next)
    {
        if (next == primary) return;

        if (primary != null)
        {
            primary.OnStatusChanged -= RefreshEnemyHp;
            primary.OnStatusApplied -= HandleStatusApplied;
            primary.OnStatusExpired -= HandleStatusExpired;
            primary.OnStatusTick -= HandleStatusTick;
        }

        primary = next;
        ClearStatusIcons();

        if (primary != null)
        {
            primary.OnStatusChanged += RefreshEnemyHp;
            primary.OnStatusApplied += HandleStatusApplied;
            primary.OnStatusExpired += HandleStatusExpired;
            primary.OnStatusTick += HandleStatusTick;

            if (enemyNameText != null) enemyNameText.text = primary.enemyName;
            ApplyEnemySprite(primary.sprite);
        }
        RefreshEnemyHp();
    }

    // --- HP ---

    private void RefreshEnemyHp()
    {
        if (primary == null)
        {
            if (enemyHpFill != null) enemyHpFill.fillAmount = 0f;
            if (enemyHpText != null) enemyHpText.text = "";
            return;
        }
        float ratio = primary.maxHp > 0 ? (float)primary.hp / primary.maxHp : 0f;
        if (enemyHpFill != null) enemyHpFill.fillAmount = Mathf.Clamp01(ratio);
        if (enemyHpText != null) enemyHpText.text = Mathf.Max(0, primary.hp) + " / " + primary.maxHp;
    }

    private void RefreshPlayerHp()
    {
        if (player == null) return;
        float ratio = player.hp > 0 ? (float)player.currentHp / player.hp : 0f;
        if (playerHpFill != null) playerHpFill.fillAmount = Mathf.Clamp01(ratio);
        if (playerHpText != null) playerHpText.text = Mathf.Max(0, player.currentHp) + " / " + player.hp;
    }

    private void RefreshPlayerMana()
    {
        if (player == null) return;
        float ratio = player.maxMana > 0 ? player.currentMana / player.maxMana : 0f;
        if (playerManaFill != null) playerManaFill.fillAmount = Mathf.Clamp01(ratio);
        if (playerManaText != null)
            playerManaText.text = Mathf.FloorToInt(Mathf.Max(0f, player.currentMana)) + " / " + player.maxMana;
    }

    private void HandleManaStarved(MagicData spell)
    {
        if (castLogText == null) return;
        castLogText.text = "マナ不足…";
        castLogHideAt = Time.time + castLogVisibleSeconds;
    }

    // --- 状態異常（代表個体） ---

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
        RefreshEnemyHp();
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

    private void HandleWaveChanged(int waveNo, int total, string label)
    {
        if (waveText != null) waveText.text = total < 0 ? "深度 " + waveNo : waveNo + " / " + total;
        if (enemyNameText != null && primary != null) enemyNameText.text = primary.enemyName;
        if (primary != null) ApplyEnemySprite(primary.sprite);
        HandleRosterChanged();
        RefreshEnemyHp();
    }

    private void ApplyEnemySprite(Sprite s)
    {
        if (enemySpriteImage == null) return;
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
