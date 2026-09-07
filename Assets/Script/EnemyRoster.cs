using System;
using System.Collections.Generic;
using UnityEngine;

// 現在のウェーブの敵インスタンス群を管理する。シーンにシングルトンで1つ。
// 敵GameObjectは起動時にプール生成し、SpawnWave で必要数を有効化・Setupする。
public class EnemyRoster : MonoBehaviour
{
    public static EnemyRoster Instance { get; private set; }

    [SerializeField] private int poolSize = 6;

    private readonly List<EnemyStatus> pool = new List<EnemyStatus>();
    private readonly List<EnemyStatus> living = new List<EnemyStatus>();
    public IReadOnlyList<EnemyStatus> Living { get { return living; } }

    // ウェーブの敵構成が変わった / 全滅した通知
    public Action OnRosterChanged;
    public Action OnWaveDefeated;

    // ウェーブ切り替えを検知するための世代番号（BattleManager等の再入ガード用）
    public int Generation { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = new GameObject("Enemy" + i);
            go.transform.SetParent(transform, false);
            EnemyStatus es = go.AddComponent<EnemyStatus>();
            EnemyStatus captured = es;
            es.OnDefeated += () => HandleEnemyDefeated(captured);
            go.SetActive(false);
            pool.Add(es);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public EnemyStatus FirstAlive()
    {
        for (int i = 0; i < living.Count; i++)
        {
            if (living[i] != null && living[i].hp > 0) return living[i];
        }
        return null;
    }

    public int AliveCount()
    {
        int n = 0;
        for (int i = 0; i < living.Count; i++)
        {
            if (living[i] != null && living[i].hp > 0) n++;
        }
        return n;
    }

    public void SpawnWave(IList<EnemyData> enemies)
    {
        Generation++;

        foreach (EnemyStatus es in pool) es.gameObject.SetActive(false);
        living.Clear();

        int count = Mathf.Min(enemies != null ? enemies.Count : 0, pool.Count);
        for (int i = 0; i < count; i++)
        {
            EnemyStatus es = pool[i];
            es.gameObject.SetActive(true);
            es.Setup(enemies[i]);
            living.Add(es);
        }

        OnRosterChanged?.Invoke();
    }

    public void Clear()
    {
        Generation++;
        foreach (EnemyStatus es in pool) es.gameObject.SetActive(false);
        living.Clear();
        OnRosterChanged?.Invoke();
    }

    private void HandleEnemyDefeated(EnemyStatus es)
    {
        // living からは即座に外さず hp<=0 で生存判定する。
        // 代表個体の切り替え等のためUIに変化を知らせ、全滅ならウェーブ決着を通知する。
        OnRosterChanged?.Invoke();
        if (living.Count > 0 && AliveCount() == 0)
        {
            OnWaveDefeated?.Invoke();
        }
    }
}
