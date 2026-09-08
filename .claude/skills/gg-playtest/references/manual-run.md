# モード B: 手動ディープラン スニペット集

`execute_code`（Unity MCP、compiler=codedom / C# 6・**型定義や local function は不可**、匿名デリゲートは可）で駆動する。人力に近い経済判断が要る／到達深度の絶対値が要るとき用。1周1時間・ツール呼び 100+ を覚悟する。

前例（再検証3, 2026-09-09）: cold start 5周で壁 **11 → 12 → 15 → 14 → 16**。1トライ目は結晶配分をミスって深度9でハードロック → wipe 再開。

---

## 0. セットアップ

```
manage_editor(action="stop")
execute_menu_item("Grimoire/Wipe Save")
execute_code:  return "exists=" + System.IO.File.Exists(Application.persistentDataPath + "/gg_save.json");
manage_editor(action="play")
```

cold start の確認（HP130/$80/素材0/設備0/grid 3x3/敵40/base-free 魔法10 なら OK）。

---

## 1. 常駐オートドライバ（1回だけ差す）

`EditorApplication.update` に匿名デリゲートを差す。再生モード中も毎エディタフレーム発火する。ウェーブ突破を自動判定し、報酬画面を自動で帰還させ、結果を `PlayerPrefs` に置く。ドメインリロード（＝コード再コンパイル）で消えるので、その時は差し直す。二重差ししても各アクションは冪等（`ContinueRun`/`EscapeRun` は phase 不一致なら no-op）。

```csharp
if (UnityEditor.EditorPrefs.GetBool("gg_driver_installed", false)) return "already installed";
UnityEditor.EditorPrefs.SetBool("gg_driver_installed", true);
PlayerPrefs.SetInt("gg_running",0); PlayerPrefs.SetInt("gg_lastdepth",0);
PlayerPrefs.SetInt("gg_lastcleared",0); PlayerPrefs.SetInt("gg_waves",0); PlayerPrefs.SetInt("gg_minhpfrac",100);
UnityEditor.EditorApplication.CallbackFunction drv=null;
drv=delegate{
  try{
    var g=UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>(); if(g==null) return;
    var d=UnityEngine.Object.FindFirstObjectByType<DungeonManager>();
    var p=UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
    if(g.Current==GamePhaseManager.GamePhase.Battle && p!=null && p.hp>0){
      int cur=Mathf.RoundToInt(100f*p.currentHp/p.hp); int mn=PlayerPrefs.GetInt("gg_minhpfrac",100);
      if(cur<mn) PlayerPrefs.SetInt("gg_minhpfrac",cur);
    }
    if(g.Current==GamePhaseManager.GamePhase.WaveClear && d!=null && p!=null){
      int cap=PlayerPrefs.GetInt("gg_cap",999); float esc=PlayerPrefs.GetFloat("gg_esc",0.4f);
      float frac=p.hp>0?(float)p.currentHp/p.hp:0f;
      PlayerPrefs.SetInt("gg_waves",PlayerPrefs.GetInt("gg_waves",0)+1);
      if(d.Depth>=cap||frac<=esc) g.EscapeRun(); else g.ContinueRun();
    } else if(g.Current==GamePhaseManager.GamePhase.Reward){
      var rs=UnityEngine.Object.FindFirstObjectByType<RewardScreen>();
      if(rs!=null){
        var mi=typeof(RewardScreen).GetMethod("OnReturn",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        PlayerPrefs.SetInt("gg_lastdepth",d!=null?d.Depth:0);
        PlayerPrefs.SetInt("gg_lastcleared",g.LastRunCleared?1:0);
        if(mi!=null) mi.Invoke(rs,null);   // ← これが戦利品付与。ReturnToBuild() だけでは入らない
      }
      PlayerPrefs.SetInt("gg_running",0);
    }
  }catch(System.Exception){}
};
UnityEditor.EditorApplication.update+=drv;
return "driver installed";
```

