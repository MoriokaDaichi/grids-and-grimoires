using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 通しプレイ検証の自動ドライバ（再生モード専用）。
// 通常のゲームプレイでは一切生成されない。エディタ拡張の PlaytestDriver（Grimoire メニュー）が
// セーブを wipe → 再生モード突入 → シーンにこの MonoBehaviour を1つ差して Run() を回す。
//
// v1: 経済（ハイドアウト/研究/トレード）は回さず、コアの戦闘ループだけを cold start から複数周する。
//   - 開始ステP を HP/Def/Atk に配分
//   - 解放済みの攻撃魔法を greedy first-fit でグリッドへ配置（回転4方向を試す）
//   - 出撃 → ウェーブ突破ごとに HP割合と深度キャップで「進む/脱出」を自動判定
//   - 報酬画面で帰還 → 次周へ
//   - 到達深度・各深度の最小HP割合・コンソール error/warning を収集してレポート文字列を作る
public class PlaytestAutopilot : MonoBehaviour
{
    public int loops = 3;
    public int depthCap = 25;
    public float timeScale = 20f;
    public float escapeHpFraction = 0.35f;   // ウェーブ突破時これ未満なら脱出
    public float perRunRealTimeout = 150f;   // 1周の実時間上限（保険）

    public Action<string> OnFinished;        // レポート文字列を受け取る（PlaytestDriver が書き出す）

    private readonly List<string> logErrors = new List<string>();
    private readonly List<string> logWarnings = new List<string>();
    private int exceptionCount;

    private struct RunResult
    {
        public int index;
        public bool cleared;
        public int depth;
        public string hpTrail;      // "d1:100% d2:82% ..."
        public string note;
    }

    void Start()
    {
        StartCoroutine(Run());
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (condition != null && condition.StartsWith("[Autopilot]")) return;
        switch (type)
        {
            case LogType.Error:
            case LogType.Assert:
                logErrors.Add(condition);
                break;
            case LogType.Exception:
                exceptionCount++;
                logErrors.Add("EXCEPTION: " + condition);
                break;
            case LogType.Warning:
                logWarnings.Add(condition);
                break;
        }
    }

