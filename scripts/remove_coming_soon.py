import UnityPy
import os
import struct
import gzip

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

sfile = env.file.files["sharedassets0.assets"]
obj56 = sfile.objects[56]
raw = bytearray(obj56.get_raw_data())

# Verify target location
# 5636: 06 00 00 00
# 5640: 14 04 00 00 00 00 00 00
# 5648: 15 04 00 00 00 00 00 00
# 5656: 16 04 00 00 00 00 00 00
# 5664: 17 04 00 00 00 00 00 00 (child 1047: btn-level-2)
# 5672: 18 04 00 00 00 00 00 00 (child 1048: btn-level-3)
# 5680: 19 04 00 00 00 00 00 00 (child 1049: btn-levels-back)

count_pos = 5636
current_count = struct.unpack("<I", raw[count_pos:count_pos+4])[0]
print(f"Current child count at {count_pos}: {current_count}")

c1 = struct.unpack("<Q", raw[5640:5648])[0]
c2 = struct.unpack("<Q", raw[5648:5656])[0]
c3 = struct.unpack("<Q", raw[5656:5664])[0]
c4 = struct.unpack("<Q", raw[5664:5672])[0]
c5 = struct.unpack("<Q", raw[5672:5680])[0]
c6 = struct.unpack("<Q", raw[5680:5688])[0]

print(f"Children: {[c1, c2, c3, c4, c5, c6]}")
assert current_count == 6
assert c4 == 1047 # btn-level-2
assert c5 == 1048 # btn-level-3
assert c6 == 1049 # btn-levels-back

# Update count to 4
struct.pack_into("<I", raw, count_pos, 4)

# Remove c4 and c5 (from index 5664 to 5680, 16 bytes)
new_raw = raw[:5664] + raw[5680:]
print(f"Original size: {len(raw)}, New size: {len(new_raw)}")

obj56.set_raw_data(bytes(new_raw))

# Save data.unity3d
print("[Patch] Saving data.unity3d...")
with open(bundle_path, "wb") as f:
    f.write(env.file.save(packer="lz4"))
print(f"Saved data.unity3d ({os.path.getsize(bundle_path)} bytes)")

# Repack UnityWebData1.0
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

# Gzip to WebGL.data.unityweb
print("[Patch] Compressing to WebGL.data.unityweb...")
unityweb_path = os.path.join("unity-webgl", "Build", "WebGL.data.unityweb")
with open(unpacked_path, "rb") as f_in:
    raw_all = f_in.read()

gzipped = gzip.compress(raw_all, compresslevel=9)
with open(unityweb_path, "wb") as f_out:
    f_out.write(gzipped)

print(f"[Patch] DONE! WebGL.data.unityweb updated successfully ({len(gzipped)} bytes)!")