終了時は `UnityEditor.EditorPrefs.DeleteKey("gg_driver_installed")` してから `manage_editor stop`。

---

## 2. sortie 開始 + ポーリング

```csharp
// 出撃（farm は cap 低め/esc 高め、push は cap=99/esc≈0.1）
var g=UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
Time.timeScale=40f;
PlayerPrefs.SetInt("gg_cap",8); PlayerPrefs.SetFloat("gg_esc",0.42f);
PlayerPrefs.SetInt("gg_running",1); PlayerPrefs.SetInt("gg_waves",0); PlayerPrefs.SetInt("gg_minhpfrac",100);
g.StartSortie();
return "start "+g.Current;
```

```csharp
// ポーリング（run==0 になったら1周終了）。数回叩けば終わる（timeScale 40 で 1周 数秒〜十数秒の実時間）
return "run="+PlayerPrefs.GetInt("gg_running")
 +" phase="+UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().Current
 +" d"+UnityEngine.Object.FindFirstObjectByType<DungeonManager>().Depth
 +" wv="+PlayerPrefs.GetInt("gg_waves")
 +" lastdepth="+PlayerPrefs.GetInt("gg_lastdepth")
 +" cleared="+PlayerPrefs.GetInt("gg_lastcleared")
 +" minhp%="+PlayerPrefs.GetInt("gg_minhpfrac");
```

`cleared=1` = 脱出（戦利品あり）、`cleared=0` = 戦闘不能（没収）。`lastdepth` は最終到達深度。

---

## 3. ステ配分 + 魔法配置

```csharp
var ps=UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
string[] pat=new string[]{"HP","HP","Def","Atk","HP","Def","Atk","HP","Def","Atk","HP","Def","Atk","HP","Def"};
int i=0; while(ps.statsPoint>0 && i<pat.Length){ ps.AddStat(pat[i]); i++; }
// 配置: 解放済み攻撃魔法を「強い単体2 + AoE1 + 詰め」の優先で、回転4方向を試して greedy first-fit。
var grid=UnityEngine.Object.FindFirstObjectByType<MagicGridManager>();
var spawner=UnityEngine.Object.FindFirstObjectByType<MagicSpawner>();
var rm=ResearchManager.Instance;
grid.ApplyWandTierSize();                         // ← 杖 tier をグリッドサイズに反映（フェーズ遷移待ちしない）
foreach(var m in grid.GetPlacedMagics()) grid.RemoveMagic(m);
System.Func<System.Collections.Generic.List<Vector2Int>,int,System.Collections.Generic.List<Vector2Int>> rot=delegate(System.Collections.Generic.List<Vector2Int> s,int k){
  var c=new System.Collections.Generic.List<Vector2Int>(s);
  for(int r=0;r<k;r++){ var n=new System.Collections.Generic.List<Vector2Int>(); foreach(var v in c) n.Add(new Vector2Int(v.y,-v.x)); c=n; } return c; };
System.Func<string,bool> place=delegate(string nm){
  MagicData md=null; foreach(var x in spawner.magicDataList) if(x!=null&&x.name==nm) md=x;
  if(md==null||!rm.IsUnlocked(md)) return false;
  for(int k=0;k<4;k++){ var sh=rot(md.shapeNodes,k);
    for(int y=0;y<grid.height;y++) for(int x=0;x<grid.width;x++){
      var gp=new Vector2Int(x,y); bool ok=true;
      foreach(var v in sh){ int tx=gp.x+v.x,ty=gp.y+v.y; if(tx<0||tx>=grid.width||ty<0||ty>=grid.height){ok=false;break;} }
      if(!ok) continue;
      MagicData tp=md; if(k>0){ tp=ScriptableObject.Instantiate(md); tp.name=md.name; tp.shapeNodes=new System.Collections.Generic.List<Vector2Int>(sh); }
      if(grid.CanPlace(tp,gp)){ grid.RegisterMagic(tp,gp); return true; } else if(k>0) ScriptableObject.Destroy(tp);
    } } return false; };
var got=new System.Collections.Generic.List<string>();
foreach(var nm in new string[]{"GigaFire","GigaThunder","MegaFlame","MegaFire","MegaThunder","Flame","Bolt","Fire","Thunder","Wind"}) if(place(nm)) got.Add(nm);
return grid.width+"x"+grid.height+" ["+string.Join(",",got.ToArray())+"]";
```

