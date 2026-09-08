using UnityEngine;
using UnityEngine.UI;

// 構築(Build) → 戦闘(Battle) → 報酬(Reward) の状態と、研究(Hideout)画面を管理する単純なステートマシン。
// 各フェーズに属するGameObject群を SetActive で切り替えるだけ。
// （企画書のTarkov型ホームハブ Character/Trade は後続フェーズ）
public class GamePhaseManager : MonoBehaviour
{
    public enum GamePhase { Build, Hideout, Research, Trade, Battle, WaveClear, Reward }

    [Header("構築フェーズで表示するオブジェクト（杖グリッド/魔法一覧/ステータス/出撃ボタン等）")]
    [SerializeField] private GameObject[] buildPhaseObjects;

    [Header("ハイドアウト・研究・トレード・戦闘・ウェーブ突破・報酬の画面ルート")]
    [SerializeField] private GameObject hideoutRoot;    // 設備ハブ
    [SerializeField] private GameObject researchRoot;   // スキルツリー（ハブの研究机から入る）
    [SerializeField] private GameObject tradeRoot;
    [SerializeField] private GameObject battleRoot;
    [SerializeField] private GameObject waveClearRoot;
    [SerializeField] private GameObject rewardRoot;

    [Header("ボタン（Awakeでクリックを配線）")]
    [SerializeField] private Button sortieButton;
    [SerializeField] private Button hideoutButton;
    [SerializeField] private Button tradeButton;

    public GamePhase Current { get; private set; }
    public bool LastRunCleared { get; private set; }
    public System.Action<GamePhase> OnPhaseChanged;

    private DungeonManager dungeon;

    void Awake()
    {
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
        if (sortieButton != null) sortieButton.onClick.AddListener(StartSortie);
        if (hideoutButton != null) hideoutButton.onClick.AddListener(GoToHideout);
        if (tradeButton != null) tradeButton.onClick.AddListener(GoToTrade);
    }

    void OnEnable()
    {
        if (dungeon != null)
        {
            dungeon.OnDungeonCleared += HandleCleared;
            dungeon.OnDungeonFailed += HandleFailed;
            dungeon.OnWaveCleared += HandleWaveCleared;
        }
    }

    void OnDisable()
    {
        if (dungeon != null)
        {
            dungeon.OnDungeonCleared -= HandleCleared;
            dungeon.OnDungeonFailed -= HandleFailed;
            dungeon.OnWaveCleared -= HandleWaveCleared;
        }
    }

    void Start()
    {
        GoTo(GamePhase.Build);
    }

    public void GoTo(GamePhase phase)
    {
        Current = phase;

        if (buildPhaseObjects != null)
        {
            foreach (GameObject go in buildPhaseObjects)
            {
                if (go != null) go.SetActive(phase == GamePhase.Build);
            }
        }
        if (hideoutRoot != null) hideoutRoot.SetActive(phase == GamePhase.Hideout);
        if (researchRoot != null) researchRoot.SetActive(phase == GamePhase.Research);
        if (tradeRoot != null) tradeRoot.SetActive(phase == GamePhase.Trade);
        if (battleRoot != null) battleRoot.SetActive(phase == GamePhase.Battle);
        if (waveClearRoot != null) waveClearRoot.SetActive(phase == GamePhase.WaveClear);
        if (rewardRoot != null) rewardRoot.SetActive(phase == GamePhase.Reward);

        OnPhaseChanged?.Invoke(phase);
    }

    // 「ハイドアウト」ボタンから呼ぶ
    public void GoToHideout()
    {
        if (Current == GamePhase.Build || Current == GamePhase.Research) GoTo(GamePhase.Hideout);
    }

    // ハブの研究机「研究する」から呼ぶ
    public void GoToResearch()
    {
        if (Current == GamePhase.Hideout) GoTo(GamePhase.Research);
    }

    // スキルツリーの「戻る」から呼ぶ（ハブに戻る）
    public void CloseResearch()
    {
        if (Current == GamePhase.Research) GoTo(GamePhase.Hideout);
    }

    // 「トレード」ボタンから呼ぶ
    public void GoToTrade()
    {
        if (Current == GamePhase.Build) GoTo(GamePhase.Trade);
    }

    // 出撃ボタンから呼ぶ。杖に発動可能な魔法が無ければ構築画面に留まる。
    public void StartSortie()
    {
        if (dungeon == null || Current != GamePhase.Build) return;

        // 入場料が払えないなら構築画面のまま留まる
        if (!dungeon.CanAffordEntry())
        {
            Debug.LogWarning("[GamePhaseManager] ダンジョン入場料が足りません。");
            return;
        }

        GoTo(GamePhase.Battle);
        if (!dungeon.StartDungeon())
        {
            GoTo(GamePhase.Build);
        }
    }

    // 報酬画面の「帰還」ボタンから呼ぶ
    public void ReturnToBuild()
    {
        GoTo(GamePhase.Build);
    }

    // ウェーブ突破画面の「深層へ進む」ボタンから呼ぶ
    public void ContinueRun()
    {
        if (Current != GamePhase.WaveClear || dungeon == null) return;
        GoTo(GamePhase.Battle);
        dungeon.ContinueDeeper();
    }

    // ウェーブ突破画面の「脱出する」ボタンから呼ぶ（Escape が OnDungeonCleared を発火 → 報酬へ）
    public void EscapeRun()
    {
        if (Current != GamePhase.WaveClear || dungeon == null) return;
        dungeon.Escape();
    }

    private void HandleWaveCleared(int clearedDepth)
    {
        GoTo(GamePhase.WaveClear);
    }

    private void HandleCleared()
    {
        LastRunCleared = true;
        GoTo(GamePhase.Reward);
    }

    private void HandleFailed()
    {
        LastRunCleared = false;
        GoTo(GamePhase.Reward);
    }
}
