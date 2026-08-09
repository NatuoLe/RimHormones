# 肾上腺素（Adrenaline）数值影响清单

> 浓度由 `Adrenaline` Hediff 的 Severity（0~1）决定，分 4 档：
> - **Dormant** `S < 0.15`：无任何效果
> - **Low** `0.15 ≤ S < 0.5`
> - **Medium** `0.5 ≤ S < 0.75`
> - **High** `0.75 ≤ S ≤ 1.0`
>
> 数据来源：`Define.cs` 的 `AdrenalineLow/Medium/High` 常量 + `Hediff_Adrenaline.xml` 的 stage + `HormonesLogic.cs` 的 Harmony 注入。

---

## 一、浓度 → 数值影响

### A 路：身体容量 / 减痛 / 休息（XML 原生字段，静态，**不随体魄变化**）

| 数值 | Low | Medium | High | 含义 |
|---|---|---|---|---|
| 视觉 Sight（容量 offset，负=下降） | −0.08 | −0.14 | −0.20 | 肾上腺越高，视物越模糊（应激副作用） |
| 听觉 Hearing | −0.08 | −0.14 | −0.20 | 听声越不清晰 |
| 呼吸 Breathing | +0.07 | +0.13 | +0.20 | 供氧提升 |
| 意识 Consciousness | +0.03 | +0.05 | +0.08 | 专注提升 |
| 血液过滤 BloodFiltration | +0.07 | +0.13 | +0.20 | 循环提升 |
| 减痛 painFactor（<1=更耐痛） | 0.93 | 0.87 | 0.80 | 承受同样伤害时"感到的痛"按比例降低（参考 Tough 特性） |
| 休息下降 restFallFactor | 1.0 | 0.8 | 0.667 | **待移除**（用户决定先不开发） |

> capacity 修正为加法 offset（叠加到该 PawnCapacity 基础值上）；painFactor 为乘法。

### B 路：战斗 / 体能 Stat（Harmony 注入，数值 = Define 常量 × 体魄修正）

| Stat | Low | Medium | High | 注入方式 |
|---|---|---|---|---|
| 移动速度 MoveSpeed | +4% | +7% | +10% | `__result × (1+v)` |
| 近战伤害 MeleeDamageFactor | +6% | +12%→+20% | +20% | `__result × (1+v)` |
| 闪避 MeleeDodgeChance | +3.6% | +7.2% | +12% | `__result × (1+v)` |
| 近战命中 MeleeHitChance | −2.4% | −4.8% | −8% | `__result × (1+v)`（v 为负=降命中，应激手抖） |
| 饭量消耗 Metabolism（Need_Food） | +13% | +26% | +40% | `__result × (1+v)` |

> 注：MeleeHitReduction 当前为惩罚值，用户要求"降低 3"，**待确认具体含义后调整**（见文末待办）。

---

## 二、体魄（physiqueLevel）对这些数值的影响

体魄等级取自 `PhysiqueLgc.GetPhysiqueLevel`；相关阈值在 `PhysiqueDefine.cs:67-69`：
- `PhysiqueAdrenalinePenaltyThreshold = 8`
- `PhysiqueAdrenalineExemptionThreshold = 13`
- `PhysiqueAdrenalinePenaltyFactor = 0.5`

### B 路 Stat（真实受体魄影响）
| 体魄等级 | 移动/近战伤害/闪避/饭量 | 近战命中惩罚 |
|---|---|---|
| `< 8` | × 0.5（效果砍半） | × 0.5 后再计入（仍为负惩罚） |
| `8 ~ 12` | × 1.0（全额） | 全额惩罚 |
| `≥ 13` | × 1.0（全额） | **归零**（不再降命中） |

### A 路 容量 / 减痛 / 休息（当前实际不受影响）
- 视觉/听觉/呼吸/意识/血液过滤/减痛/restFallFactor **完全静态**，对所有殖民者一视同仁，**体魄 0 影响**。
- 代码中虽有 `visionHearingExempt`（体魄≥13 时把视觉/听觉惩罚归零）的逻辑，但**实际生效走 XML 写死值，该豁免没接上** → 即便体魄巅峰，高浓度时视觉仍 −0.20。

---

## 三、当前不一致 / 待确认

1. **restFallFactor 先不开发** → 将从 `Hediff_Adrenaline.xml` 的 3 个 stage 移除。
2. **MeleeHitReduction 降低 3** → 当前 −2.4% / −4.8% / −8%，待确认目标含义后改动（见对话提问）。
3. **A 路容量是否要随体魄变化** → 现状是静态。若要体魄≥13 豁免视觉/听觉、其余按体魄缩放，需把 `capMods` 改为代码注入（工程量较大）。维持现状则 A 路永远不吃体魄。

---

## 四、本次清理（用户已同意）

`AdrenalineEffects` 类中以下字段**仅在 C# 赋值、从未被注入**（实际由 XML 兜底），属冗余，将删除：
`Consciousness / Respiratory / Circulation / BloodFiltration / PainReduction / VisionReduction / HearingReduction / RestMultiplier`（及其在 `CalculateAdrenalineEffects` 中的赋值）。
保留实际注入用字段：`MoveSpeed / MeleeDamage / Dodge / MeleeHitReduction / Metabolism`（+ `Level / PhysiqueModifier`）。
