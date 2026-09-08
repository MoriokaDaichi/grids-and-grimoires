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
    public bool runEconomy = true;           // 周のあいだに建造/杖製作/変換/交換/タスク受領を貪欲に回す

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

            // 経済パス（周をまたいだ成長）。run1 は素材ゼロなのでほぼ空振り、run2 以降で効く。
            if (runEconomy)
            {
                try { RunEconomyPass(); }
                catch (Exception e) { logErrors.Add("ECON: " + e.Message); }
                grid.ApplyWandTierSize(); // 杖を作ったら 4×4 に広げてから再配置
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

    private int econTasksClaimed, econBuilds, econCrafts, econTransmutes, econTrades;

    // 周のあいだの貪欲な経済処理。何も進まなくなるまで（上限つき）回す。
    private void RunEconomyPass()
    {
        var inv = PlayerInventory.Instance;
        var money = MoneyManager.Instance;
        var hideout = HideoutManager.Instance;
        var trade = TradeManager.Instance;
        if (inv == null || hideout == null) return;

        // クリティカルパス優先: 魔力炉→錬金釜（素材→結晶の黒字化）→作業台（杖＝グリッド拡大）→研究机。
        // マジックサークルは自動プレイの導線に乗らない（実時間待ち）ので建てない。
        var buildOrder = new[]
        {
            FacilityKind.ManaFurnace, FacilityKind.AlchemyCauldron,
            FacilityKind.Workbench, FacilityKind.ResearchDesk,
        };

        for (int pass = 0; pass < 16; pass++)
        {
            bool did = false;

            // 1. 達成済みタスクを受け取る（結晶・お金・装備・レシピが入る）
            if (trade != null)
            {
                foreach (var tr in trade.Traders)
                {
                    foreach (var task in trade.VisibleTasks(tr))
                    {
                        if (trade.CanClaim(task)) { trade.ClaimTask(task); econTasksClaimed++; did = true; }
                    }
                }
            }

            // 2. 結晶を小に崩す（設備Lv1 の建材はすべて小結晶＝S(12〜24)）。glen の 大→中→小。
            //    小結晶が 45 未満のあいだ、中・大を残さず崩す（燃料は fuel ステップが小結晶の余剰から作る）。
            if (trade != null)
            {
                var med2small = FindCrystalOffer(trade, MaterialType.MediumManaCrystal, MaterialType.SmallManaCrystal);
                var large2med = FindCrystalOffer(trade, MaterialType.LargeManaCrystal, MaterialType.MediumManaCrystal);
                int guard = 0;
                while (CrystalCount(inv, MaterialType.SmallManaCrystal) < 45 && guard++ < 40)
                {
                    bool moved = false;
                    if (med2small != null && CrystalCount(inv, MaterialType.MediumManaCrystal) > 0 && trade.CanTrade(med2small))
                    { trade.TryTrade(med2small); econTrades++; moved = true; }
                    else if (large2med != null && CrystalCount(inv, MaterialType.LargeManaCrystal) > 0 && trade.CanTrade(large2med))
                    { trade.TryTrade(large2med); econTrades++; moved = true; }
                    if (!moved) break;
                    did = true;
                }
            }

            // 2b. 余剰ゴールドがあるときだけ お金→結晶（80G以上残す）
            if (trade != null && money != null)
            {
                foreach (var offer in trade.Offers)
                {
                    if (offer == null || offer.giveMoney <= 0) continue;
                    if (offer.give != null && offer.give.Count > 0) continue;
                    if (!ReceivesCrystal(offer)) continue;
                    int safety = 0;
                    while (money.Balance - offer.giveMoney >= 80 && trade.CanTrade(offer) && safety++ < 20)
                    { trade.TryTrade(offer); econTrades++; did = true; }
                }
            }

            // 3. 建造（順序固定・Lv1）
            foreach (var kind in buildOrder)
            {
                if (!hideout.IsBuilt(kind) && hideout.CanAdvance(kind))
                { hideout.Advance(kind); econBuilds++; did = true; }
            }

            // 4. 燃料投入。全設備アクション（建造/強化/変換/製作）の前提なので、常に薄いバッファを保つ。
            //    中・大結晶を優先。小結晶は「建材ぶん（最大 S(24)）＋余裕」を超える余剰があるときだけ燃料に回す。
            if (hideout.IsBuilt(FacilityKind.ManaFurnace) && hideout.Fuel < 15)
            {
                foreach (var ct in new[] { MaterialType.MediumManaCrystal, MaterialType.LargeManaCrystal })
                {
                    int safety = 0;
                    while (hideout.Fuel < 15 && CrystalCount(inv, ct) > 0 && hideout.LoadFuel(ct) && safety++ < 20)
                        did = true;
                }
                int safety2 = 0;
                while (hideout.Fuel < 15 && CrystalCount(inv, MaterialType.SmallManaCrystal) > 34
                       && hideout.LoadFuel(MaterialType.SmallManaCrystal) && safety2++ < 30)
                    did = true;

                // 完全な燃料切れ（他に結晶が無い）を避けるための最終手段：小結晶が少しでもあれば数個だけ回す。
                int safety3 = 0;
                while (hideout.Fuel == 0 && CrystalCount(inv, MaterialType.SmallManaCrystal) >= 6
                       && hideout.LoadFuel(MaterialType.SmallManaCrystal) && safety3++ < 8)
                    did = true;
            }

            // 5. 杖を打つ（無ければ最低 tier を1本）→ グリッド拡大
            if (CurrentWandTier(hideout) == 0)
            {
                foreach (var g in GearCatalog.All)
                {
                    if (g.slot != GearSlot.Wand) continue;
                    if (hideout.CanCraft(g)) { hideout.Craft(g); econCrafts++; did = true; break; }
                }
            }
            // 6. サステイン系アクセを1つ（ウェーブ間回復＝バースト対策）
            if (!OwnsAccessory(hideout))
            {
                foreach (var g in GearCatalog.All)
                {
                    if (g.slot != GearSlot.Accessory || !g.HasSustain) continue;
                    if (hideout.CanCraft(g)) { hideout.Craft(g); econCrafts++; did = true; break; }
                }
            }

            // 7. モンスター素材を変換（錬金釜）。ただし未建造設備・未製作装備の建材になる素材は温存する
            //    （cauldron が建材を先に食い潰して作業台/杖が永遠に建たない事故を防ぐ）。
            if (hideout.IsBuilt(FacilityKind.AlchemyCauldron))
            {
                var protectedParts = CollectProtectedParts(hideout);
                foreach (var kv in new List<KeyValuePair<string, int>>(inv.Counts))
                {
                    var sample = inv.Sample(kv.Key);
                    if (sample == null || sample.materialType != MaterialType.SpecialItem) continue;
                    int keep = protectedParts.Contains(sample.specialItemName) ? 5 : 0;
                    int safety = 0;
                    while (inv.GetCount(sample) > keep && hideout.CanTransmute(sample, 1) && hideout.Transmute(sample, 1) && safety++ < 40)
                    { econTransmutes++; did = true; }
                }
            }

            // 8. Lv2 強化（燃料黒字化＝魔力炉/錬金釜を優先）
            foreach (var kind in new[] { FacilityKind.ManaFurnace, FacilityKind.AlchemyCauldron, FacilityKind.Workbench, FacilityKind.ResearchDesk })
            {
                if (hideout.Level(kind) == 1 && hideout.CanAdvance(kind))
                { hideout.Advance(kind); econBuilds++; did = true; }
            }

            if (!did) break;
        }
    }

    private static int CrystalCount(PlayerInventory inv, MaterialType t)
    {
        return inv.GetCount(new MaterialCost { materialType = t });
    }

    // give が単一の giveType 結晶、receive に receiveType 結晶を含み、お金が絡まないオファー。
    private static TradeOffer FindCrystalOffer(TradeManager trade, MaterialType giveType, MaterialType receiveType)
    {
        foreach (var o in trade.Offers)
        {
            if (o == null || o.giveMoney > 0 || o.gainMoney > 0) continue;
            if (o.give == null || o.give.Count != 1 || o.give[0].materialType != giveType) continue;
            if (o.receive == null) continue;
            foreach (var r in o.receive)
                if (r != null && r.materialType == receiveType) return o;
        }
        return null;
    }

    private static bool ReceivesCrystal(TradeOffer offer)
    {
        if (offer.receive == null) return false;
        foreach (var r in offer.receive)
            if (r != null && MaterialCatalog.Value(r.materialType) > 0) return true;
        return false;
    }

    private static int CurrentWandTier(HideoutManager h)
    {
        int best = 0;
        foreach (var id in h.OwnedGear)
        {
            var g = GearCatalog.Get(id);
            if (g != null && g.slot == GearSlot.Wand && g.tier > best) best = g.tier;
        }
        return best;
    }

    // 未建造の設備の次コスト＋未所持スロットの最安装備コストに含まれるモンスター素材名。
    private static HashSet<string> CollectProtectedParts(HideoutManager h)
    {
        var set = new HashSet<string>();
        foreach (FacilityKind k in Enum.GetValues(typeof(FacilityKind)))
        {
            if (h.IsBuilt(k)) continue;
            AddParts(set, h.NextCost(k));
        }
        AddParts(set, CheapestGearCost(h, GearSlot.Wand));
        AddParts(set, CheapestGearCost(h, GearSlot.Accessory));
        AddParts(set, CheapestGearCost(h, GearSlot.Armor));
        return set;
    }

    private static void AddParts(HashSet<string> set, List<MaterialCost> cost)
    {
        if (cost == null) return;
        foreach (var c in cost)
            if (c != null && c.materialType == MaterialType.SpecialItem && !string.IsNullOrEmpty(c.specialItemName))
                set.Add(c.specialItemName);
    }

    private static List<MaterialCost> CheapestGearCost(HideoutManager h, GearSlot slot)
    {
        GearDef best = null;
        foreach (var g in GearCatalog.All)
        {
            if (g.slot != slot || g.recipeGated || h.HasGear(g.id)) continue;
            if (best == null || g.tier < best.tier) best = g;
        }
        return best != null ? best.cost : null;
    }

    private static bool OwnsAccessory(HideoutManager h)
    {
        foreach (var id in h.OwnedGear)
        {
            var g = GearCatalog.Get(id);
            if (g != null && g.slot == GearSlot.Accessory) return true;
        }
        return false;
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
        sb.AppendLine($"- 種別: 自動ドライバ（`PlaytestAutopilot`{(runEconomy ? " v2・戦闘＋貪欲経済" : " 戦闘ループのみ")}）");
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

        if (runEconomy)
        {
            sb.AppendLine("## 経済（終了時点）");
            sb.AppendLine();
            var hideout = HideoutManager.Instance;
            var money = MoneyManager.Instance;
            var inv = PlayerInventory.Instance;
            var research = FindFirstObjectByType<ResearchManager>();
            if (hideout != null)
            {
                sb.AppendLine($"- 設備Lv: 魔力炉{hideout.Level(FacilityKind.ManaFurnace)} 研究机{hideout.Level(FacilityKind.ResearchDesk)} 錬金釜{hideout.Level(FacilityKind.AlchemyCauldron)} 作業台{hideout.Level(FacilityKind.Workbench)} サークル{hideout.Level(FacilityKind.MagicCircle)} / 燃料{hideout.Fuel}");
                var gear = new List<string>();
                foreach (var id in hideout.OwnedGear) { var g = GearCatalog.Get(id); gear.Add(g != null ? g.name : id); }
                sb.AppendLine($"- 装備: {(gear.Count > 0 ? string.Join(", ", gear) : "なし")}（杖 tier{CurrentWandTier(hideout)}）");
            }
            if (money != null) sb.AppendLine($"- 所持金: {money.Balance}G");
            if (inv != null)
            {
                int sc = inv.GetCount(new MaterialCost { materialType = MaterialType.SmallManaCrystal });
                int mc = inv.GetCount(new MaterialCost { materialType = MaterialType.MediumManaCrystal });
                int lc = inv.GetCount(new MaterialCost { materialType = MaterialType.LargeManaCrystal });
                sb.AppendLine($"- 結晶: 小{sc} 中{mc} 大{lc}");
            }
            sb.AppendLine($"- 経済アクション: タスク受領{econTasksClaimed} / 建造・強化{econBuilds} / 製作{econCrafts} / 変換{econTransmutes} / 交換{econTrades}");
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
        sb.AppendLine("_ハーネスの限界: 経済は貪欲エージェント近似（人力の結晶配分・杖の手詰めより下手）。魔法配置は greedy first-fit で回転のみ。「脱出」判定はウェーブ間だけ（1ウェーブ内のバースト即死は拾えるが事前脱出はできない）。研究の小ノード割当は未駆動。_");
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
