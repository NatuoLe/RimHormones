import io, re

excel = r"F:/ObsdianProj/边缘世界mod开发本地/.obsidian/plugins/excel/main.js"
adv   = r"F:/ObsdianProj/边缘世界mod开发本地/.obsidian/plugins/table-editor-obsidian/main.js"
s_ex = io.open(excel, encoding="utf-8", errors="replace").read()
s_adv = io.open(adv, encoding="utf-8", errors="replace").read()

print("=== ExcelView display text ===")
for pat in [r'getDisplayText\s*\(\)\s*\{[^}]*\}', r'displayText\s*[=:][^;,{]*']:
    m = re.search(pat, s_ex)
    if m:
        print("  ", m.group()[:140])

print("\n=== Excel markdown post processor ===")
for m in re.finditer(r'registerMarkdownPostProcessor', s_ex):
    i = m.start()
    print("  @", i, s_ex[i:i+120])
    break

print("\n=== table-editor-obsidian ===")
m = re.search(r'registerView\(\s*([A-Za-z_$][\w$]*)\s*,', s_adv)
print("  registerView type var:", m.group(1) if m else "?")
if m:
    var = m.group(1)
    dm = re.search(re.escape(var) + r'\s*=\s*["\']([^"\']+)["\']', s_adv)
    print("  view type id:", dm.group(1) if dm else "?")
for kw in ["registerMarkdownPostProcessor", "MarkdownView", "editorExtension", "EditorView", "livePreview"]:
    if kw in s_adv:
        i = s_adv.find(kw)
        print(f"  has {kw} @", i)
