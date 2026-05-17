# Fearless (无畏值) 系统设计文档

## 1. 系统概述

无畏值（Fearless）是一个反映殖民者勇气状态的核心属性，显示在殖民者的状态栏中。高无畏值意味着殖民者在战斗中保持冷静，低无畏值会导致恐惧和恐慌。

## 2. 核心组件

### 2.1 FearnessComponent

**职责**: 管理殖民者的无畏值状态和计算逻辑

**关键属性**:
| 属性 | 类型 | 说明 |
|------|------|------|
| `curLevelInt` | float | 当前无畏值（0-100） |
| `lastLevelInt` | float | 上一时刻无畏值 |
| `MaxLevel` | float | 最大无畏值（100） |
| `CurLevel` | float | 当前无畏值（带边界限制） |

**核心方法**:
| 方法 | 功能 |
|------|------|
| `Initialize()` | 初始化无畏值为最大值（100%） |
| `AddFearnessReduction()` | 处理受伤时的无畏值减少 |
| `FearnessInterval()` | 周期性更新（恢复/减少） |
| `CompTick()` | 每200tick调用一次更新 |

### 2.2 Need_Fearness

**职责**: 在殖民者需求面板中显示无畏值进度条

**配置**:
- `defName`: Fearness
- `needClass`: Fearness.Need_Fearness
- `showOnNeedList`: true
- `major`: true

## 3. 数值配置

所有配置集中在 `Define` 类中：

| 配置项 | 值 | 说明 |
|--------|-----|------|
| `FearnessMaxLevel` | 100f | 无畏值最大值 |
| `FearnessDecayRate` | 0.5f | 基础恢复速率 |
| `FearnessBaseDamageReduction` | 15f | 受伤时基础减少值 |
| `FearnessBleedingReductionFactor` | 0.1f | 出血减少系数 |

## 4. 核心逻辑

### 4.1 受伤时无畏值减少

```
实际减少值 = 基础减少值 × (1 - (勇气等级 - 1) / 19 × 0.5)
```

勇气等级越高，受伤时无畏值减少越少。

### 4.2 周期性恢复

```
恢复速率 = 基础恢复速率 × (1 + (勇气等级 - 1) / 19 × 0.5)
恢复量 = 恢复速率 × 心情因子
```

心情越好，恢复越快；勇气等级越高，恢复越快。

### 4.3 严重出血影响

当失血效率超过 50% 时：
1. 添加 "严重失血" 心情 debuff（-15 心情）
2. 无畏值持续减少（每秒减少基础值的 10%）

## 5. 状态判定

根据当前无畏值判定状态：

| 无畏值范围 | 状态 | 效果 |
|-----------|------|------|
| 70-100 | Brave | 冷静状态 |
| 30-70 | Normal | 正常状态 |
| 0-30 | Panicked | 恐慌状态 |

## 6. 数据持久化

通过 `IExposable` 接口实现存档：

```csharp
public void ExposeData()
{
    Scribe_Values.Look(ref curLevelInt, "curLevelInt", 100f);
    Scribe_Values.Look(ref lastLevelInt, "lastLevelInt", 100f);
}
```

## 7. 翻译支持

翻译文件位置：`Languages/ChineseSimplified/DefInjected/NeedDef/Fearness.xml`

| 翻译键 | 中文 |
|--------|------|
| `Fearness.label` | 无畏值 |
| `Fearness.description` | 殖民者当前的无畏值... |

## 8. 依赖关系

- **CourageComponent**: 勇气等级影响无畏值变化
- **ThoughtWorker_SevereBleeding**: 检测严重出血状态
- **HarmonyPatches**: 初始化组件和处理伤害事件
