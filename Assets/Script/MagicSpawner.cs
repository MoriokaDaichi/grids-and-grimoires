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

    void Start()
    {
        // 初期ボタン生成
        foreach (var data in magicDataList)
        {
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
}