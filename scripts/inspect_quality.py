import UnityPy
import os

bundle_path = os.path.join("unity-webgl", "Build", "extracted", "data.unity3d")
env = UnityPy.load(bundle_path)

for fname, sfile in env.file.files.items():
    for pid, obj in sfile.objects.items():
        if obj.type.name in ["QualitySettings", "GraphicsSettings", "TimeManager"]:
            try:
                data = obj.read()
                print(f"[{fname}] {obj.type.name} (pid={pid}):")
                for attr in dir(data):
                    if not attr.startswith("_") and not callable(getattr(data, attr)):
                        val = getattr(data, attr)
                        if isinstance(val, (int, float, str, bool, list)):
                            print(f"  {attr}: {val}")
            except Exception as e:
                print(f"[{fname}] {obj.type.name} error: {e}")
