import io

excel = r"F:/ObsdianProj/边缘世界mod开发本地/.obsidian/plugins/excel/main.js"
s = io.open(excel, encoding="utf-8", errors="replace").read()

i = s.find("switchToExcelAfterLoad() {")
# print the whole method (brace-balanced-ish via scanning)
seg = s[i:i+1400]
print(seg)
