using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MagicSpawner : MonoBehaviour
{
    [Header("設定")]
    public GameObject pieceUIPrefab;    // MagicPieceUIのプレハブ
    public GameObject buttonPrefab;     // MagicGeneratorButtonのプレハブ
    public Transform spawnParent;       // ピースを生成する親(Canvas内のどこか)
    public ScrollRect scrollRect;       // 非アクティブ化制御用
    public Transform buttonContainer;   // ボタンを並べるContent(ScrollViewのContent)

    [Header("生成リスト")]
    public List<MagicData> magicDataList;

    private MagicPieceUI activePiece;   // 現在カーソルに追従中の（未配置の）ピース
    private MagicGeneratorButton lastClickedButton;
    private ResearchManager research;

    void Start()
    {
        research = Object.FindFirstObjectByType<ResearchManager>();
        if (research != null) research.OnUnlocksChanged += RebuildButtons;
        RebuildButtons();
    }

    void OnDestroy()
    {
        if (research != null) research.OnUnlocksChanged -= RebuildButtons;
    }

    // 解放済みの魔法だけボタンを並べ直す（研究で解放されたとき、および初期化時に呼ぶ）
    public void RebuildButtons()
    {
        if (buttonContainer != null)
        {
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = buttonContainer.GetChild(i);
                child.SetParent(null, false); // Destroyはフレーム末尾まで遅延するので、先に親から外して数え違いを防ぐ
                Destroy(child.gameObject);
            }
        }

        foreach (var data in magicDataList)
        {
            if (data == null) continue;
            if (research != null && !research.IsUnlocked(data)) continue;
            CreateButton(data);
        }
    }

    private void CreateButton(MagicData data)
    {
        GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
        MagicGeneratorButton btnScript = btnObj.GetComponent<MagicGeneratorButton>();
        btnScript.Init(data, this);

        // ボタンのクリックイベント登録
        btnObj.GetComponent<Button>().onClick.AddListener(() => btnScript.OnClickButton());
    }

    public void SpawnPiece(MagicGeneratorButton button)
    {
        // すでに選択中のピースがあれば何もしない
        if (activePiece != null) return;

        lastClickedButton = button;

        // ピース生成。以後はカーソルに追従させる（MagicPieceUI.StartHolding）
        GameObject pieceObj = Instantiate(pieceUIPrefab, spawnParent);

        activePiece = pieceObj.GetComponent<MagicPieceUI>();
        activePiece.Setup(button.magicData);
        activePiece.StartHolding();

        // ScrollViewを操作不能にする
        scrollRect.enabled = false;
    }

    // MagicPieceUI側から「配置完了」を知らせるために呼ぶ。使用したボタンを破棄する
    public void NotifyPlaced()
    {
        activePiece = null;
        scrollRect.enabled = true;

        if (lastClickedButton != null)
        {
            Destroy(lastClickedButton.gameObject);
            lastClickedButton = null;
        }
    }

    // MagicPieceUI側から「選択解除（配置キャンセル）」を知らせるために呼ぶ。ボタンは消費しない
    public void NotifyCancelled()
    {
        activePiece = null;
        scrollRect.enabled = true;
        lastClickedButton = null;
    }

    // MagicPieceUI側から「配置済みの魔法をグリッドから外した」ことを知らせるために呼ぶ。
    // 再度選べるようにボタンを復元する
    public void RestoreButton(MagicData data)
    {
        CreateButton(data);
    }

    // 盤面をまっさらにする（グリッドのサイズが変わったときに MagicGridManager から呼ぶ）。
    // 生成済みのピース（追従中・配置済みを問わず）をすべて破棄し、ボタン一覧を作り直す。
    public void ResetBoard()
    {
        MagicPieceUI[] pieces = Object.FindObjectsByType<MagicPieceUI>(FindObjectsSortMode.None);
        foreach (MagicPieceUI p in pieces)
        {
            if (p == null) continue;
            p.transform.SetParent(null, false); // Destroy はフレーム末尾まで遅延するので先に外す
            Destroy(p.gameObject);
        }

        activePiece = null;
        lastClickedButton = null;
        if (scrollRect != null) scrollRect.enabled = true;

        RebuildButtons();
    }
}