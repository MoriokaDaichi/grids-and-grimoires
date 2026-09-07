using UnityEngine;
using UnityEngine.UI;

// 構築(Build) → 戦闘(Battle) → 報酬(Reward) → 構築 の3状態を管理する単純なステートマシン。
// 各フェーズに属するGameObject群を SetActive で切り替えるだけ。
// （企画書のTarkov型ホームハブ Character/Trade/Hideout は後続フェーズ。ここではコアループの最小連結のみ）
public class GamePhaseManager : MonoBehaviour
{
    public enum GamePhase { Build, Battle, Reward }

    [Header("構築フェーズで表示するオブジェクト（杖グリッド/魔法一覧/ステータス/出撃ボタン等）")]
    [SerializeField] private GameObject[] buildPhaseObjects;

    [Header("戦闘・報酬の画面ルート")]
    [SerializeField] private GameObject battleRoot;
    [SerializeField] private GameObject rewardRoot;

    [Header("出撃ボタン（Awakeでクリックを配線）")]
    [SerializeField] private Button sortieButton;

    public GamePhase Current { get; private set; }
    public bool LastRunCleared { get; private set; }
    public System.Action<GamePhase> OnPhaseChanged;

    private DungeonManager dungeon;

    void Awake()
    {
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
        if (sortieButton != null) sortieButton.onClick.AddListener(StartSortie);
    }

    void OnEnable()
    {
        if (dungeon != null)
        {
            dungeon.OnDungeonCleared += HandleCleared;
            dungeon.OnDungeonFailed += HandleFailed;
        }
    }

    void OnDisable()
    {
        if (dungeon != null)
        {
            dungeon.OnDungeonCleared -= HandleCleared;
            dungeon.OnDungeonFailed -= HandleFailed;
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
        if (battleRoot != null) battleRoot.SetActive(phase == GamePhase.Battle);
        if (rewardRoot != null) rewardRoot.SetActive(phase == GamePhase.Reward);

        OnPhaseChanged?.Invoke(phase);
    }

    // 出撃ボタンから呼ぶ。杖に発動可能な魔法が無ければ構築画面に留まる。
    public void StartSortie()
    {
        if (dungeon == null || Current != GamePhase.Build) return;

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
