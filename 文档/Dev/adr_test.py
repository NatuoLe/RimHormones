# Faithful re-implementation of the adrenaline physique logic for test-case generation.
# Mirrors Source/Logic/AdrenalineLogic.cs + PhysiqueLogic/PhysiqueDefine.cs

PENALTY_TH = 8      # PhysiqueAdrenalinePenaltyThreshold
EXEMPT_TH   = 13     # PhysiqueAdrenalineExemptionThreshold
PENALTY_FAC = 0.5    # PhysiqueAdrenalinePenaltyFactor

# Base values straight from Define.cs AdrenalineLow/Medium/High
BASE = {
    'Low':    dict(MoveSpeed=0.04, MeleeDamage=0.06, Dodge=0.036, MeleeHitReduction=-0.024, Metabolism=0.13),
    'Medium': dict(MoveSpeed=0.07, MeleeDamage=0.12, Dodge=0.072, MeleeHitReduction=-0.048, Metabolism=0.26),
    'High':   dict(MoveSpeed=0.10, MeleeDamage=0.20, Dodge=0.12,  MeleeHitReduction=-0.08,  Metabolism=0.40),
}

def phys_mod(P):
    return PENALTY_FAC if P < PENALTY_TH else 1.0

def exempt(P):
    return P >= EXEMPT_TH

def compute(level, P):
    b = BASE[level]
    m = phys_mod(P)
    ex = exempt(P)
    move   = b['MoveSpeed'] * m
    mdmg   = b['MeleeDamage'] * m
    dodge  = b['Dodge'] * m
    hit    = 0.0 if ex else b['MeleeHitReduction'] * m
    metab  = b['Metabolism'] * m
    return dict(MoveSpeed=move, MeleeDamage=mdmg, Dodge=dodge, MeleeHit=hit, Metabolism=metab,
                mult_move=1+move, mult_dmg=1+mdmg, mult_dodge=1+dodge,
                mult_hit=1+hit, mult_metab=1+metab, mod=m, ex=ex)

Ps = [0,3,5,7,8,10,12,13,15,18,20]
levels = ['Low','Medium','High']

for lvl in levels:
    print(f"\n=== Adrenaline {lvl} ===")
    print(f"{'P':>3} | {'mod':>4} | {'exempt':>5} | {'MoveSpeedMult':>13} | {'MeleeDmgMult':>12} | {'DodgeMult':>10} | {'MeleeHitMult':>12} | {'MetabMult':>10}")
    for P in Ps:
        c = compute(lvl, P)
        print(f"{P:>3} | {c['mod']:>4} | {str(c['ex']):>5} | {c['mult_move']:>13.4f} | {c['mult_dmg']:>12.4f} | {c['mult_dodge']:>10.4f} | {c['mult_hit']:>12.4f} | {c['mult_metab']:>10.4f}")

print("\n=== Raw adrenaline effect offsets (B-route, before +1) ===")
for lvl in levels:
    print(f"\n-- {lvl} --")
    print(f"{'P':>3} | {'Move':>7} | {'MeleeDmg':>8} | {'Dodge':>7} | {'MeleeHit':>8} | {'Metab':>6}")
    for P in Ps:
        c = compute(lvl, P)
        print(f"{P:>3} | {c['MoveSpeed']:>7.4f} | {c['MeleeDamage']:>8.4f} | {c['Dodge']:>7.4f} | {c['MeleeHit']:>8.4f} | {c['Metabolism']:>6.4f}")
