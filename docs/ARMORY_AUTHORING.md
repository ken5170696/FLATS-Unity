# 軍械庫模型與近戰調整入口

近戰模型以 `Assets/Resources/Armory/Melee/` 的八個 prefab 與其 `Meshes/` 網格資產為來源。遊戲只載入資產，不在執行期重新生成模型。所有武器 renderer 共用原版槍的 `Assets/_Flats/Art/Shared/Materials/Color_Black.mat`（`Texture Only` shader、`Black_0.png`），不另造材質。刀刃、護手、電極與盾牌視窗靠幾何輪廓辨識。

設計師可在每把 prefab 的 `RogueMeleeVisual` 調整手腕起手、蓄力、命中的位置與旋轉、兩條動畫曲線、兩個視角共用的世界尺寸、手部偏移、命中音效、格擋音效，以及大錘鏡頭晃動幅度。傷害、前搖、收招、範圍、連段與狀態數字集中於 `Core/Roguelike/Meta/RogueArmory.cs`、`MeleeRules.cs`；視覺參數不會改變命中規則。

遠程外觀透過 `RogueWeaponSkin.Apply(gunModel, def)` 套用、`Clear(gunModel)` 還原。配件 prefab 位於 `Assets/Resources/Armory/`，以局部 +Z 朝前、+Y 朝上製作；原版槍的 +Y 朝槍口、+Z 朝瞄具，LMG 則是 -X 朝槍口、+Y 朝上。`FirePosition` 是槍口參考，第三個子物件 `IronSightPosition` 的位置與子樹保持原樣。所有武器的 `Tint` 為 -1，不改 sharedMaterials；明確指定非負 Tint 的程式路徑保留，Clear 會釋放其實例。Bullpup／ShortStock 內的 `BaseModel4`、`BaseModel5`、`BaseModel10` 是保存的替換輪廓，僅對相符的基底隱藏原 renderer，不停用槍物件；Clear 還原。不要改這些配對名稱。

卡片圖示是 `Assets/Resources/UI/Roguelike/Armory/Icons/` 的 512×256 透明底白色側視剪影。檔名直接對應資料 ID（例如 `mw.knife.png`、`rw.smg1.png`）。圖示是離線渲染結果，修改模型後需由作者工具重新匯出指定圖示。

Roguelike 中按 `V`、手把十字鍵左或觸控「近戰」即可揮擊；原有近距離射擊觸發 Smash 的操作仍可使用。盾牌與斧頭使用相同按鍵長按：盾牌盾擊後持續舉盾，放開解除；斧頭蓄力後投出，走近地上斧頭拾回。按鍵與手把按鈕可在玩家的 `RogueMelee` 元件調整。

暈眩與緩速使用現有 `Ultimate`、`Reload` 圖示，分別呈黃色、藍色。狀態由 `RogueEnemyStatus` 管理；既有 `RogueEnemyRole.Slow` 會合併進同一計時與還原流程。

## 手上持握與手臂軌跡

`RogueMeleeVisual` 的 `RestPosition`、`ChargedPosition`、`HitPosition`、`GuardPosition` 是手腕目標：以角色 Chest 為原點，MeleeEye 的右／上／前為方向，單位為世界公尺。旋轉同樣相對這個方向座標。`ChargeFraction` 指定前搖中到達蓄力點的比例；剩餘前搖由蓄力點揮到命中點。命中判定時即到 Hit，Recovery 由 Hit 回 Rest。兩段使用 quaternion 插值和可編輯的 `WindupCurve`／`SwingCurve`，不改 Core 的戰鬥時間。

只有一個模型。一般武器掛 `mixamorig_RightHand`，`HandPosition`／`HandRotation` 是手骨局部的握柄對位，`ThirdPersonScale` 是兩個視角共同的世界縮放；不採用槍口朝向。`TwoHanded` 讓左掌對準模型局部 `LeftGripPosition`，`LeftGripRotation` 與世界單位 `LeftPalmOffset` 定義左腕到掌心的關係。單手的另一隻手使用 `FreeHandPosition`／`FreeHandRotation`，盾牌勾選 `LeftForearm` 後改掛左前臂並使右手採用 FreeHand。`RightElbowHint`／`LeftElbowHint` 控制肘的彎曲方向。

`RogueMeleeIK` 在既有 IKController 完成胸口瞄準旋轉後，以兩節解析 IK 覆寫手臂。它每幀保存動畫的局部旋轉，下一幀與結束時還原；不修改 IKController 的左手 IK 開關／錨點與胸口旋轉。揮擊期間暫設 Animator 為 `AlwaysAnimate`，避免相機重新判定可見性時覆回舊動畫姿勢，結束還原原有 culling mode。第一人稱沿用角色原始蒙皮身體、layer 13 及 Gun Camera，不用額外模型或 ShadowsOnly 隱藏身體。收招、換裝、倒地或停用時會清除新元件權重並還原槍 renderer。Classic Smash 不使用新元件。

調整時同時檢查第一人稱與側面。肩到目標需在角色臂長內；不可為了第一人稱任意放大獨立模型。雙手必須留在握柄，單手的空手不能停在護木位置；命中瞬間允許刀棍越過身體中線，大錘可出框，但手臂應維持在鏡頭前、視野邊緣。
