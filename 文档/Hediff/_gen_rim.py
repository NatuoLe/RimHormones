# -*- coding: utf-8 -*-
import os
W, H = 980, 740
FONT = "'Microsoft YaHei','PingFang SC','SimHei','Segoe UI',sans-serif"
OUT = os.path.join(os.path.dirname(__file__), "边缘体魄功能图.svg")
C = {
    "bg1":"#FBFBF9","bg2":"#ECECE6",
    "slate":"#2B3A42","slate_sub":"#9FB3BD",
    "ink":"#2C3E50","bul":"#34495E",
    "phys":"#4C6EF5","phys_bg":"#EEF2FF",
    "cort":"#E8893B","cort_bg":"#FDF3E9",
    "adr":"#E0524B","adr_bg":"#FCEDED",
    "line":"#C7CDD4",
}
parts=[]
def add(s): parts.append(s)
def rrect(x,y,w,h,r,fill,stroke=None,sw=1):
    s=f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{r}" ry="{r}" fill="{fill}"'
    if stroke: s+=f' stroke="{stroke}" stroke-width="{sw}"'
    return s+'/>'
def text(x,y,t,size=13,color="#2C3E50",bold=False,anchor="start"):
    t=t.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;")
    wt=' font-weight="bold"' if bold else ''
    return f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{size}" fill="{color}" text-anchor="{anchor}"{wt}>{t}</text>'

# background
add(f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#bg)"/>')
add('<defs><radialGradient id="bg" cx="50%" cy="46%" r="75%">'
    f'<stop offset="0%" stop-color="{C["bg1"]}"/><stop offset="100%" stop-color="{C["bg2"]}"/>'
    '</radialGradient></defs>')

# connector lines (center -> cards)
add(f'<line x1="490" y1="328" x2="490" y2="292" stroke="{C["phys"]}" stroke-width="2.5" stroke-opacity="0.55"/>')
add(f'<line x1="490" y1="392" x2="332" y2="468" stroke="{C["cort"]}" stroke-width="2.5" stroke-opacity="0.55"/>')
add(f'<line x1="490" y1="392" x2="648" y2="468" stroke="{C["adr"]}" stroke-width="2.5" stroke-opacity="0.55"/>')

# center node
add(rrect(388,326,204,72,16,C["slate"]))
add(text(490,360,"边缘体魄",23,"#FFFFFF",True,"middle"))
add(text(490,383,"RimPhysique",13,C["slate_sub"],False,"middle"))

# ---- Physique card (top) ----
cx,cy,cw,ch=345,40,290,252
add(rrect(cx,cy,cw,ch,14,C["phys_bg"],C["phys"],1.6))
add(text(cx+16,cy+30,"体魄系统 Physique",16,C["phys"],True))
bullets=[ "五阶段：虚弱·一般·健康·强壮·卓越",
          "劳作 & 锻炼成长",
          "用进废退（日常衰减）",
          "特质 / 背景偏移" ]
yy=cy+62
for b in bullets:
    add(f'<circle cx="{cx+22}" cy="{yy-4}" r="3" fill="{C["phys"]}"/>')
    add(text(cx+34,yy,b,13,C["bul"]))
    yy+=34

# ---- Cortisol card (bottom-left) ----
cx,cy,cw,ch=30,415,300,278
add(rrect(cx,cy,cw,ch,14,C["cort_bg"],C["cort"],1.6))
add(text(cx+16,cy+30,"皮质醇 Cortisol",16,C["cort"],True))
items=[("慢性压力积累",None),
       ("承压 / 高压 → 心情↓",None),
       ("触发坏 Hediff",["神经衰弱","快感缺失","失眠"])]
yy=cy+62
for label,subs in items:
    add(f'<circle cx="{cx+22}" cy="{yy-4}" r="3" fill="{C["cort"]}"/>')
    add(text(cx+34,yy,label,13,C["bul"]))
    yy+=32
    if subs:
        for s in subs:
            add(text(cx+52,yy,"· "+s,12.5,C["bul"]))
            yy+=24

# ---- Adrenaline card (bottom-right) ----
cx,cy,cw,ch=650,415,300,278
add(rrect(cx,cy,cw,ch,14,C["adr_bg"],C["adr"],1.6))
add(text(cx+16,cy+30,"肾上腺素 Adrenaline",16,C["adr"],True))
bullets=[ "战斗应激反应",
          "战斗上升 · 脱战衰减",
          "增益：移速/意识/伤害/闪避",
          "风险：透支损伤" ]
yy=cy+62
for b in bullets:
    add(f'<circle cx="{cx+22}" cy="{yy-4}" r="3" fill="{C["adr"]}"/>')
    add(text(cx+34,yy,b,13,C["bul"]))
    yy+=34

# footer
add(text(490,H-22,"边缘体魄 RimPhysique · 功能关联图（无数值，基于设计数值表）",12,C["line"],False,"middle"))

svg=(f'<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="{FONT}">\n'
     + "\n".join(parts) + "\n</svg>\n")
open(OUT,"w",encoding="utf-8").write(svg)
print("WROTE",OUT,len(svg))
