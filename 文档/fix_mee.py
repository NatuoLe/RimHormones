#!/usr/bin/env python3
import re, os, glob, shutil

SRC = "D:/RimMods/Rim-Hormones/RimHormones"
DST = "D:/Steam/steamapps/common/RimWorld/Mods/Rim-Hormones"

# ---------- 1) ThoughtDef: add <description> inside each <li> stage ----------
thought_files = [
    "Thought_MEE_AteSugar.xml",
    "Thought_MEE_NeedWater.xml",
    "Thought_MEE_Thirsty.xml",
    "Thought_MEE_WaterPoisoningMood.xml",
    "Thought_MEE_WeaknessMood.xml",
]
tdir = os.path.join(SRC, "Defs/ThoughtDefs")
for f in thought_files:
    p = os.path.join(tdir, f)
    s = open(p, encoding="utf-8").read()
    m = re.search(r"<description>(.*?)</description>", s, re.S)
    desc = m.group(1).strip() if m else ""
    def add_desc(li):
        if "<description>" in li.group(0):
            return li.group(0)
        # insert after the <label>...</label> inside the <li>
        return re.sub(r"(<label>.*?</label>)",
                      r"\1\n      <description>%s</description>" % desc,
                      li.group(0), count=1, flags=re.S)
    s2 = re.sub(r"<li>.*?</li>", add_desc, s, flags=re.S)
    open(p, "w", encoding="utf-8").write(s2)
    print("thought fixed:", f)

# ---------- 2) Item ThingDefs: Graphic_StackCount -> Graphic_Single ----------
item_files = [
    "Thing_MEE_GlucoseMash.xml",
    "Thing_MEE_ProteinExtract.xml",
    "Thing_MEE_Salt.xml",
    "Thing_MEE_WaterBottle.xml",
]
id_dir = os.path.join(SRC, "Defs/ThingDefs")
for f in item_files:
    p = os.path.join(id_dir, f)
    s = open(p, encoding="utf-8").read()
    if "<graphicClass>Graphic_StackCount</graphicClass>" in s:
        s = s.replace("<graphicClass>Graphic_StackCount</graphicClass>",
                      "<graphicClass>Graphic_Single</graphicClass>")
        open(p, "w", encoding="utf-8").write(s)
        print("graphic fixed:", f)
    else:
        print("graphic NOT StackCount (skip):", f)

# ---------- 3) sync to deployed ----------
for rel in [
    "Defs/ThoughtDefs/Thought_MEE_AteSugar.xml",
    "Defs/ThoughtDefs/Thought_MEE_NeedWater.xml",
    "Defs/ThoughtDefs/Thought_MEE_Thirsty.xml",
    "Defs/ThoughtDefs/Thought_MEE_WaterPoisoningMood.xml",
    "Defs/ThoughtDefs/Thought_MEE_WeaknessMood.xml",
    "Defs/ThingDefs/Thing_MEE_GlucoseMash.xml",
    "Defs/ThingDefs/Thing_MEE_ProteinExtract.xml",
    "Defs/ThingDefs/Thing_MEE_Salt.xml",
    "Defs/ThingDefs/Thing_MEE_WaterBottle.xml",
]:
    sp = os.path.join(SRC, rel)
    dp = os.path.join(DST, rel)
    shutil.copy2(sp, dp)
print("synced to deployed")
