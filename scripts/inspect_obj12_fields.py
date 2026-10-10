import UnityPy
import os
import struct

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

sfile = env.file.files["globalgamemanagers"]
obj12 = sfile.objects[12]
raw = bytearray(obj12.get_raw_data())

for i in range(0, len(raw), 4):
    val_i = struct.unpack("<I", raw[i:i+4])[0]
    val_f = struct.unpack("<f", raw[i:i+4])[0]
    print(f"Offset {i:03d} (0x{i:02x}): int={val_i:10d}, float={val_f:12.3f}")
