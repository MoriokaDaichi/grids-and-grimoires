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
        DestroyExisting(canvasT, "RewardRoot");
        DestroyExisting(canvasT, "InventoryPanel");
        DestroyExisting(canvasT, "HideoutRoot");
        DestroyExisting(canvasT, "HideoutButton");
        GameObject oldGpm = GameObject.Find("GamePhaseManager");
        if (oldGpm != null) Object.DestroyImmediate(oldGpm);

        EnsureSingleton<PlayerInventory>("PlayerInventory");
        EnsureSingleton<ResearchManager>("ResearchManager");

        GameObject battleRoot = BuildBattleRoot(canvasT);
        GameObject rewardRoot = BuildRewardRoot(canvasT);
        GameObject inventoryPanel = BuildInventoryPanel(canvasT);
        GameObject hideoutRoot = BuildHideoutRoot(canvasT);
        GameObject hideoutButton = BuildHideoutButton(canvasT);

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
        buildObjs.Add(hideoutButton);

        SerializedObject so = new SerializedObject(gpm);
        SerializedProperty arr = so.FindProperty("buildPhaseObjects");
        arr.arraySize = buildObjs.Count;
        for (int i = 0; i < buildObjs.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = buildObjs[i];
        so.FindProperty("hideoutRoot").objectReferenceValue = hideoutRoot;
        so.FindProperty("battleRoot").objectReferenceValue = battleRoot;
        so.FindProperty("rewardRoot").objectReferenceValue = rewardRoot;
        so.FindProperty("sortieButton").objectReferenceValue = sortie;
        so.FindProperty("hideoutButton").objectReferenceValue = hideoutButton.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        hideoutRoot.SetActive(false);
        battleRoot.SetActive(false);
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
        so.FindProperty("playerHpFill").objectReferenceValue = playerHpFill;
        so.FindProperty("playerHpText").objectReferenceValue = playerHpText;
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
        Frame(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(280f, 28f));

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

    private static void EnsureSingleton<T>(string goName) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null) return;
        GameObject go = new GameObject(goName);
        go.AddComponent<T>();
    }

    // ---------------------------------------------------------------- HideoutRoot（研究画面）

    private static GameObject BuildHideoutRoot(Transform canvas)
    {
        RectTransform root = NewUI("HideoutRoot", canvas);
        Stretch(root);
        AddImage(root, new Color(0.10f, 0.11f, 0.14f, 1f), true);

        TMP_Text title = AddText(root, "Title", "研究 — 隠れ家", 34, TextAlignmentOptions.Center);
        Frame(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 60f));

        // スクロールビュー
        RectTransform viewport = NewUI("Viewport", root);
        Frame(viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 520f));
        Image vpImg = AddImage(viewport, new Color(0f, 0f, 0f, 0.3f), true);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        RectTransform listRoot = NewUI("ListRoot", viewport);
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(1f, 1f);
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.offsetMin = new Vector2(8f, 0f);
        listRoot.offsetMax = new Vector2(-8f, 0f);
        listRoot.anchoredPosition = new Vector2(0f, 0f);
        VerticalLayoutGroup vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        ContentSizeFitter csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = listRoot;
        scroll.viewport = viewport;

        RectTransform closeRt = NewUI("CloseButton", root);
        Frame(closeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(220f, 56f));
        Image closeImg = AddImage(closeRt, new Color(0.3f, 0.3f, 0.36f, 1f), true);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        TMP_Text closeLbl = AddText(closeRt.gameObject.transform, "Label", "戻る", 24, TextAlignmentOptions.Center);
        Stretch(closeLbl.rectTransform);

        HideoutPanel panel = root.gameObject.AddComponent<HideoutPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("listRoot").objectReferenceValue = listRoot;
        so.FindProperty("entryPrefab").objectReferenceValue = Load("HideoutEntry");
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root.gameObject;
    }

    private static GameObject BuildHideoutButton(Transform canvas)
    {
        RectTransform rt = NewUI("HideoutButton", canvas);
        Frame(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(160f, 52f));
        Image img = AddImage(rt, new Color(0.30f, 0.26f, 0.45f, 1f), true);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        TMP_Text lbl = AddText(rt.gameObject.transform, "Label", "研究", 22, TextAlignmentOptions.Center);
        Stretch(lbl.rectTransform);
        return rt.gameObject;
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
        SavePrefabIfMissing("HideoutEntry", BuildHideoutEntryTemplate);
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

    private static GameObject BuildHideoutEntryTemplate()
    {
        RectTransform rt = NewUI("HideoutEntry", null);
        rt.sizeDelta = new Vector2(740f, 48f);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(10, 10, 4, 4);
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 48f; le.minHeight = 48f;

        TMP_Text nameLabel = AddText(rt, "Name", "魔法名", 22, TextAlignmentOptions.MidlineLeft);
        LayoutElement nameLe = nameLabel.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 180f; nameLe.minWidth = 140f;

        TMP_Text costLabel = AddText(rt, "Cost", "-", 18, TextAlignmentOptions.MidlineLeft);
        LayoutElement costLe = costLabel.gameObject.AddComponent<LayoutElement>();
        costLe.flexibleWidth = 1f; costLe.preferredWidth = 400f;

        RectTransform btnRt = NewUI("Action", rt);
        Image btnImg = AddImage(btnRt, new Color(0.28f, 0.5f, 0.35f, 1f), true);
        Button btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        LayoutElement btnLe = btnRt.gameObject.AddComponent<LayoutElement>();
        btnLe.preferredWidth = 100f; btnLe.minWidth = 100f;
        TMP_Text actionLabel = AddText(btnRt.gameObject.transform, "Label", "解放", 18, TextAlignmentOptions.Center);
        Stretch(actionLabel.rectTransform);

        HideoutEntry comp = rt.gameObject.AddComponent<HideoutEntry>();
        SetPrivate(comp, "nameLabel", nameLabel);
        SetPrivate(comp, "costLabel", costLabel);
        SetPrivate(comp, "actionButton", btn);
        SetPrivate(comp, "actionLabel", actionLabel);
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
