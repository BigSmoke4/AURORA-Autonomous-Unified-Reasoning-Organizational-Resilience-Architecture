import glob
import re

text = open("probe-add.txt", encoding="utf-8", errors="replace").read()
lines = [line for line in text.splitlines() if line.strip()]


def emit(message):
    message = message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print("::error title=probe::" + message)


emit("ADD OUTPUT tail: " + " | ".join(lines[-8:])[-3000:])
for path in glob.glob("src/Web/Aurora.Web/ProbeOut/*.cs"):
    if path.endswith(".Designer.cs"):
        continue
    source = open(path, encoding="utf-8").read()
    match = re.search(r"Up\(MigrationBuilder migrationBuilder\)(.*?)protected override void Down", source, re.S)
    body = re.sub(r"\s+", " ", match.group(1) if match else source)
    ops = [op.strip() for op in body.split("migrationBuilder.") if op.strip()]
    emit("FILE " + path + " ops=" + str(len(ops)))
    chunk = ""
    for op in ops:
        piece = "[" + op + "] "
        if len(chunk) + len(piece) > 3000:
            emit(chunk)
            chunk = ""
        chunk += piece
    if chunk:
        emit(chunk)
