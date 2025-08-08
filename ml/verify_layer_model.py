import os, joblib, sys, pathlib

p = pathlib.Path("ml/artifacts/layer_clf.pkl")
size = os.path.getsize(p)
try:
    fitted = hasattr(joblib.load(p).named_steps["tfidf"], "vocabulary_")
except Exception:
    fitted = False
if size < 100_000 or not fitted:
    print(f"Model looks unfitted (size={size}, fitted={fitted})")
    sys.exit(1)
print(f"Model OK  ✓  (size = {size} bytes)")
