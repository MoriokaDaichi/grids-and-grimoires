using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// SampleScene に戦闘UI(BattleRoot)・報酬UI(RewardRoot)・GamePhaseManager を構築し、
// 出撃ボタンを GamePhaseManager.StartSortie に付け替える Editor 拡張。
// 何度でも再実行できる（既存の生成物を作り直す）。実行後はシーンを保存する。
// メニュー: Grimoire > Build Battle UI
public static class BattleUISceneBuilder
{
    private const string PrefabDir = "Assets/Prefabs";
    private const string JpFontPath = "Assets/NotoSansJP-Light SDF.asset";

    private static TMP_FontAsset _jpFont;
    private static TMP_FontAsset JpFont
    {
        get
        {
            if (_jpFont == null) _jpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFontPath);
            return _jpFont;
        }
    }

    [MenuItem("Grimoire/Build Battle UI")]
    public static void Build()
    {
        EnsurePrefabs();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Grimoire] Canvas がシーンに見つかりません。");
            return;
        }
        Transform canvasT = canvas.transform;

        DestroyExisting(canvasT, "BattleRoot");
        DestroyExisting(canvasT, "WaveClearRoot");
        DestroyExisting(canvasT, "RewardRoot");
        DestroyExisting(canvasT, "InventoryPanel");
        DestroyExisting(canvasT, "HideoutRoot");
        DestroyExisting(canvasT, "ResearchRoot");
        DestroyExisting(canvasT, "HideoutButton");
        DestroyExisting(canvasT, "TradeRoot");
        DestroyExisting(canvasT, "TradeButton");
        GameObject oldGpm = GameObject.Find("GamePhaseManager");
        if (oldGpm != null) Object.DestroyImmediate(oldGpm);

        EnsureSingleton<PlayerInventory>("PlayerInventory");
        EnsureSingleton<MoneyManager>("MoneyManager");
        EnsureSingleton<ResearchManager>("ResearchManager");
        EnsureSingleton<HideoutManager>("HideoutManager");
        EnsureSingleton<TradeManager>("TradeManager");
        EnsureSingleton<EnemyRoster>("EnemyRoster");
        RemoveStandaloneEnemyStatus();

        GameObject battleRoot = BuildBattleRoot(canvasT);
        GameObject waveClearRoot = BuildWaveClearRoot(canvasT);
        GameObject rewardRoot = BuildRewardRoot(canvasT);
        GameObject inventoryPanel = BuildInventoryPanel(canvasT);
        GameObject researchRoot = BuildResearchRoot(canvasT);
        GameObject hideoutRoot = BuildHideoutRoot(canvasT);
        GameObject tradeRoot = BuildTradeRoot(canvasT);

        // シーンの MenuPanel に既にあるボタンを使う（左下のコーナーボタンは作らない）。
        // Button (2) = トレード / Button (3) = ハイドアウト。ラベルはシーン側の指定を尊重して触らない。
        Button tradeButton = BindMenuButton(canvasT, "MenuPanel/Button (2)", null);
        Button hideoutButton = BindMenuButton(canvasT, "MenuPanel/Button (3)", null);

        GameObject gpmGo = new GameObject("GamePhaseManager");
        GamePhaseManager gpm = gpmGo.AddComponent<GamePhaseManager>();

        Button sortie = FindSortieButton();
        if (sortie == null)
        {
            // 再実行時は BattleManager.StartBattle リスナーを既に外しているので、パスで拾う
            Transform t = canvasT.Find("MenuPanel/Button");
            if (t != null) sortie = t.GetComponent<Button>();
        }
        if (sortie != null)
        {
            for (int i = sortie.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (sortie.onClick.GetPersistentTarget(i) is BattleManager)
                    UnityEventTools.RemovePersistentListener(sortie.onClick, i);
            }
            TMP_Text lbl = sortie.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = "出撃";
        }
        else
        {
            Debug.LogWarning("[Grimoire] 出撃ボタンが見つかりませんでした。手動で GamePhaseManager.sortieButton を設定してください。");
        }

        List<GameObject> buildObjs = new List<GameObject>();
        foreach (string n in new[] { "WandPanel", "CharacterPanel", "MenuPanel" })
        {
            Transform t = canvasT.Find(n);
            if (t != null) buildObjs.Add(t.gameObject);
        }
        buildObjs.Add(inventoryPanel);
        // ハイドアウト/トレードボタンは MenuPanel の子なので、MenuPanel と一緒に表示切替される

        SerializedObject so = new SerializedObject(gpm);
        SerializedProperty arr = so.FindProperty("buildPhaseObjects");
        arr.arraySize = buildObjs.Count;
        for (int i = 0; i < buildObjs.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = buildObjs[i];
        so.FindProperty("hideoutRoot").objectReferenceValue = hideoutRoot;
        so.FindProperty("researchRoot").objectReferenceValue = researchRoot;
        so.FindProperty("tradeRoot").objectReferenceValue = tradeRoot;
        so.FindProperty("battleRoot").objectReferenceValue = battleRoot;
        so.FindProperty("waveClearRoot").objectReferenceValue = waveClearRoot;
        so.FindProperty("rewardRoot").objectReferenceValue = rewardRoot;
        so.FindProperty("sortieButton").objectReferenceValue = sortie;
        so.FindProperty("hideoutButton").objectReferenceValue = hideoutButton;
        so.FindProperty("tradeButton").objectReferenceValue = tradeButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        hideoutRoot.SetActive(false);
        researchRoot.SetActive(false);
        tradeRoot.SetActive(false);
        battleRoot.SetActive(false);
        waveClearRoot.SetActive(false);
        rewardRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[Grimoire] Battle UI 構築完了。シーンを保存してください (Ctrl+S)。");
    }

    // ---------------------------------------------------------------- BattleRoot

    private static GameObject BuildBattleRoot(Transform canvas)
    {
        RectTransform root = NewUI("BattleRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.09f, 0.10f, 0.13f, 1f), true);

        // ウェーブ表示（上中央）
        TMP_Text wave = AddText(root, "WaveText", "1 / 4", 30, TextAlignmentOptions.Center);
        Frame(wave.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(300f, 60f));

        // 敵ブロック（上寄り中央）
        RectTransform enemySprite = NewUI("EnemySprite", root);
        Frame(enemySprite, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
        Image enemyImg = AddImage(enemySprite, new Color(0.5f, 0.5f, 0.55f, 1f), false);

        TMP_Text enemyName = AddText(root, "EnemyName", "スライム", 28, TextAlignmentOptions.Center);
        Frame(enemyName.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(360f, 44f));

        RectTransform enemyHpBg = NewUI("EnemyHpBarBG", root);
        Frame(enemyHpBg, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(360f, 26f));
        AddImage(enemyHpBg, new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform enemyHpFillRt = NewUI("EnemyHpFill", enemyHpBg);
        Stretch(enemyHpFillRt);
        Image enemyHpFill = AddImage(enemyHpFillRt, new Color(0.85f, 0.25f, 0.25f, 1f), false);
        MakeHorizontalFill(enemyHpFill);

        TMP_Text enemyHpText = AddText(root, "EnemyHpText", "60 / 60", 18, TextAlignmentOptions.Center);
        Frame(enemyHpText.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(360f, 26f));

        RectTransform statusIconRoot = NewUI("EnemyStatusIconRoot", root);
        Frame(statusIconRoot, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(360f, 48f));
        AddHorizontalLayout(statusIconRoot, 6f);

        // 残り体数（複数敵ウェーブ時のみ表示）
        TMP_Text enemyCount = AddText(root, "EnemyCountText", "残り 3 体", 20, TextAlignmentOptions.Center);
        Frame(enemyCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(300f, 32f));
        enemyCount.color = new Color(1f, 0.8f, 0.5f, 1f);
        enemyCount.gameObject.SetActive(false);

        // キャストログ（中央）
        TMP_Text castLog = AddText(root, "CastLogText", "", 26, TextAlignmentOptions.Center);
        Frame(castLog.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(600f, 50f));
        castLog.color = new Color(1f, 0.95f, 0.7f, 1f);

        // バフ表示（プレイヤーHPバーの上）
        RectTransform buffRoot = NewUI("BuffIconRoot", root);
        Frame(buffRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(480f, 48f));
        AddHorizontalLayout(buffRoot, 6f);

        // プレイヤーHP（下）
        RectTransform playerHpBg = NewUI("PlayerHpBarBG", root);
        Frame(playerHpBg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(560f, 30f));
        AddImage(playerHpBg, new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform playerHpFillRt = NewUI("PlayerHpFill", playerHpBg);
        Stretch(playerHpFillRt);
        Image playerHpFill = AddImage(playerHpFillRt, new Color(0.30f, 0.75f, 0.35f, 1f), false);
        MakeHorizontalFill(playerHpFill);

        TMP_Text playerHpText = AddText(root, "PlayerHpText", "100 / 100", 18, TextAlignmentOptions.Center);
        Frame(playerHpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(560f, 30f));

        // プレイヤーマナ（HPバーの下）
        RectTransform playerManaBg = NewUI("PlayerManaBarBG", root);
        Frame(playerManaBg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(560f, 22f));
        AddImage(playerManaBg, new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform playerManaFillRt = NewUI("PlayerManaFill", playerManaBg);
        Stretch(playerManaFillRt);
        Image playerManaFill = AddImage(playerManaFillRt, new Color(0.30f, 0.55f, 0.95f, 1f), false);
        MakeHorizontalFill(playerManaFill);

        TMP_Text playerManaText = AddText(root, "PlayerManaText", "100 / 100", 14, TextAlignmentOptions.Center);
        Frame(playerManaText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(560f, 22f));

        // ダメージ数字レイヤー
        RectTransform dmgRoot = NewUI("DamageNumberRoot", root);
        Stretch(dmgRoot);
        dmgRoot.GetComponent<Image>();
        RectTransform enemyAnchor = NewUI("EnemyDamageAnchor", dmgRoot);
        Frame(enemyAnchor, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(10f, 10f));
        RectTransform playerAnchor = NewUI("PlayerDamageAnchor", dmgRoot);
        Frame(playerAnchor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(10f, 10f));

        BattleHUD hud = root.gameObject.AddComponent<BattleHUD>();
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("enemySpriteImage").objectReferenceValue = enemyImg;
        so.FindProperty("enemyNameText").objectReferenceValue = enemyName;
        so.FindProperty("enemyHpFill").objectReferenceValue = enemyHpFill;
        so.FindProperty("enemyHpText").objectReferenceValue = enemyHpText;
        so.FindProperty("enemyStatusIconRoot").objectReferenceValue = statusIconRoot;
        so.FindProperty("enemyCountText").objectReferenceValue = enemyCount;
        so.FindProperty("playerHpFill").objectReferenceValue = playerHpFill;
        so.FindProperty("playerHpText").objectReferenceValue = playerHpText;
        so.FindProperty("playerManaFill").objectReferenceValue = playerManaFill;
        so.FindProperty("playerManaText").objectReferenceValue = playerManaText;
        so.FindProperty("buffIconRoot").objectReferenceValue = buffRoot;
        so.FindProperty("waveText").objectReferenceValue = wave;
        so.FindProperty("castLogText").objectReferenceValue = castLog;
        so.FindProperty("damageNumberRoot").objectReferenceValue = dmgRoot;
        so.FindProperty("enemyDamageAnchor").objectReferenceValue = enemyAnchor;
        so.FindProperty("playerDamageAnchor").objectReferenceValue = playerAnchor;
        so.FindProperty("damageNumberPrefab").objectReferenceValue = Load("DamageNumber");
        so.FindProperty("statusIconPrefab").objectReferenceValue = Load("StatusEffectIcon");
        so.FindProperty("buffIndicatorPrefab").objectReferenceValue = Load("BuffIndicator");
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // ---------------------------------------------------------------- WaveClearRoot

    private static GameObject BuildWaveClearRoot(Transform canvas)
    {
        RectTransform root = NewUI("WaveClearRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.07f, 0.10f, 0.11f, 0.96f), true);

        TMP_Text depth = AddText(root, "DepthText", "深度 1 突破", 40, TextAlignmentOptions.Center);
        Frame(depth.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(760f, 70f));

        TMP_Text hint = AddText(root, "HintText", "進むほど敵は強大に。脱出すれば戦利品を持ち帰れる。", 18, TextAlignmentOptions.Center);
        Frame(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 30f));
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        RectTransform listPanel = NewUI("LootListPanel", root);
        Frame(listPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(420f, 280f));
        AddImage(listPanel, new Color(0f, 0f, 0f, 0.35f), false);

        RectTransform listRoot = NewUI("LootListRoot", listPanel);
        Frame(listRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(400f, 260f));
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        ContentSizeFitter csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform contRt = NewUI("ContinueButton", root);
        Frame(contRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-140f, 80f), new Vector2(240f, 64f));
        Image contImg = AddImage(contRt, new Color(0.6f, 0.35f, 0.2f, 1f), true);
        Button contBtn = contRt.gameObject.AddComponent<Button>();
        contBtn.targetGraphic = contImg;
        TMP_Text contLabel = AddText(root, "Label", "深層へ進む", 24, TextAlignmentOptions.Center);
        contLabel.transform.SetParent(contRt, false);
        Stretch(contLabel.rectTransform);

        RectTransform escRt = NewUI("EscapeButton", root);
        Frame(escRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(140f, 80f), new Vector2(240f, 64f));
        Image escImg = AddImage(escRt, new Color(0.25f, 0.4f, 0.7f, 1f), true);
        Button escBtn = escRt.gameObject.AddComponent<Button>();
        escBtn.targetGraphic = escImg;
        TMP_Text escLabel = AddText(root, "Label", "脱出する", 24, TextAlignmentOptions.Center);
        escLabel.transform.SetParent(escRt, false);
        Stretch(escLabel.rectTransform);

        WaveClearPanel panel = root.gameObject.AddComponent<WaveClearPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("depthText").objectReferenceValue = depth;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("lootListRoot").objectReferenceValue = listRoot;
        so.FindProperty("dropRowPrefab").objectReferenceValue = Load("DropRow");
        so.FindProperty("continueButton").objectReferenceValue = contBtn;
        so.FindProperty("escapeButton").objectReferenceValue = escBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // ---------------------------------------------------------------- RewardRoot

    private static GameObject BuildRewardRoot(Transform canvas)
    {
        RectTransform root = NewUI("RewardRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.08f, 0.09f, 0.12f, 1f), true);

        TMP_Text result = AddText(root, "ResultText", "ダンジョンクリア！", 40, TextAlignmentOptions.Center);
        Frame(result.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(700f, 70f));

        RectTransform listPanel = NewUI("DropListPanel", root);
        Frame(listPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(420f, 320f));
        AddImage(listPanel, new Color(0f, 0f, 0f, 0.35f), false);

        RectTransform listRoot = NewUI("DropListRoot", listPanel);
        Frame(listRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(400f, 300f));
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        ContentSizeFitter csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform btnRt = NewUI("ReturnButton", root);
        Frame(btnRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(240f, 64f));
        Image btnImg = AddImage(btnRt, new Color(0.25f, 0.4f, 0.7f, 1f), true);
        Button returnBtn = btnRt.gameObject.AddComponent<Button>();
        returnBtn.targetGraphic = btnImg;
        TMP_Text btnLabel = AddText(root, "Label", "帰還", 26, TextAlignmentOptions.Center);
        btnLabel.transform.SetParent(btnRt, false);
        Stretch(btnLabel.rectTransform);

        RewardScreen screen = root.gameObject.AddComponent<RewardScreen>();
        SerializedObject so = new SerializedObject(screen);
        so.FindProperty("dropListRoot").objectReferenceValue = listRoot;
        so.FindProperty("dropRowPrefab").objectReferenceValue = Load("DropRow");
        so.FindProperty("resultText").objectReferenceValue = result;
        so.FindProperty("returnButton").objectReferenceValue = returnBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // ---------------------------------------------------------------- InventoryPanel（構築画面 左上）

    private static GameObject BuildInventoryPanel(Transform canvas)
    {
        RectTransform root = NewUI("InventoryPanel", canvas);
        Frame(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(300f, 260f));
        AddImage(root, new Color(0f, 0f, 0f, 0.4f), false);

        TMP_Text title = AddText(root, "Title", "所持素材", 20, TextAlignmentOptions.TopLeft);
        Frame(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(160f, 28f));

        TMP_Text moneyText = AddText(root, "MoneyText", "所持金 0 G", 16, TextAlignmentOptions.TopRight);
        Frame(moneyText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(160f, 24f));
        moneyText.color = new Color(1f, 0.92f, 0.6f, 1f);
        WireMoneyLabel(moneyText);

        RectTransform listRoot = NewUI("ListRoot", root);
        Frame(listRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -40f), new Vector2(284f, 210f));
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        ContentSizeFitter csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text empty = AddText(root, "EmptyLabel", "（まだ何も持っていない）", 16, TextAlignmentOptions.TopLeft);
        Frame(empty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -44f), new Vector2(276f, 24f));
        empty.color = new Color(1f, 1f, 1f, 0.6f);

        InventoryPanel panel = root.gameObject.AddComponent<InventoryPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("listRoot").objectReferenceValue = listRoot;
        so.FindProperty("rowPrefab").objectReferenceValue = Load("DropRow");
        so.FindProperty("emptyLabel").objectReferenceValue = empty;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // TMP_Text に MoneyLabel を付けて label 参照を配線する。
    private static void WireMoneyLabel(TMP_Text text)
    {
        MoneyLabel ml = text.gameObject.AddComponent<MoneyLabel>();
        SerializedObject so = new SerializedObject(ml);
        so.FindProperty("label").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureSingleton<T>(string goName) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null) return;
        GameObject go = new GameObject(goName);
        go.AddComponent<T>();
    }

    // 旧: シーンに直置きの単体 EnemyStatus を除去（敵は EnemyRoster がプール管理するようになったため）
    private static void RemoveStandaloneEnemyStatus()
    {
        EnemyStatus[] all = Object.FindObjectsByType<EnemyStatus>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EnemyStatus es in all)
        {
            if (es.GetComponentInParent<EnemyRoster>() == null)
            {
                Object.DestroyImmediate(es.gameObject);
            }
        }
    }

    // ---------------------------------------------------------------- HideoutRoot（研究画面）

    // 横並びのタブバー（子はレイアウト要素で自分の幅を決める）。
    private static RectTransform TabStrip(RectTransform parent, string name, Vector2 anchoredPos, Vector2 size, float spacing)
    {
        RectTransform strip = NewUI(name, parent);
        Frame(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), anchoredPos, size);
        HorizontalLayoutGroup h = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        return strip;
    }

    // 研究（放射状スキルツリー）画面。ハイドアウトのハブ（研究机）から入る。
    private static GameObject BuildResearchRoot(Transform canvas)
    {
        RectTransform root = NewUI("ResearchRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.06f, 0.07f, 0.10f, 1f), true);

        TMP_Text title = AddText(root, "Title", "研究 — 深淵のスキルツリー", 34, TextAlignmentOptions.Center);
        Frame(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 56f));

        // 表示領域（マスク＋ドラッグ受け）— 画面いっぱいに広げる
        RectTransform viewport = NewUI("TreeViewport", root);
        Frame(viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-190f, -6f), new Vector2(1500f, 900f));
        AddImage(viewport, new Color(0f, 0f, 0f, 0.25f), true); // raycast でドラッグ／ホイールを拾う
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewUI("TreeContent", viewport);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(10000f, 10000f);
        content.anchoredPosition = Vector2.zero;

        RectTransform edgeLayer = NewUI("Edges", content);
        Stretch(edgeLayer);
        RectTransform nodeLayer = NewUI("Nodes", content);
        Stretch(nodeLayer);

        // 詳細サイドバー（右にドッキング）
        RectTransform side = NewUI("DetailPanel", root);
        Frame(side, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, -6f), new Vector2(350f, 900f));
        AddImage(side, new Color(0f, 0f, 0f, 0.5f), true);

        TMP_Text detailTitle = AddText(side, "DetailTitle", "ノードを選択", 24, TextAlignmentOptions.Top);
        Frame(detailTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(320f, 76f));

        TMP_Text detailBody = AddText(side, "DetailBody", "", 17, TextAlignmentOptions.TopLeft);
        Frame(detailBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(316f, 600f));

        RectTransform allocRt = NewUI("AllocateButton", side);
        Frame(allocRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(260f, 62f));
        Image allocImg = AddImage(allocRt, new Color(0.25f, 0.4f, 0.7f, 1f), true);
        Button allocBtn = allocRt.gameObject.AddComponent<Button>();
        allocBtn.targetGraphic = allocImg;
        TMP_Text allocLabel = AddText(allocRt.gameObject.transform, "Label", "取得", 24, TextAlignmentOptions.Center);
        Stretch(allocLabel.rectTransform);

        TMP_Text legend = AddText(root, "Legend", "", 15, TextAlignmentOptions.Center);
        Frame(legend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f, 96f), new Vector2(1500f, 24f));
        legend.color = new Color(1f, 1f, 1f, 0.6f);

        RectTransform closeRt = NewUI("CloseButton", root);
        Frame(closeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(240f, 54f));
        Image closeImg = AddImage(closeRt, new Color(0.3f, 0.3f, 0.36f, 1f), true);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        TMP_Text closeLbl = AddText(closeRt.gameObject.transform, "Label", "戻る", 22, TextAlignmentOptions.Center);
        Stretch(closeLbl.rectTransform);

        ResearchTreeView view = viewport.gameObject.AddComponent<ResearchTreeView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("edgeLayer").objectReferenceValue = edgeLayer;
        so.FindProperty("nodeLayer").objectReferenceValue = nodeLayer;
        so.FindProperty("nodeWidgetPrefab").objectReferenceValue = Load("ResearchNode");
        so.FindProperty("detailTitle").objectReferenceValue = detailTitle;
        so.FindProperty("detailBody").objectReferenceValue = detailBody;
        so.FindProperty("allocateButton").objectReferenceValue = allocBtn;
        so.FindProperty("allocateLabel").objectReferenceValue = allocLabel;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("legendText").objectReferenceValue = legend;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // ハイドアウトのハブ画面（5設備の建造/強化＋各機能を1スクロールに動的生成）。
    private static GameObject BuildHideoutRoot(Transform canvas)
    {
        RectTransform root = NewUI("HideoutRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.07f, 0.07f, 0.09f, 1f), true);

        TMP_Text title = AddText(root, "Title", "ハイドアウト", 34, TextAlignmentOptions.Center);
        Frame(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(700f, 52f));

        // 上部：魔力炉の燃料バー
        RectTransform fuelBg = NewUI("FuelBarBG", root);
        Frame(fuelBg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(760f, 26f));
        AddImage(fuelBg, new Color(0f, 0f, 0f, 0.5f), false);
        RectTransform fuelFillRt = NewUI("FuelFill", fuelBg);
        Stretch(fuelFillRt);
        Image fuelFill = AddImage(fuelFillRt, new Color(0.30f, 0.55f, 0.95f, 1f), false);
        MakeHorizontalFill(fuelFill);
        TMP_Text fuelText = AddText(root, "FuelText", "魔力炉 —", 15, TextAlignmentOptions.Center);
        Frame(fuelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(760f, 26f));

        // 中央：縦スクロール
        RectTransform viewport = NewUI("Viewport", root);
        Frame(viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(820f, 560f));
        AddImage(viewport, new Color(0f, 0f, 0f, 0.25f), true);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 26f;

        RectTransform listRoot = NewUI("ListRoot", viewport);
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(1f, 1f);
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.offsetMin = new Vector2(8f, 0f);
        listRoot.offsetMax = new Vector2(-8f, 0f);
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        listRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = listRoot;
        scroll.viewport = viewport;

        // 研究するボタン（研究机が建っていれば有効）
        RectTransform researchRt = NewUI("ResearchButton", root);
        Frame(researchRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-140f, 40f), new Vector2(240f, 54f));
        Image researchImg = AddImage(researchRt, new Color(0.25f, 0.4f, 0.7f, 1f), true);
        Button researchBtn = researchRt.gameObject.AddComponent<Button>();
        researchBtn.targetGraphic = researchImg;
        TMP_Text researchLbl = AddText(researchRt.gameObject.transform, "Label", "研究する", 22, TextAlignmentOptions.Center);
        Stretch(researchLbl.rectTransform);

        RectTransform closeRt = NewUI("CloseButton", root);
        Frame(closeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(140f, 40f), new Vector2(240f, 54f));
        Image closeImg = AddImage(closeRt, new Color(0.3f, 0.3f, 0.36f, 1f), true);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        TMP_Text closeLbl = AddText(closeRt.gameObject.transform, "Label", "戻る", 22, TextAlignmentOptions.Center);
        Stretch(closeLbl.rectTransform);

        HideoutHubPanel panel = root.gameObject.AddComponent<HideoutHubPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("listRoot").objectReferenceValue = listRoot;
        so.FindProperty("fuelText").objectReferenceValue = fuelText;
        so.FindProperty("fuelFill").objectReferenceValue = fuelFill;
        so.FindProperty("researchButton").objectReferenceValue = researchBtn;
        so.FindProperty("researchButtonLabel").objectReferenceValue = researchLbl;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        if (JpFont != null) so.FindProperty("font").objectReferenceValue = JpFont;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // トレード画面。トレーダーごとのタブ＋「交換/依頼」サブタブ。行は TradePanel がコードで生成する。
    private static GameObject BuildTradeRoot(Transform canvas)
    {
        RectTransform root = NewUI("TradeRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.10f, 0.11f, 0.14f, 1f), true);

        TMP_Text title = AddText(root, "Title", "トレード", 34, TextAlignmentOptions.Center);
        Frame(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(700f, 52f));

        RectTransform tabBar = TabStrip(root, "TabBar", new Vector2(0f, -84f), new Vector2(1180f, 60f), 8f);
        RectTransform subTabBar = TabStrip(root, "SubTabBar", new Vector2(0f, -150f), new Vector2(700f, 44f), 8f);

        TMP_Text blurb = AddText(root, "Blurb", "", 15, TextAlignmentOptions.Center);
        Frame(blurb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(1180f, 30f));
        blurb.color = new Color(1f, 1f, 1f, 0.7f);

        TMP_Text money = AddText(root, "MoneyText", "所持金 0 G", 20, TextAlignmentOptions.Right);
        Frame(money.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(280f, 34f));
        money.color = new Color(1f, 0.92f, 0.6f, 1f);
        WireMoneyLabel(money);

        RectTransform viewport = NewUI("Viewport", root);
        Frame(viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1180f, 600f));
        AddImage(viewport, new Color(0f, 0f, 0f, 0.25f), true);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 26f;

        RectTransform listRoot = NewUI("ListRoot", viewport);
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(1f, 1f);
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.offsetMin = new Vector2(8f, 0f);
        listRoot.offsetMax = new Vector2(-8f, 0f);
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        listRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = listRoot;
        scroll.viewport = viewport;

        RectTransform closeRt = NewUI("CloseButton", root);
        Frame(closeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(220f, 54f));
        Image closeImg = AddImage(closeRt, new Color(0.3f, 0.3f, 0.36f, 1f), true);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        TMP_Text closeLbl = AddText(closeRt.gameObject.transform, "Label", "戻る", 22, TextAlignmentOptions.Center);
        Stretch(closeLbl.rectTransform);

        TradePanel panel = root.gameObject.AddComponent<TradePanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("tabBar").objectReferenceValue = tabBar;
        so.FindProperty("subTabBar").objectReferenceValue = subTabBar;
        so.FindProperty("listRoot").objectReferenceValue = listRoot;
        so.FindProperty("blurbText").objectReferenceValue = blurb;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        if (JpFont != null) so.FindProperty("font").objectReferenceValue = JpFont;

        // traderIcons をトレーダーIDで先埋め（ユーザーが後で Sprite を割り当てる）
        SerializedProperty icons = so.FindProperty("traderIcons");
        System.Collections.Generic.List<Trader> traders = TraderCatalog.BuildTraders();
        icons.arraySize = traders.Count;
        for (int i = 0; i < traders.Count; i++)
        {
            SerializedProperty e = icons.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("traderId").stringValue = traders[i].id;
            e.FindPropertyRelative("icon").objectReferenceValue = null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    // シーンに既にあるボタン（MenuPanel の子など）を探して返す。label を渡したときだけ
    // 子テキストを差し替える（null/空なら触らない）。onClick の配線は GamePhaseManager が Awake で行う。
    private static Button BindMenuButton(Transform canvas, string path, string label)
    {
        Transform t = canvas.Find(path);
        if (t == null)
        {
            Debug.LogWarning("[Grimoire] ボタンが見つかりません: Canvas/" + path + "。GamePhaseManager の該当ボタンを手動で設定してください。");
            return null;
        }
        Button b = t.GetComponent<Button>();
        if (b == null)
        {
            Debug.LogWarning("[Grimoire] " + path + " に Button コンポーネントがありません。");
            return null;
        }
        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text lbl = b.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = label;
        }
        return b;
    }

    // ---------------------------------------------------------------- prefabs

    private static void EnsurePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        SavePrefabIfMissing("DamageNumber", BuildDamageNumberTemplate);
        SavePrefabIfMissing("StatusEffectIcon", BuildStatusIconTemplate);
        SavePrefabIfMissing("BuffIndicator", BuildBuffIndicatorTemplate);
        SavePrefabIfMissing("DropRow", BuildDropRowTemplate);
        SavePrefabIfMissing("ResearchNode", BuildResearchNodeTemplate);
        AssetDatabase.SaveAssets();
    }

    private static void SavePrefabIfMissing(string name, System.Func<GameObject> builder)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        GameObject template = builder();
        PrefabUtility.SaveAsPrefabAsset(template, path);
        Object.DestroyImmediate(template);
        Debug.Log("[Grimoire] prefab 生成/更新: " + path);
    }

    private static GameObject BuildDamageNumberTemplate()
    {
        RectTransform rt = NewUI("DamageNumber", null);
        rt.sizeDelta = new Vector2(140f, 44f);
        TMP_Text label = AddText(rt.gameObject.transform, "Label", "0", 30, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyles.Bold;
        DamageNumber comp = rt.gameObject.AddComponent<DamageNumber>();
        SetPrivate(comp, "label", label);
        return rt.gameObject;
    }

    private static GameObject BuildStatusIconTemplate()
    {
        RectTransform rt = NewUI("StatusEffectIcon", null);
        rt.sizeDelta = new Vector2(44f, 44f);
        Image bg = AddImage(rt, Color.gray, false);
        TMP_Text label = AddText(rt.gameObject.transform, "Label", "火", 24, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyles.Bold;
        StatusEffectIcon comp = rt.gameObject.AddComponent<StatusEffectIcon>();
        SetPrivate(comp, "background", bg);
        SetPrivate(comp, "label", label);
        return rt.gameObject;
    }

    private static GameObject BuildBuffIndicatorTemplate()
    {
        RectTransform rt = NewUI("BuffIndicator", null);
        rt.sizeDelta = new Vector2(44f, 44f);
        Image fill = AddImage(rt, new Color(0.35f, 0.65f, 0.95f, 1f), false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillAmount = 1f;
        TMP_Text label = AddText(rt.gameObject.transform, "Label", "攻", 24, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyles.Bold;
        BuffIndicator comp = rt.gameObject.AddComponent<BuffIndicator>();
        SetPrivate(comp, "radialFill", fill);
        SetPrivate(comp, "label", label);
        return rt.gameObject;
    }

    private static GameObject BuildDropRowTemplate()
    {
        RectTransform rt = NewUI("DropRow", null);
        rt.sizeDelta = new Vector2(380f, 40f);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 40f; le.minHeight = 40f;

        RectTransform iconRt = NewUI("Icon", rt);
        Image icon = AddImage(iconRt, Color.white, false);
        LayoutElement iconLe = iconRt.gameObject.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 28f; iconLe.preferredHeight = 28f; iconLe.minWidth = 28f;

        TMP_Text nameLabel = AddText(rt, "Name", "素材名", 22, TextAlignmentOptions.MidlineLeft);
        LayoutElement nameLe = nameLabel.gameObject.AddComponent<LayoutElement>();
        nameLe.flexibleWidth = 1f; nameLe.preferredWidth = 240f;

        TMP_Text amountLabel = AddText(rt, "Amount", "×1", 22, TextAlignmentOptions.MidlineRight);
        LayoutElement amtLe = amountLabel.gameObject.AddComponent<LayoutElement>();
        amtLe.preferredWidth = 60f; amtLe.minWidth = 60f;

        DropRow comp = rt.gameObject.AddComponent<DropRow>();
        SetPrivate(comp, "icon", icon);
        SetPrivate(comp, "nameLabel", nameLabel);
        SetPrivate(comp, "amountLabel", amountLabel);
        return rt.gameObject;
    }

    private static GameObject BuildResearchNodeTemplate()
    {
        RectTransform rt = NewUI("ResearchNode", null);
        rt.sizeDelta = new Vector2(92f, 92f);
        Image bg = AddImage(rt, new Color(0.32f, 0.34f, 0.40f, 1f), true); // クリックを拾うため raycastTarget=true 必須
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;

        TMP_Text label = AddText(rt.gameObject.transform, "Label", "◆", 13, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        ResearchNodeWidget comp = rt.gameObject.AddComponent<ResearchNodeWidget>();
        SetPrivate(comp, "background", bg);
        SetPrivate(comp, "label", label);
        SetPrivate(comp, "button", btn);
        return rt.gameObject;
    }


    // ---------------------------------------------------------------- helpers

    private static RectTransform NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 100f);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Frame(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycast)
    {
        Image img = rt.gameObject.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static void MakeHorizontalFill(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, int size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (JpFont != null) t.font = JpFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static void AddHorizontalLayout(RectTransform rt, float spacing)
    {
        HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = false;
        h.childControlHeight = false;
    }

    private static GameObject Load(string prefabName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefabName + ".prefab");
    }

    private static void SetPrivate(object comp, string field, Object value)
    {
        FieldInfo fi = comp.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) fi.SetValue(comp, value);
        else Debug.LogWarning("[Grimoire] フィールドが見つかりません: " + comp.GetType().Name + "." + field);
    }

    private static Button FindSortieButton()
    {
        Button[] all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button b in all)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                if (b.onClick.GetPersistentTarget(i) is BattleManager &&
                    b.onClick.GetPersistentMethodName(i) == "StartBattle")
                    return b;
            }
        }
        return null;
    }

    private static void DestroyExisting(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }
}
