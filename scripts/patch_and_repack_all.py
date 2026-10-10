import UnityPy
from PIL import Image
import os
import zlib
import struct

print("[Patch] Loading data.unity3d...")
bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

# --- 1. Patch sharedassets0.assets (Main Menu textures and UI) ---
sfile0 = env.file.files["sharedassets0.assets"]

# Logo textures
print("[Patch] Updating Main Menu logo textures (English Verdant Zero)...")
logo_img = Image.open("VerdantZero/Assets/VerdantZero/UI/MainMenu/Art/menu_logo.png")
obj18 = sfile0.objects[18]
data18 = obj18.read()
data18.set_image(logo_img, target_format=data18.m_TextureFormat)
data18.save()

glow_img = Image.open("VerdantZero/Assets/VerdantZero/UI/MainMenu/Art/menu_logo_glow.png")
obj20 = sfile0.objects[20]
data20 = obj20.read()
data20.set_image(glow_img, target_format=data20.m_TextureFormat)
data20.save()
print("  Successfully updated menu_logo and menu_logo_glow!")

# MainMenu UXML in Object 56
print("[Patch] Updating Main Menu UI strings in Object 56...")
obj56 = sfile0.objects[56]
raw56 = bytearray(obj56.get_raw_data())

def make_pair(t, r):
    if isinstance(t, str): t = t.encode("utf-8")
    if isinstance(r, str): r = r.encode("utf-8")
    if len(r) < len(t):
        r = r + b" " * (len(t) - len(r))
    elif len(r) > len(t):
        raise ValueError(f"Repl '{r}' ({len(r)}) > target '{t}' ({len(t)})")
    return (t, r)

replacements_56 = [
    make_pair(b"JUGAR", b"PLAY"),
    make_pair(b"OPCIONES", b"OPTIONS"),
    make_pair("CRÉDITOS", "CREDITS"),
    make_pair(b"SALIR", b"QUIT"),
    make_pair(b"VOLVER", b"BACK"),
    make_pair(b"ELIGE NIVEL", b"SELECT ZONE"),
    make_pair("1 · CENTRO DE VILLALIMPIA", "1 · VERDANT CITY DOWNTOWN"),
    make_pair(b"DIFICULTAD: NORMAL", b"DIFFICULTY: NORMAL"),
    make_pair(b"Joystick fijo", b"Fixed joypad"),
    make_pair("Música", "Music"),
    make_pair("Vibración", "Vibration"),
    make_pair(b"Equipo Smog Buster", b"Verdant Zero Team"),
    make_pair(b"Dron Buster-1", b"Verdant Drone"),
    make_pair(b"Modelo propio hecho en Blender", b"Original model made in Blender"),
    make_pair(b"Ciudad", b"City"),
    make_pair(b"Cielos", b"Skies"),
    make_pair(b"Kloofendal Overcast Pure Sky y Spruit Sunrise", b"Kloofendal Overcast Pure Sky & Spruit Sunrise"),
    make_pair("Diseño y desarrollo", "Design & development"),
    make_pair(b"Unity con Universal Render Pipeline", b"Unity Universal Render Pipeline")
]

for target, repl in replacements_56:
    count = 0
    idx = 0
    while True:
        idx = raw56.find(target, idx)
        if idx == -1:
            break
        raw56[idx:idx+len(repl)] = repl
        count += 1
        idx += len(repl)
    print(f"  Patched {count}x: {target.decode('utf-8', 'ignore')} -> {repl.decode('utf-8', 'ignore')}")

obj56.set_raw_data(bytes(raw56))

# --- 2. Patch sharedassets1.assets (In-game HUD) ---
print("[Patch] Updating Game HUD strings in sharedassets1.assets Object 468...")
sfile1 = env.file.files["sharedassets1.assets"]
obj468 = sfile1.objects[468]
raw468 = bytearray(obj468.get_raw_data())

replacements_468 = [
    (b"CONTINUAR", b"RESUME   "),
    (b"REINTENTAR", b"RETRY     "),
    (b"SEMILLA", b"SEED   "),
    (b"vuelve a la ciudad", b"Return to the city")
]