回転コピーは元 `MagicData` を汚さないよう `ScriptableObject.Instantiate`。戦闘は category/damage/interval しか見ないのでコピーで問題ない。**greedy first-fit は人間の手詰めより下手**（GigaFire〈6セルの階段〉＋大型AoE が 4×4 に同居しにくい）＝結果を割り引いて読む。

---

## 4. 経済パス

周のあいだに貪欲に回す（内部で fixpoint ループ、~1回の execute_code で吸い切る）。優先度:

1. **結晶の下方リファイン**（＝唯一の cold-start 結晶 faucet）: glen `大→中×10` → `中→小×10`。small が枯れたら med→small、それも無ければ現金で `オルカ 120G→大×1` / `ダグ 80G→中×1`。
2. 設備 Lv1 建造（魔力炉→作業台→研究机→錬金釜。マジックサークルは低優先）。
3. **見習いの杖を最優先で製作**（小×15＋ゴブリンの牙×3 → グリッド 4×4）。次にサステイン系アクセ1個、防具1個。**同スロットの churn（作り直し）はしない**。
4. モンスター素材の変換: 錬金釜 Lv1 では tier1 が燃料中立〜微益なので**無理に回さない**。錬金釜 Lv2＋魔力炉 Lv2 になって初めて tier1 が純増（-1燃料+小2）→ 溜めた素材を一気に結晶化（前例: 80個超 → 小 277）。未建造設備・未製作装備の建材素材は keep する。
5. 研究割当: `foreach ResearchGraph.Nodes → rm.CanAllocate/Allocate`。魔法ノードは常に、ステノードは Atk>Def>Hp>Spd を優先、**ManaMax/ManaRegen/Luc はスキップ**（マナは基本足りる、1トライ目で全部溶かした失敗例あり）。小結晶を 20 前後は残す。
6. 設備 Lv2 強化（魔力炉/錬金釜を優先＝燃料黒字化）。
7. タスク受領: `foreach trader.tasks → tm.CanClaim/ClaimTask`。
8. 余剰の tier2 素材はダグに売却（$150 未満のときだけ）。

中盤ブレイクスルーの型（前例 3周目）: **tier2素材を売って $146 → オルカで大結晶×1（$120）→ glen で大→中×10 → 錬金釜Lv2 建造（中×3〜5＋剛毛×2＋蜘蛛の糸×2）→ 魔力炉Lv2 → tier1 変換が黒字化 → 研究が 28→40→53 に一気に伸びる**。

主要 API: `HideoutManager`（`CanAdvance/Advance/CanCraft/Craft/CanTransmute/Transmute/LoadFuel/Fuel/CanPowerFacilityAction/Level/NextCost/OwnedGear`）／`ResearchManager.Instance`（`CanAllocate/Allocate/IsIdAllocated`, `ResearchGraph.Nodes[].id/.isMagic/.stat`）／`TradeManager.Instance`（`Traders[].offers/.tasks`, `CanTrade/TryTrade/CanClaim/ClaimTask`, `TradeOffer.give/receive/giveMoney/gainMoney`）／`PlayerInventory.Instance`（`Counts`, `Sample(key)`, `GetCount(MaterialCost)`）／`MoneyManager.Instance.Balance`／`MonsterPartCatalog.TierOf(name)`／`MagicGridManager.CurrentWandTier()`。

---

## 5. スナップショット（周の頭と尻）

