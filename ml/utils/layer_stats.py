r"""
layer_stats.py  –  quick layer frequency + example browser
----------------------------------------------------------
Usage (from repo root):
    python ml\utils\layer_stats.py
"""
import pandas as pd, textwrap, pathlib

ds = pathlib.Path(r"ml/datasets/labeled.csv")
df = pd.read_csv(ds)

# ── top‑N layers ───────────────────────────────────────────────
top = df["Layer"].value_counts().head(25)
print(textwrap.dedent(f"""
Top‑25 layer counts  (total rows: {len(df):,})
---------------------------------------------
{top.to_string()}
"""))

# ── peek at a phrase that looks mis‑labelled ───────────────────
phrase = "L.S."
sample = df[df["Content"].str.contains(phrase, na=False, case=False)]
print(f"\nRows containing “{phrase}” ({len(sample)}):")
print(sample[["Content", "Layer"]].head(20).to_string(index=False))
