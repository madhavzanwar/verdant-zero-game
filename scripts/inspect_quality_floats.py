import UnityPy
import os

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

sfile = env.file.files["globalgamemanagers"]
obj12 = sfile.objects[12]
raw = bytearray(obj12.get_raw_data())

print(f"obj12 raw size: {len(raw)}")
# Let's search for float 40.0 (0x00 0x00 0x20 0x42)
import struct
idx = 0
matches = []
while True:
    idx = raw.find(struct.pack("<f", 40.0), idx)
    if idx == -1:
        break
    matches.append(idx)
    idx += 4
print(f"Float 40.0 found at: {matches}")
