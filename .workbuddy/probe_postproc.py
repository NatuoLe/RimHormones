import io, re

excel = r"F:/ObsdianProj/边缘世界mod开发本地/.obsidian/plugins/excel/main.js"
s = io.open(excel, encoding="utf-8", errors="replace").read()

# Find the markdownPostProcessor function definition
m = re.search(r'function markdownPostProcessor|markdownPostProcessor\s*=\s*\(', s)
if m:
    i = m.start()
    print("=== markdownPostProcessor (head) ===")
    print(s[i:i+700])
else:
    # search where it's assigned/used
    idx = s.find("registerMarkdownPostProcessor(markdownPostProcessor)")
    print("registerMarkdownPostProcessor @", idx)
    # find the definition before it
    pre = s[:idx]
    j = pre.rfind("markdownPostProcessor")
    print("last mention before:", pre[j-60:j+120])

# What does it look for in the markdown? (# Excel)
m2 = re.search(r'# Excel', s)
print("\n'# Excel' literal present:", m2 is not None)

# Does it call getExcelData / create a Sheet?
for kw in ["getExcelData", "new Sheet", "x_spreadsheet", "bottombar", "spreadsheet"]:
    print(f"  references {kw}:", kw in s)
