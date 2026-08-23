using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class MagicPieceUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("基本設定")]
    public MagicData data;
    public GameObject nodePrefab;
    public float cellSize = 50f;

    [Header("アウトライン設定")]
    public Color outlineColor = new Color(0f, 0f, 0f, 0.8f);
    public float outlineThickness = 4f;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private MagicGridManager gridManager;
    private MagicSpawner spawner; // 生成管理クラスへの参照
    private bool isDragging = false;
    private bool isHeld = false;   // スポナーで選択直後、カーソルに追従中か
    private bool isPlaced = false; // すでにグリッドに配置済みか
    private bool wasPlacedBeforeDrag = false; // 今回のドラッグ開始前に配置済みだったか

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        // シーン内からマネージャー類を探す
        gridManager = Object.FindFirstObjectByType<MagicGridManager>();
        spawner = Object.FindFirstObjectByType<MagicSpawner>();
    }

    void Start()
    {
        if (data != null) Setup(data);

        // 生成直後の位置を「失敗時の戻り先」として記録
        startPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        if (isHeld)
        {
            FollowCursor();

            if (Input.GetKeyDown(KeyCode.R))
            {
                RotatePiece();
            }

            // クリックで、グリッド上なら配置、そうでなければ選択解除
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceOrCancel();
            }
        }
        else if (isDragging && Input.GetKeyDown(KeyCode.R))
        {
            // ドラッグ中（配置済みピースの再配置中）、かつ R キーで回転
            RotatePiece();
        }
    }

    /// <summary>
    /// スポナーのボタンで選択された直後に呼ばれる。以後カーソルに追従する
    /// </summary>
    public void StartHolding()
    {
        isHeld = true;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
        FollowCursor();
    }

    private void FollowCursor()
    {
        if (canvas == null) return;
        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, canvas.worldCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }

        // グリッドに重なっている間は、はみ出さない位置へ常に補正する
        ClampToGridBounds();
    }

    private void TryPlaceOrCancel()
    {
        Vector2Int gridPos = gridManager.WorldToGridPos(Input.mousePosition);

        if (gridManager.CanPlace(data, gridPos))
        {
            // 配置成功：グリッドに吸着
            Vector3 snapWorldPos = gridManager.GetSnapWorldPosition(gridPos);
            snapWorldPos.z = transform.position.z;
            transform.position = snapWorldPos;

            gridManager.RegisterMagic(data, gridPos);

            isHeld = false;
            isPlaced = true;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            startPosition = rectTransform.anchoredPosition;

            if (spawner != null) spawner.NotifyPlaced();
        }
        else
        {
            // 配置失敗：選択解除してピースを破棄
            isHeld = false;
            if (spawner != null) spawner.NotifyCancelled();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// MagicDataに基づいてピースの見た目（各マス）を生成する
    /// </summary>
    public void Setup(MagicData magicData)
    {
        // データのインスタンス化（元のScriptableObjectを書き換えないための安全策）
        // もし既にインスタンス化済みならそのまま、未定義なら新しく作る
        if (data == null || data.name != magicData.name)
        {
            data = Instantiate(magicData);
        }

        // 既存のノードを削除（CenterMarker以外）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "CenterMarker") continue;
            Destroy(child.gameObject);
        }

        if (data == null || nodePrefab == null) return;

        // 隣接判定用に形状のマス集合を作成（外周の縁取り判定に使う）
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(data.shapeNodes);

        // 形状データに基づいてノード（Image）を配置
        foreach (Vector2Int pos in data.shapeNodes)
        {
            GameObject node = Instantiate(nodePrefab, transform);
            RectTransform nRect = node.GetComponent<RectTransform>();

            // グリッドサイズに合わせて配置
            nRect.anchoredPosition = new Vector2(pos.x * cellSize, pos.y * cellSize);

            if (node.TryGetComponent<Image>(out Image img))
            {
                img.color = data.pieceColor;
                img.raycastTarget = false; // ノードがドラッグの邪魔をしないように
            }

            // 同じピースの隣接マスが無い辺だけに縁取りを描く（形状の外周のみ縁取られるように）
            if (!occupied.Contains(pos + Vector2Int.up)) CreateOutlineEdge(node.transform, new Vector2(0, cellSize / 2f - outlineThickness / 2f), new Vector2(cellSize, outlineThickness));
            if (!occupied.Contains(pos + Vector2Int.down)) CreateOutlineEdge(node.transform, new Vector2(0, -(cellSize / 2f - outlineThickness / 2f)), new Vector2(cellSize, outlineThickness));
            if (!occupied.Contains(pos + Vector2Int.left)) CreateOutlineEdge(node.transform, new Vector2(-(cellSize / 2f - outlineThickness / 2f), 0), new Vector2(outlineThickness, cellSize));
            if (!occupied.Contains(pos + Vector2Int.right)) CreateOutlineEdge(node.transform, new Vector2(cellSize / 2f - outlineThickness / 2f, 0), new Vector2(outlineThickness, cellSize));
        }

        // 中心マーカーを常に最前面に
        Transform marker = transform.Find("CenterMarker");
        if (marker != null) marker.SetAsLastSibling();
    }

    /// <summary>
    /// ノードの指定した辺に縁取り用の細長いImageを1枚生成する
    /// </summary>
    private void CreateOutlineEdge(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject edge = new GameObject("OutlineEdge", typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(parent, false);

        RectTransform edgeRect = edge.GetComponent<RectTransform>();
        edgeRect.anchorMin = edgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        edgeRect.pivot = new Vector2(0.5f, 0.5f);
        edgeRect.anchoredPosition = anchoredPosition;
        edgeRect.sizeDelta = size;

        Image edgeImage = edge.GetComponent<Image>();
        edgeImage.color = outlineColor;
        edgeImage.raycastTarget = false;
    }

    /// <summary>
    /// ピースを時計回りに90度回転させる。グリッド上に乗っている場合、回転後の形状が
    /// グリッド範囲に収まらない、または既存の魔法と重なる場合は回転をキャンセルする
    /// </summary>
    public void RotatePiece()
    {
        if (data == null) return;

        // 現在の形状をバックアップ
        List<Vector2Int> backupNodes = new List<Vector2Int>(data.shapeNodes);

        // (x, y) -> (y, -x)
        for (int i = 0; i < data.shapeNodes.Count; i++)
        {
            Vector2Int oldPos = data.shapeNodes[i];
            data.shapeNodes[i] = new Vector2Int(oldPos.y, -oldPos.x);
        }

        // グリッド上に乗っている場合、回転後の形状が配置可能か（範囲内・かつ既存の魔法と重ならないか）確認する
        if (gridManager.IsOverlappingGrid(transform.position))
        {
            Vector2Int gridPos = gridManager.WorldPositionToGridPos(transform.position);
            if (!gridManager.CanPlace(data, gridPos))
            {
                // 収まらない・重なる場合は回転をキャンセルして元に戻す
                data.shapeNodes = backupNodes;
                return;
            }
        }

        // 見た目を再構築
        Setup(data);
    }

    /// <summary>
    /// グリッドの矩形に重なっているなら、形状全体がグリッドの範囲内に収まるように位置を補正する。
    /// グリッドに重なっていない場合は何もしない（ボタン一覧上などで回転しても位置は動かさない）
    /// </summary>
    private void ClampToGridBounds()
    {
        // グリッドの矩形と重なっていないなら対象外（ボタン一覧上などでの回転・移動では位置は動かさない）
        if (!gridManager.IsOverlappingGrid(transform.position))
        {
            return;
        }

        Vector2Int gridPos = gridManager.WorldPositionToGridPos(transform.position);
        Vector2Int clampedPos = gridManager.ClampToGrid(data, gridPos);

        if (clampedPos != gridPos)
        {
            Vector3 snapPos = gridManager.GetSnapWorldPosition(clampedPos);
            snapPos.z = transform.position.z;
            transform.position = snapPos;
        }
    }

    // --- ドラッグイベント（配置済みピースの再配置用） ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isHeld) return; // ホールド中（クリック配置待ち）はドラッグを無視

        isDragging = true;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        // ドラッグ開始時に最前面へ
        transform.SetAsLastSibling();

        // もし配置済みだったものを動かすなら、グリッド登録を解除
        wasPlacedBeforeDrag = isPlaced;
        if (isPlaced)
        {
            gridManager.RemoveMagic(data);
            isPlaced = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

        // グリッドに重なっている間は、はみ出さない位置へ常に補正する
        ClampToGridBounds();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        Vector2Int gridPos = gridManager.WorldToGridPos(eventData.position);

        if (gridManager.CanPlace(data, gridPos))
        {
            // 配置成功
            Vector3 snapWorldPos = gridManager.GetSnapWorldPosition(gridPos);
            snapWorldPos.z = transform.position.z;
            transform.position = snapWorldPos;

            gridManager.RegisterMagic(data, gridPos);

            // 以前に配置されたことがない（新規生成された）ピースの場合のみSpawnerに通知
            if (!isPlaced && spawner != null)
            {
                spawner.NotifyPlaced();
            }

            isPlaced = true;
            startPosition = rectTransform.anchoredPosition; // 成功した位置を記憶
        }
        else if (wasPlacedBeforeDrag && !gridManager.IsOverlappingGrid(transform.position))
        {
            // 配置済みだった魔法をグリッド外へドラッグして外した：破棄してボタンを復元する
            if (spawner != null) spawner.RestoreButton(data);
            Destroy(gameObject);
        }
        else
        {
            // 配置失敗：元の位置に戻る
            rectTransform.anchoredPosition = startPosition;
        }
    }
}