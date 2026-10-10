import UnityPy
import os
import struct
import gzip

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

# 1. Optimize QualitySettings in globalgamemanagers
sfile = env.file.files["globalgamemanagers"]
obj12 = sfile.objects[12]
raw12 = bytearray(obj12.get_raw_data())

# Offset 36 (0x24): shadows: 2 -> 1 (Hard Shadows, huge WebGL shader speedup)
print(f"Current shadows: {struct.unpack('<I', raw12[36:40])[0]}")
struct.pack_into("<I", raw12, 36, 1)

# Offset 40 (0x28): shadowDistance: 40.0 -> 25.0 (reduces shadow render pass geometry by 40%)
print(f"Current shadowDistance: {struct.unpack('<f', raw12[40:44])[0]}")
struct.pack_into("<f", raw12, 40, 25.0)

# Offset 144 (0x90): particleRaycastBudget: 256 -> 64
print(f"Current particleRaycastBudget: {struct.unpack('<I', raw12[144:148])[0]}")
struct.pack_into("<I", raw12, 144, 64)

obj12.set_raw_data(bytes(raw12))
print("Successfully optimized QualitySettings in globalgamemanagers!")

# Save data.unity3d
print("[Patch] Saving data.unity3d...")
with open(bundle_path, "wb") as f:
    f.write(env.file.save(packer="lz4"))
print(f"Saved data.unity3d ({os.path.getsize(bundle_path)} bytes)")

# 2. Repack UnityWebData1.0
print("[Patch] Repacking UnityWebData1.0...")
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

print(f"Repacked WebGL.data.unpacked ({os.path.getsize(unpacked_path)} bytes)")

# 3. Gzip to WebGL.data.unityweb
print("[Patch] Compressing to WebGL.data.unityweb...")
unityweb_path = os.path.join("unity-webgl", "Build", "WebGL.data.unityweb")
with open(unpacked_path, "rb") as f_in:
    raw_all = f_in.read()

gzipped = gzip.compress(raw_all, compresslevel=9)
with open(unityweb_path, "wb") as f_out:
    f_out.write(gzipped)

print(f"[Patch] DONE! WebGL.data.unityweb updated successfully ({len(gzipped)} bytes)!")
