# -*- coding: utf-8 -*-
"""按「文档/Hediff/损伤Hediff与触发逻辑.xlsx」→ Hediff总览 表的关键词列，
为 Defs/HediffDefs/Hediff_StrainPool.xml 中各损伤 Hediff：
  1) 修正 <tendable>（是否可治疗列）
  2) 在 </maxSeverity> 之后插入 <modExtensions> 战斗触发标签块
幂等：已存在 modExtensions 的 Hediff 会先移除旧块再写入。
"""
import re, io

XML = r'D:\RimMods\Rim-Hormones\RimHormones\Defs\HediffDefs\Hediff_StrainPool.xml'

# defName -> (tendable, combatTriggered, onShoot, onMelee, onMeleeHitTaken, severityTier, onAdrenalineBuildup)
# severityTier: Mild / Moderate / Severe —— 决定触发后的抽取档位与飘字颜色，
#               档位比例见 Defs/MiscDefs/StrainTierRules.xml
# onAdrenalineBuildup: 【肾上腺素长期堆积造成的Hediff】列；阶段→档位映射见
#               Defs/MiscDefs/StrainAdrenalineStageRules.xml
SPEC = {
    'LaborMuscleStrain':      (True,  False, False, False, False, 'Mild',     False),
    'DiggingMuscleStrain':    (True,  False, False, False, False, 'Mild',     False),
    'CardioOverexert':        (False, False, False, False, False, 'Moderate', True),
    'SuffocationStrain':      (False, False, False, False, False, 'Moderate', True),
    'CombatJointStrain':      (True,  True,  False, True,  True,  'Mild',     False),
    'FallJointStrain':        (True,  True,  False, True,  True,  'Severe',   False),
    'CombatEnduranceExhaust': (False, False, False, False, False, 'Severe',   True),
    'MetabolicExhaust':       (False, False, False, False, False, 'Severe',   True),
    'VisualStrain':           (False, True,  True,  False, False, 'Moderate', False),
    'AuditoryStrain':         (False, True,  True,  False, False, 'Moderate', False),
}

with io.open(XML, encoding='utf-8') as f:
    text = f.read()

# 逐个 HediffDef 块处理
def process_block(m):
    block = m.group(0)
    dn = re.search(r'<defName>([^<]+)</defName>', block)
    if not dn or dn.group(1) not in SPEC:
        return block
    name = dn.group(1)
    tendable, combat, shoot, melee, hit, tier, buildup = SPEC[name]

    # 1) tendable
    block = re.sub(r'<tendable>(?:true|false)</tendable>',
                   '<tendable>%s</tendable>' % ('true' if tendable else 'false'), block)

    # 2) 移除已有 modExtensions 块（幂等）
    block = re.sub(r'\n[ \t]*<modExtensions>.*?</modExtensions>', '', block, flags=re.S)

    # 3) 插入新 modExtensions（放在 </maxSeverity> 之后）
    ext = (
        '\n    <modExtensions>\n'
        '      <li Class="Hormones.StrainHediffExt">\n'
        '        <combatTriggered>%s</combatTriggered>\n'
        '        <onShoot>%s</onShoot>\n'
        '        <onMelee>%s</onMelee>\n'
        '        <onMeleeHitTaken>%s</onMeleeHitTaken>\n'
        '        <onAdrenalineBuildup>%s</onAdrenalineBuildup>\n'
        '        <severityTier>%s</severityTier>\n'
        '      </li>\n'
        '    </modExtensions>'
    ) % (tuple('true' if v else 'false' for v in (combat, shoot, melee, hit, buildup)) + (tier,))

    if '</maxSeverity>' in block:
        block = block.replace('</maxSeverity>', '</maxSeverity>' + ext, 1)
    else:
        print('WARN: no maxSeverity in', name)
    return block

new_text, n = re.subn(r'<HediffDef>.*?</HediffDef>', process_block, text, flags=re.S)
with io.open(XML, 'w', encoding='utf-8', newline='\n') as f:
    f.write(new_text)
print('processed HediffDef blocks:', n)

# 校验输出
for name, spec in SPEC.items():
    blk = re.search(r'<HediffDef>(?:(?!</HediffDef>).)*?<defName>%s</defName>.*?</HediffDef>' % name,
                    new_text, flags=re.S)
    if not blk:
        print('MISSING', name); continue
    b = blk.group(0)
    tend = re.search(r'<tendable>(\w+)</tendable>', b).group(1)
    tags = dict(re.findall(r'<(combatTriggered|onShoot|onMelee|onMeleeHitTaken|onAdrenalineBuildup|severityTier)>(\w+)</', b))
    print('%-24s tendable=%-5s tier=%-9s combat=%-5s shoot=%-5s melee=%-5s hitTaken=%-5s buildup=%s'
          % (name, tend, tags.get('severityTier'), tags.get('combatTriggered'),
             tags.get('onShoot'), tags.get('onMelee'), tags.get('onMeleeHitTaken'),
             tags.get('onAdrenalineBuildup')))