    private IEnumerator Run()
    {
        string report;
        float originalTimeScale = Time.timeScale;
        var runs = new List<RunResult>();
        string fatal = null;

        // マネージャが Awake/Start を済ませるまで数フレーム待つ
        for (int i = 0; i < 10; i++) yield return null;

        var phase = FindFirstObjectByType<GamePhaseManager>();
        var dungeon = FindFirstObjectByType<DungeonManager>();
        var grid = FindFirstObjectByType<MagicGridManager>();
        var spawner = FindFirstObjectByType<MagicSpawner>();
        var player = FindFirstObjectByType<PlayerStatus>();
        var research = FindFirstObjectByType<ResearchManager>();

        if (phase == null || dungeon == null || grid == null || spawner == null || player == null)
        {
            fatal = $"必須マネージャが見つからない (phase={phase!=null} dungeon={dungeon!=null} grid={grid!=null} spawner={spawner!=null} player={player!=null})";
            report = BuildReport(runs, fatal, player, grid);
            Time.timeScale = originalTimeScale;
            OnFinished?.Invoke(report);
            yield break;
        }

        // 開始ステP を配分（レポートの人力プレイに寄せて HP:Def:Atk = 4:3:3 の比で）
        AllocateStats(player);

        for (int r = 1; r <= loops && fatal == null; r++)
        {
            // Build フェーズへ戻す
            float guard = 0f;
            while (phase.Current != GamePhaseManager.GamePhase.Build && guard < 5f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }

            EnsureMagicsPlaced(grid, spawner, research);
            int placed = grid.GetPlacedMagics().Count;
            if (placed == 0)
            {
                runs.Add(new RunResult { index = r, cleared = false, depth = 0, hpTrail = "-", note = "グリッドに配置できる魔法が無い" });
                break;
            }

            var hpMin = new SortedDictionary<int, float>();
            Time.timeScale = timeScale;
            phase.StartSortie();
            yield return null;

            if (phase.Current == GamePhaseManager.GamePhase.Build)
            {
                Time.timeScale = originalTimeScale;
                runs.Add(new RunResult { index = r, cleared = false, depth = 0, hpTrail = "-", note = "StartSortie が Build に戻った（入場料 or 空杖）" });
                break;
            }

            float realElapsed = 0f;
            int lastChoiceDepth = -1;
            while (true)
            {
                yield return null;
                realElapsed += Time.unscaledDeltaTime;

                if (player.hp > 0)
                {
                    int d = Mathf.Max(1, dungeon.Depth);
                    float frac = Mathf.Clamp01((float)player.currentHp / player.hp);
                    if (!hpMin.ContainsKey(d) || frac < hpMin[d]) hpMin[d] = frac;
                }

                if (phase.Current == GamePhaseManager.GamePhase.Reward) break;

                if (dungeon.AwaitingChoice && phase.Current == GamePhaseManager.GamePhase.WaveClear)
                {
                    if (dungeon.Depth == lastChoiceDepth) { yield return null; continue; }
                    lastChoiceDepth = dungeon.Depth;

                    float frac = player.hp > 0 ? (float)player.currentHp / player.hp : 0f;
                    bool escape = dungeon.Depth >= depthCap || frac < escapeHpFraction;
                    if (escape) phase.EscapeRun();
                    else phase.ContinueRun();
                    yield return null;
                    continue;
                }

                if (realElapsed > perRunRealTimeout)
                {
                    if (dungeon.AwaitingChoice) phase.EscapeRun();
                    runs.Add(new RunResult { index = r, cleared = false, depth = dungeon.Depth, hpTrail = FormatTrail(hpMin), note = "実時間タイムアウト" });
                    hpMin = null;
                    break;
                }
            }

            Time.timeScale = originalTimeScale;

            if (hpMin != null)
            {
                bool cleared = phase.LastRunCleared;
                runs.Add(new RunResult { index = r, cleared = cleared, depth = dungeon.Depth, hpTrail = FormatTrail(hpMin), note = cleared ? "脱出" : "戦闘不能" });
            }

            // 報酬画面で帰還
            guard = 0f;
            while (phase.Current != GamePhaseManager.GamePhase.Reward && guard < 3f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (phase.Current == GamePhaseManager.GamePhase.Reward)
            {
                var reward = FindFirstObjectByType<RewardScreen>();
                if (reward != null) reward.SendMessage("OnReturn", SendMessageOptions.DontRequireReceiver);
                yield return null;
            }
            for (int i = 0; i < 3; i++) yield return null;
        }

        Time.timeScale = originalTimeScale;
        report = BuildReport(runs, fatal, player, grid);
        OnFinished?.Invoke(report);
    }

    private void AllocateStats(PlayerStatus player)
    {
        // 4:3:3 の比で HP/Def/Atk。残りは HP。
        int safety = 200;
        int i = 0;
        while (player.statsPoint > 0 && safety-- > 0)
        {
            int m = i % 10;
            if (m < 4) player.AddStat("HP");
            else if (m < 7) player.AddStat("Def");
            else player.AddStat("Atk");
            i++;
        }
    }

    // 解放済みの攻撃魔法を、形状の小さい順に greedy first-fit でグリッドへ置く。
    private void EnsureMagicsPlaced(MagicGridManager grid, MagicSpawner spawner, ResearchManager research)
    {
        if (grid.GetPlacedMagics().Count > 0) return;
        if (spawner.magicDataList == null) return;

        var candidates = new List<MagicData>();
        foreach (var md in spawner.magicDataList)
        {
            if (md == null) continue;
            if (md.category != MagicCategory.Attack) continue;
            if (research != null && !research.IsUnlocked(md)) continue;
            candidates.Add(md);
        }
        candidates.Sort((a, b) => a.shapeNodes.Count.CompareTo(b.shapeNodes.Count));

        foreach (var md in candidates)
        {
            var inst = Instantiate(md);
            inst.name = md.name;
            if (TryPlaceGreedy(grid, inst)) { /* placed */ }
            else Destroy(inst);
        }
    }

    private bool TryPlaceGreedy(MagicGridManager grid, MagicData inst)
    {
        var original = new List<Vector2Int>(inst.shapeNodes);
        for (int rot = 0; rot < 4; rot++)
        {
            if (rot > 0) RotateShape(inst);
            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.CanPlace(inst, pos))
                    {
                        grid.RegisterMagic(inst, pos);
                        return true;
                    }
                }
            }
        }
        inst.shapeNodes = original; // 置けなかったので形状を戻す（呼び出し側が Destroy）
        return false;
    }

    private static void RotateShape(MagicData inst)
    {
        for (int i = 0; i < inst.shapeNodes.Count; i++)
        {
            var p = inst.shapeNodes[i];
            inst.shapeNodes[i] = new Vector2Int(p.y, -p.x);
        }
    }

    private static string FormatTrail(SortedDictionary<int, float> hpMin)
    {
        if (hpMin == null || hpMin.Count == 0) return "-";
        var sb = new StringBuilder();
        foreach (var kv in hpMin)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append('d').Append(kv.Key).Append(':').Append(Mathf.RoundToInt(kv.Value * 100f)).Append('%');
        }
        return sb.ToString();
    }

    private string BuildReport(List<RunResult> runs, string fatal, PlayerStatus player, MagicGridManager grid)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 改善ループ 通しプレイ検証 — {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine($"- 種別: 自動ドライバ（`PlaytestAutopilot` v1・戦闘ループのみ／経済は未駆動）");
        sb.AppendLine($"- 設定: cold start ×{loops}周, `Time.timeScale`={timeScale}, 脱出しきい値 HP<{Mathf.RoundToInt(escapeHpFraction*100)}%, 深度キャップ {depthCap}");
        if (player != null)
            sb.AppendLine($"- 配分後のプレイヤー: HP{player.hp} Atk{player.atk} Def{player.def} Spd{player.spd} Luc{player.luc} / maxMana{player.maxMana}");
        if (grid != null)
        {
            var placed = grid.GetPlacedMagics();
            var names = new List<string>();
            foreach (var m in placed) names.Add(m != null ? m.magicName : "?");
            sb.AppendLine($"- グリッド {grid.width}×{grid.height}, 配置魔法 {placed.Count}: {string.Join(", ", names)}");
        }
        sb.AppendLine();

        if (fatal != null)
        {
            sb.AppendLine($"## 中断: {fatal}");
            sb.AppendLine();
        }

        sb.AppendLine("## 到達深度");
        sb.AppendLine();
        sb.AppendLine("| 周 | 結果 | 到達深度 | 各深度の最小HP% |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var run in runs)
            sb.AppendLine($"| {run.index} | {run.note} | **{run.depth}** | {run.hpTrail} |");
        sb.AppendLine();

        if (runs.Count > 0)
        {
            int max = 0, min = int.MaxValue; float avg = 0f;
            foreach (var run in runs) { max = Mathf.Max(max, run.depth); min = Mathf.Min(min, run.depth); avg += run.depth; }
            avg /= runs.Count;
            sb.AppendLine($"到達深度: 最小 {min} / 最大 {max} / 平均 {avg:F1}");
            sb.AppendLine();
        }

        sb.AppendLine("## コンソール");
        sb.AppendLine();
        sb.AppendLine($"- Exception: **{exceptionCount}**");
        sb.AppendLine($"- Error: **{logErrors.Count}**");
        sb.AppendLine($"- Warning: **{logWarnings.Count}**");
        AppendSample(sb, "Error 例", logErrors);
        AppendSample(sb, "Warning 例", logWarnings);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("_`PlaytestAutopilot` は経済を回さないため、周をまたいだ成長は無い（＝全周ほぼ同じ深度で頭打ちになるのが期待値）。経済ドライバは後続イテレーションで追加する。_");
        return sb.ToString();
    }

    private static void AppendSample(StringBuilder sb, string title, List<string> list)
    {
        if (list.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine($"### {title}（先頭{Mathf.Min(5, list.Count)}件）");
        sb.AppendLine();
        var seen = new HashSet<string>();
        int shown = 0;
        foreach (var s in list)
        {
            string line = s.Length > 200 ? s.Substring(0, 200) : s;
            if (!seen.Add(line)) continue;
            sb.AppendLine($"- `{line.Replace("`", "'")}`");
            if (++shown >= 5) break;
        }
    }
}