for target, repl in replacements_468:
    if len(repl) < len(target):
        repl = repl + b" " * (len(target) - len(repl))
    assert len(target) == len(repl), f"Length mismatch: {target} ({len(target)}) vs {repl} ({len(repl)})"
    count = 0
    idx = 0
    while True:
        idx = raw468.find(target, idx)
        if idx == -1:
            break
        raw468[idx:idx+len(repl)] = repl
        count += 1
        idx += len(repl)
    print(f"  Patched {count}x: {target.decode('utf-8', 'ignore')} -> {repl.decode('utf-8', 'ignore')}")

obj468.set_raw_data(bytes(raw468))

# --- 3. Save data.unity3d ---
print("[Patch] Saving updated data.unity3d...")
with open(bundle_path, "wb") as f:
    f.write(env.file.save(packer="lz4"))
print(f"  Saved data.unity3d ({os.path.getsize(bundle_path)} bytes)")

# --- 4. Patch global-metadata.dat if needed ---
metadata_path = os.path.join("unity-webgl", "Build", "extracted", "Il2CppData", "Metadata", "global-metadata.dat")
with open(metadata_path, "rb") as f:
    raw_meta = bytearray(f.read())

meta_replacements = [
    (b"JUGAR DE NUEVO", b"PLAY AGAIN    "),
    (b"Buster-1", b"Verdant1")
]
for target, repl in meta_replacements:
    assert len(target) == len(repl)
    count = 0
    idx = 0
    while True:
        idx = raw_meta.find(target, idx)
        if idx == -1:
            break
        raw_meta[idx:idx+len(repl)] = repl
        count += 1
        idx += len(repl)
    if count > 0:
        print(f"  Patched global-metadata.dat {count}x: {target.decode()} -> {repl.decode()}")

with open(metadata_path, "wb") as f:
    f.write(raw_meta)

# --- 5. Repack UnityWebData1.0 into WebGL.data.unpacked ---
print("[Patch] Repacking UnityWebData1.0 archive...")
files_list = [
    'data.unity3d',
    'RuntimeInitializeOnLoads.json',
    'ScriptingAssemblies.json',
    'sharedassets0.resource',
    'sharedassets1.resource',
    'boot.config',
    'Il2CppData/Metadata/global-metadata.dat',
    'Resources/unity default resources'
]

extracted_dir = os.path.join("unity-webgl", "Build", "extracted")
magic = b"UnityWebData1.0\0"

header_size = 16 + 4
for fname in files_list:
    header_size += 4 + 4 + 4 + len(fname.encode("utf-8"))

current_offset = header_size
entries = []
for fname in files_list:
    fpath = os.path.join(extracted_dir, fname)
    size = os.path.getsize(fpath)
    entries.append({
        "path": fname,
        "offset": current_offset,
        "size": size,
        "fpath": fpath
    })
    current_offset += size

header_buf = bytearray(header_size)
header_buf[0:16] = magic
struct.pack_into("<I", header_buf, 16, header_size)

pos = 20
for entry in entries:
    struct.pack_into("<I", header_buf, pos, entry["offset"]); pos += 4
    struct.pack_into("<I", header_buf, pos, entry["size"]); pos += 4
    path_bytes = entry["path"].encode("utf-8")
    struct.pack_into("<I", header_buf, pos, len(path_bytes)); pos += 4
    header_buf[pos:pos+len(path_bytes)] = path_bytes; pos += len(path_bytes)

unpacked_path = os.path.join("unity-webgl", "Build", "WebGL.data.unpacked")
with open(unpacked_path, "wb") as out_f:
    out_f.write(header_buf)
    for entry in entries:
        with open(entry["fpath"], "rb") as in_f:
            while chunk := in_f.read(1024 * 1024):
                out_f.write(chunk)

print(f"  Repacked WebGL.data.unpacked ({os.path.getsize(unpacked_path)} bytes)")

import gzip

# --- 6. Gzip to WebGL.data.unityweb ---
print("[Patch] Compressing to WebGL.data.unityweb (gzip level 9)...")
unityweb_path = os.path.join("unity-webgl", "Build", "WebGL.data.unityweb")
with open(unpacked_path, "rb") as f_in:
    raw_all = f_in.read()

gzipped = gzip.compress(raw_all, compresslevel=9)
with open(unityweb_path, "wb") as f_out:
    f_out.write(gzipped)

print(f"[Patch] DONE! WebGL.data.unityweb updated successfully ({len(gzipped)} bytes)!")
