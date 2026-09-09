using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MagicGridManager : MonoBehaviour
{
    public int width = 5;
    public int height = 5;
    public float cellSize = 50f;

    [Header("杖のグリッド1辺（作業台で作る杖の tier で決まる。数値は仮）")]
    [Tooltip("杖 未製作(tier0) のときの1辺。tier1 で +1、tier2 で +2 … と広がる。" +
             "3 スタート＝杖なしでも基本＋1枚は置けて、tier1 杖で 4×4（Mega＋AoE＋単体が両立）に届く")]
    public int minGridSize = 3;
    [Tooltip("最大の1辺。tier3 杖（作業台Lv3）で 6×6＝GigaFlame＋GigaFire＋単体 が同居でき、" +
             "深部の同時多数（4体）に AoE throughput で対抗できる（深部設計 D）。tier2 杖は 5×5 で据え置き。")]
    public int maxGridSize = 6;

    // グリッドの1辺が変わったときに発火（新 width, height）。
    public System.Action<int, int> OnGridResized;

    // どのマスに何の魔法があるかを保持する2次元配列
    private MagicData[,] grid;

    private RectTransform cachedRect;
    private GridLayoutGroup cachedLayout;
    private GamePhaseManager phase;

    void Awake()
    {
        grid = new MagicData[width, height];
    }

    void Start()
    {
        phase = Object.FindFirstObjectByType<GamePhaseManager>();
        if (phase != null) phase.OnPhaseChanged += HandlePhaseChanged;
        if (HideoutManager.Instance != null) HideoutManager.Instance.OnHideoutChanged += ApplyWandTierSize;
        ApplyWandTierSize();
    }

    void OnDestroy()
    {
        if (phase != null) phase.OnPhaseChanged -= HandlePhaseChanged;
        if (HideoutManager.Instance != null) HideoutManager.Instance.OnHideoutChanged -= ApplyWandTierSize;
    }

    private void HandlePhaseChanged(GamePhaseManager.GamePhase p)
    {
        // 構築フェーズに入るたびに、いま所持している杖に合わせてグリッドを作り直す。
        if (p == GamePhaseManager.GamePhase.Build) ApplyWandTierSize();
    }

    // ---------------------------------------------------------------- 杖 tier → グリッドサイズ

    // いま装備している杖の tier を返す（杖を外している／未製作は 0）。
    // グリッドの広さは「所有」ではなく「実際に握っている杖」で決まる。
    public static int CurrentWandTier()
    {
        HideoutManager h = HideoutManager.Instance;
        if (h == null) return 0;

        GearDef wand = h.EquippedGear(EquipSlot.Wand);
        return wand != null ? wand.tier : 0;
    }

    public int WandTierToSize(int tier)
    {
        return Mathf.Clamp(minGridSize + Mathf.Max(0, tier), minGridSize, maxGridSize);
    }

    // いま所持している杖の tier からグリッドサイズを決めて適用する。
    public void ApplyWandTierSize()
    {
        int s = WandTierToSize(CurrentWandTier());
        SetGridSize(s, s);
    }

    // ---------------------------------------------------------------- リサイズ

    // グリッドを w×h に作り替える。サイズが変わったときは盤面のピースを一旦すべて取り外す
    // （はみ出し・座標ズレ防止）。同じサイズなら盤面はそのまま維持する。
    public void SetGridSize(int w, int h)
    {
        w = Mathf.Clamp(w, 1, 12);
        h = Mathf.Clamp(h, 1, 12);

        bool changed = grid == null || width != w || height != h;

        width = w;
        height = h;

        // サイズが変わったときだけ配列を作り直す（同じサイズなら盤面を維持）。
        if (changed) grid = new MagicData[w, h];

        ApplyVisualSize();

        if (changed)
        {
            MagicSpawner spawner = Object.FindFirstObjectByType<MagicSpawner>();
            if (spawner != null) spawner.ResetBoard();
            OnGridResized?.Invoke(w, h);
        }
    }

    // RectTransform／GridLayoutGroup／セル背景の見た目を現在の width×height に合わせる。
    private void ApplyVisualSize()
    {
        if (cachedRect == null) cachedRect = GetComponent<RectTransform>();
        if (cachedLayout == null) cachedLayout = GetComponent<GridLayoutGroup>();

        if (cachedRect != null)
            cachedRect.sizeDelta = new Vector2(width * cellSize, height * cellSize);

        if (cachedLayout != null)
        {
            cachedLayout.cellSize = new Vector2(cellSize, cellSize);
            cachedLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cachedLayout.constraintCount = width;
        }

        // 直下の子（グリッドのマス目背景）を w*h 個そろえる。足りなければ子0を複製して増やす
        // （6×6＝36 セルに対しシーンの既存が 25 だと 11 マス背景が欠けるため。深部設計 D）。
        int need = width * height;
        if (transform.childCount > 0)
        {
            Transform template = transform.GetChild(0);
            while (transform.childCount < need)
            {
                GameObject clone = Instantiate(template.gameObject, transform);
                clone.name = template.name;
            }
        }
        int shown = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject cell = transform.GetChild(i).gameObject;
            bool on = shown < need;
            if (cell.activeSelf != on) cell.SetActive(on);
            if (on) shown++;
        }
    }

    // スクリーン座標（マウス位置）をグリッド座標（0,0〜4,4）に変換
    public Vector2Int WorldToGridPos(Vector2 mousePos)
    {
        RectTransform rect = GetComponent<RectTransform>();
        // マウス位置をグリッドのローカル座標に変換
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, mousePos, null, out Vector2 localPos);

        return LocalPointToGridPos(localPos);
    }

    // ワールド座標（ピースの transform.position など）を、スクリーン座標を経由せず直接グリッド座標に変換する
    public Vector2Int WorldPositionToGridPos(Vector3 worldPos)
    {
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 localPos = rect.InverseTransformPoint(worldPos);
        return LocalPointToGridPos(localPos);
    }

    private Vector2Int LocalPointToGridPos(Vector2 localPos)
    {
        RectTransform rect = GetComponent<RectTransform>();

        // グリッドの左下角を基準(0,0)にするための補正
        // rect.rect.width / 2 は、Pivotが(0.5, 0.5)の場合の左端までの距離
        float originX = localPos.x + (rect.rect.width * rect.pivot.x);
        float originY = localPos.y + (rect.rect.height * rect.pivot.y);

        // マス番号を算出
        int x = Mathf.FloorToInt(originX / cellSize);
        int y = Mathf.FloorToInt(originY / cellSize);

        return new Vector2Int(x, y);
    }

    // ワールド座標がグリッドの矩形に重なっているかどうかを判定する（スクリーン座標を経由しない）
    public bool IsOverlappingGrid(Vector3 worldPos)
    {
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 localPos = rect.InverseTransformPoint(worldPos);
        Rect localRect = rect.rect;
        return localRect.Contains(localPos);
    }

    // 指定した形状（data.shapeNodes）の全マスがグリッドの範囲内に収まるよう、
    // アンカー位置（gridPos）を補正して返す。障害物との重なりはここではチェックしない
    public Vector2Int ClampToGrid(MagicData data, Vector2Int gridPos)
    {
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        foreach (Vector2Int node in data.shapeNodes)
        {
            minX = Mathf.Min(minX, node.x);
            maxX = Mathf.Max(maxX, node.x);
            minY = Mathf.Min(minY, node.y);
            maxY = Mathf.Max(maxY, node.y);
        }

        int clampedX = Mathf.Clamp(gridPos.x, -minX, width - 1 - maxX);
        int clampedY = Mathf.Clamp(gridPos.y, -minY, height - 1 - maxY);
        return new Vector2Int(clampedX, clampedY);
    }

    // マスの中心の「ワールド座標」を返すように変更
    public Vector3 GetSnapWorldPosition(Vector2Int gridPos)
    {
        RectTransform rect = GetComponent<RectTransform>();

        // グリッドの左下端からのローカルオフセットを計算
        float x = (gridPos.x * cellSize) + (cellSize / 2f) - (rect.rect.width * rect.pivot.x);
        float y = (gridPos.y * cellSize) + (cellSize / 2f) - (rect.rect.height * rect.pivot.y);

        // ローカル座標をワールド座標に変換して返す
        return transform.TransformPoint(new Vector3(x, y, 0));
    }

    // 配置可能かチェックする関数
    public bool CanPlace(MagicData data, Vector2Int gridPos)
    {
        foreach (var node in data.shapeNodes)
        {
            int targetX = gridPos.x + node.x;
            int targetY = gridPos.y + node.y;

            // 枠外チェック
            if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) return false;
            // 重なりチェック
            if (grid[targetX, targetY] != null) return false;
        }
        return true;
    }

    // 魔法をグリッド配列に記録する
    public void RegisterMagic(MagicData data, Vector2Int gridPos)
    {
        foreach (var node in data.shapeNodes)
        {
            grid[gridPos.x + node.x, gridPos.y + node.y] = data;
        }
    }

    // 特定の魔法をグリッドから削除する（移動開始時用）
    public void RemoveMagic(MagicData data)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == data) grid[x, y] = null;
            }
        }
    }

    // 現在配置されている魔法を重複なく取得する（オートバトル開始時に使用）
    public List<MagicData> GetPlacedMagics()
    {
        HashSet<MagicData> placed = new HashSet<MagicData>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null) placed.Add(grid[x, y]);
            }
        }
        return new List<MagicData>(placed);
    }
}
