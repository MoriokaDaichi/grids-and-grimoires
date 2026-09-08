using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 通しプレイ検証のエディタ側オーケストレーション。
// セーブを wipe → 再生モード突入 → シーンに PlaytestAutopilot を1つ差す → 完了レポートを
// Docs/検証レポート/_ループ_<日時>.md に書き出す → 再生モードを抜ける。
//
// 使い方:
//   - メニュー Grimoire > Run Playtest > Cold start x3 / x5
//   - もしくは MCP から:  PlaytestDriver.Begin(3, 25);
//   - 完了後のレポートパスは SessionState "gg_playtest_report" に入る。
[InitializeOnLoad]
public static class PlaytestDriver
{
    private const string ActiveKey = "gg_playtest_active";
    private const string LoopsKey = "gg_playtest_loops";
    private const string DepthCapKey = "gg_playtest_depthcap";
    private const string ReportKey = "gg_playtest_report";
    private const string DoneKey = "gg_playtest_done";

    static PlaytestDriver()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Grimoire/Run Playtest/Cold start x3")]
    public static void RunX3() => Begin(3, 25);

    [MenuItem("Grimoire/Run Playtest/Cold start x5")]
    public static void RunX5() => Begin(5, 30);

    // MCP / スクリプトから直接叩けるエントリ。
    public static void Begin(int loops, int depthCap)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlaytestDriver] すでに再生モード。先に停止してください。");
            return;
        }
        SaveManager.Wipe();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(LoopsKey, loops);
        SessionState.SetInt(DepthCapKey, depthCap);
        SessionState.SetString(ReportKey, "");
        SessionState.SetBool(DoneKey, false);
        Debug.Log($"[PlaytestDriver] cold start x{loops} を開始（セーブ wipe 済み）。再生モードへ。");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var go = new GameObject("~PlaytestAutopilot");
            var ap = go.AddComponent<PlaytestAutopilot>();
            ap.loops = SessionState.GetInt(LoopsKey, 3);
            ap.depthCap = SessionState.GetInt(DepthCapKey, 25);
            ap.OnFinished = HandleFinished;
            Debug.Log("[PlaytestDriver] PlaytestAutopilot を差した。");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // レポート未生成のまま Edit モードに戻った＝手動停止など。フラグを畳む。
            if (!SessionState.GetBool(DoneKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
                Debug.LogWarning("[PlaytestDriver] レポート生成前に再生モードを抜けた（中断扱い）。");
            }
        }
    }

    private static void HandleFinished(string report)
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "検証レポート");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"_ループ_{DateTime.Now:yyyy-MM-dd_HHmm}.md");
            File.WriteAllText(file, report);
            SessionState.SetString(ReportKey, file);
            SessionState.SetBool(DoneKey, true);
            Debug.Log($"[PlaytestDriver] レポートを書き出した: {file}");
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaytestDriver] レポート書き出し失敗: " + e);
        }
        finally
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
        }
    }
}
