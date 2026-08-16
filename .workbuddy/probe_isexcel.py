import io, re

excel = r"F:/ObsdianProj/边缘世界mod开发本地/.obsidian/plugins/excel/main.js"
s = io.open(excel, encoding="utf-8", errors="replace").read()

# isExcelFile
m = re.search(r'isExcelFile\s*\([^)]*\)\s*\{', s)
if m:
    i = m.start()
    print("=== isExcelFile ===")
    print(s[i:i+400])
else:
    print("isExcelFile not found by direct regex; searching 'isExcelFile'")
    for mm in re.finditer(r'isExcelFile', s):
        print("  @", mm.start(), s[mm.start():mm.start()+60])

# onload start
m2 = s.find("async onload()")
if m2 < 0:
    m2 = s.find("onload()")
print("\n=== onload (first 700 chars) ===")
print(s[m2:m2+700])
