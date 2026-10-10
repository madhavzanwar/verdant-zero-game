import UnityPy
import os

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

sfile = env.file.files["globalgamemanagers.assets"]
print(f"globalgamemanagers.assets has {len(sfile.objects)} objects")

for pid, obj in sfile.objects.items():
    if "Render" in obj.type.name or "Pipeline" in obj.type.name or obj.path_id == 3459:
        print(f"Object {pid}: type={obj.type.name}")

# Also check resources.assets
sfile_res = env.file.files.get("resources.assets")
if sfile_res:
    for pid, obj in sfile_res.objects.items():
        if "Pipeline" in obj.type.name or "Render" in obj.type.name or obj.path_id == 3459:
            print(f"[resources] Object {pid}: type={obj.type.name}")