```csharp
var inv=PlayerInventory.Instance; var hm=HideoutManager.Instance; var rm=ResearchManager.Instance;
var tm=TradeManager.Instance; var mm=MoneyManager.Instance; var ps=UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
var grid=UnityEngine.Object.FindFirstObjectByType<MagicGridManager>();
System.Func<MaterialType,int> C=delegate(MaterialType t){ int s=0; foreach(var kv in inv.Counts){ var mc=inv.Sample(kv.Key); if(mc!=null&&mc.materialType==t) s+=kv.Value; } return s; };
int at=0,mt=0; foreach(var nd in ResearchGraph.Nodes) if(rm.IsIdAllocated(nd.id)){at++; if(nd.isMagic) mt++;}
int td=0; foreach(var tr in tm.Traders) foreach(var tk in tr.tasks) if(tm.IsTaskCompleted(tk)) td++;
return "HP"+ps.hp+" Atk"+ps.atk+" Def"+ps.def+" Spd"+ps.spd+" mana"+ps.maxMana+"/"+ps.manaRegenPerSecond+" hpR/s"+ps.hpRegenPerSecond+" wv%"+ps.healPerWavePercent
+" | $"+mm.Balance+" S"+C(MaterialType.SmallManaCrystal)+" M"+C(MaterialType.MediumManaCrystal)+" L"+C(MaterialType.LargeManaCrystal)+" Pt"+C(MaterialType.SpecialItem)
+" | res"+at+"(mg"+mt+") facF"+hm.Level(FacilityKind.ManaFurnace)+"W"+hm.Level(FacilityKind.Workbench)+"D"+hm.Level(FacilityKind.ResearchDesk)+"C"+hm.Level(FacilityKind.AlchemyCauldron)+"M"+hm.Level(FacilityKind.MagicCircle)
+" grid"+grid.width+"x"+grid.height+" gear=["+string.Join(",",new System.Collections.Generic.List<string>(hm.OwnedGear).ToArray())+"] tasks"+td;
```

---

## 6. 1周の型

```
[econ パス] → [ステ配分＋配置] → farm ×2〜3（cap=壁-2, esc≈0.45, 脱出で戦利品確保）
           → [econ パス]（溜めた素材を処理）→ [再配置（杖/研究が変わったら）]
           → push ×1（cap=99, esc≈0.1, 壁で死ぬ）→ lastdepth を記録 → [スナップショット]
```

farm で `minhp%` が 3% 台に落ちる（＝ウェーブ内バースト）ことがある。深度 13+ で頻発。cap を 2〜3 下げて回避。

---

## 7. 落とし穴（このセッションで踏んだ）

- **`RewardScreen.OnReturn` は private**。呼ばないと `pendingDrops` が付与されず素材が増えない。`phase.ReturnToBuild()` は画面遷移だけ。
- **グリッドのリサイズは `GamePhaseManager.OnPhaseChanged(Build)` でしか走らない**。Build に居たまま杖を作っても 3×3 のまま。`grid.ApplyWandTierSize()` を明示。
- **`execute_code` の `replay` はヒストリ ~30 超で信用できない**（別インデックスを叩いて別スニペットが走る）。小スニペットは毎回ベタ貼り。econ など巨大なものだけ replay、直後に出力の形で正しいか確認。
- **cold-start の結晶は有限**（dag_1/orca_1/glen_1/dag_2/glen_2-3 の中結晶を glen で崩したぶんだけ）。研究やマナに溶かすと**杖が買えず 3×3 のまま詰む**。→ 杖 → Atk/Def/HP研究 → その他 の順で。
- 錬金釜 Lv1 の tier1 変換で経済を回そうとしない（燃料中立〜微益）。回るのは錬金釜 Lv2＋魔力炉 Lv2 から。
- `Time.timeScale` は 25〜45。戦闘の実時間は timeScale 40 で 1ウェーブ 0.3〜0.5 秒。
- 検証後は **`manage_editor stop` してから `run_tests`**（Play 中は起動不可）。`EditorPrefs "gg_driver_installed"` も掃除。
