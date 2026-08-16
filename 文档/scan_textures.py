import re, os, glob
base = "D:/RimMods/Rim-Hormones/RimHormones"
texdir = os.path.join(base, "Textures")
missing = []
for p in glob.glob(base + "/Defs/**/*.xml", recursive=True):
    try:
        s = open(p, encoding='utf-8').read()
    except Exception:
        continue
    for m in re.finditer(r'<graphicPath>(.*?)</graphicPath>', s):
        gp = m.group(1).strip().replace(chr(92), '/')
        png = os.path.join(texdir, gp + ".png")
        if not os.path.exists(png):
            missing.append((os.path.basename(p), gp))
print("Missing explicit graphicPath textures:", missing if missing else "NONE")

dst = "D:/Steam/steamapps/common/RimWorld/Mods/Rim-Hormones/Textures"
for rel in ["Things/Item/MEE/MEE_GlucoseMash.png","Things/Item/MEE/MEE_ProteinExtract.png",
            "Things/Item/MEE/MEE_Salt.png","Things/Item/MEE/MEE_WaterBottle.png",
            "Things/Building/MEE/MEE_WaterWell.png"]:
    fp = os.path.join(dst, rel)
    print(("OK  " if os.path.exists(fp) else "MISS"), rel)
