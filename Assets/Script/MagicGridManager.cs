using UnityEngine;
using System.Collections.Generic;

public class MagicGridManager : MonoBehaviour
{
    public int width = 5;
    public int height = 5;
    public float cellSize = 50f;

    // どのマスに何の魔法があるかを保持する2次元配列
    private MagicData[,] grid;

    void Awake()
    {
        grid = new MagicData[width, height];
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