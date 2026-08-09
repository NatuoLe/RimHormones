# -*- coding: utf-8 -*-
import os
W, H = 940, 1080
FONT = "'Microsoft YaHei','PingFang SC','SimHei',sans-serif"
OUT = os.path.join(os.path.dirname(__file__), "皮质醇机制图.svg")
C = {
    "bg":"#F4F7FB","ink":"#2C3E50","muted":"#7F8C8D",
    "growth":"#E74C3C","growth_bg":"#FDEDEC",
    "decay":"#2D9CDB","decay_bg":"#EAF6FD",
    "core":"#14507A",
    "neuro":"#8E44AD","neuro_bg":"#F5EEF8",
    "anhed":"#2F80ED","anhed_bg":"#EAF2FE",
    "insom":"#E67E22","insom_bg":"#FDF3E7",
    "line":"#D5DEE8","panel":"#FFFFFF",
}
parts=[]
def add(s): parts.append(s)
def rrect(x,y,w,h,r,fill,stroke=None,sw=1):
    s=f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{r}" ry="{r}" fill="{fill}"'
    if stroke: s+=f' stroke="{stroke}" stroke-width="{sw}"'
    return s+ '/>'
def text(x,y,t,size=13,color="#2C3E50",bold=False,anchor="start"):
    t=t.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;")
    wt=' font-weight="bold"' if bold else ''
    return f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{size}" fill="{color}" text-anchor="{anchor}"{wt}>{t}</text>'

add(f'<rect x="0" y="0" width="{W}" height="{H}" fill="{C["bg"]}"/>')
add('<defs><linearGradient id="hdr" x1="0" y1="0" x2="1" y2="0">'
    '<stop offset="0%" stop-color="#14507A"/><stop offset="100%" stop-color="#2D9CDB"/></linearGradient></defs>')
add(rrect(0,0,W,110,0,"url(#hdr)"))
add(text(40,52,"皮质醇 Cortisol 影响机制（简明版）",28,"#FFFFFF",True))
add(text(40,84,"哪些事件抬升 / 降低皮质醇  ·  高皮质醇触发哪些坏 Hediff",14,"#DCEEFB"))

# ---- two source columns ----
cy, ch = 150, 380
gap = 40
cw = (W - gap*3) / 2
# left: growth
lx = gap
add(rrect(lx,cy,cw,ch,12,C["panel"],C["line"],1))
add(rrect(lx,cy,cw,40,12,C["growth"]))
add(rrect(lx,cy+30,cw,10,0,C["growth"]))
add(text(lx+18,cy+27,"① 抬升皮质醇的事件  ↑",17,"#FFFFFF",True))
growth=["低心情","饥饿","疼痛","得病","被侮辱","不舒适","缺少娱乐","环境差","吃生食","湿透"]
yy=cy+70
for i in range(0,len(growth),2):
    left_item=growth[i]; right_item=growth[i+1] if i+1<len(growth) else ""
    add(text(lx+22,yy,"• "+left_item,14,C["ink"]))
    if right_item: add(text(lx+cw/2+10,yy,"• "+right_item,14,C["ink"]))
    yy+=30
add(text(lx+22,cy+ch-16,"（应激源，满足条件即叠加抬升）",11,C["muted"]))

# right: decay
rx = gap*2 + cw
add(rrect(rx,cy,cw,ch,12,C["panel"],C["line"],1))
add(rrect(rx,cy,cw,40,12,C["decay"]))
add(rrect(rx,cy+30,cw,10,0,C["decay"]))
add(text(rx+18,cy+27,"② 降低皮质醇的事件  ↓",17,"#FFFFFF",True))
decay=["娱乐活动","优质睡眠","高心情","美食","自然衰减"]
yy=cy+70
for i in range(0,len(decay),2):
    left_item=decay[i]; right_item=decay[i+1] if i+1<len(decay) else ""
    add(text(rx+22,yy,"• "+left_item,14,C["ink"]))
    if right_item: add(text(rx+cw/2+10,yy,"• "+right_item,14,C["ink"]))
    yy+=30
add(text(rx+22,cy+ch-16,"（恢复来源，叠加降低浓度）",11,C["muted"]))

# ---- core node ----
node_y = cy+ch+30
add(rrect(W/2-150,node_y,300,54,27,C["core"]))
add(text(W/2,node_y+34,"皮质醇浓度过高",18,"#FFFFFF",True,"middle"))
# arrows from columns to node
add(f'<path d="M {lx+cw/2} {cy+ch} C {lx+cw/2} {node_y-10}, {W/2-150} {node_y-10}, {W/2-150} {node_y}" stroke="{C["growth"]}" stroke-width="3" fill="none"/>')
add(f'<path d="M {rx+cw/2} {cy+ch} C {rx+cw/2} {node_y-10}, {W/2+150} {node_y-10}, {W/2+150} {node_y}" stroke="{C["decay"]}" stroke-width="3" fill="none"/>')

# ---- bad hediffs ----
by = node_y+70
add(text(40,by,"③ 高皮质醇触发的坏 Hediff（每 6000 tick 检测，加权随机抽其一）",15,C["ink"],True))
cards_y = by+14
cw3=(W-gap*4)/3
cards=[
 ("神经衰弱","CortisolNeurasthenia",C["neuro"],C["neuro_bg"],"休息效率下降\n心情下降"),
 ("快感缺失","CortisolAnhedonia",C["anhed"],C["anhed_bg"],"正面心情归零\n负面心情保留"),
 ("失眠","CortisolInsomnia",C["insom"],C["insom_bg"],"无法躺下入睡\n（blocks sleeping）"),
]
for i,(nm,en,col,bg,eff) in enumerate(cards):
    cx=gap+(cw3+gap)*i
    add(rrect(cx,cards_y,cw3,210,12,C["panel"],C["line"],1))
    add(rrect(cx,cards_y,cw3,42,12,col))
    add(rrect(cx,cards_y+32,cw3,10,0,col))
    add(text(cx+16,cards_y+28,nm,16,"#FFFFFF",True))
    add(text(cx+cw3-14,cards_y+27,en,9,"#FFFFFF",False,"end"))
    yy=cards_y+72
    for ln in eff.split("\n"):
        add(text(cx+18,yy,ln,14,C["ink"])); yy+=26
    add(rrect(cx+12,yy+4,cw3-24,40,8,bg))
    add(text(cx+18,yy+22,"加权互斥施加",12,col,True))

add(text(40,H-26,"设计源：皮质醇数值.xlsx ｜ 红=抬升应激  蓝=恢复  紫/蓝/橙=坏 Hediff",11,C["muted"]))

svg=(f'<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="{FONT}">\n'+"\n".join(parts)+"\n</svg>\n")
open(OUT,"w",encoding="utf-8").write(svg)
print("WROTE",OUT,len(svg))
