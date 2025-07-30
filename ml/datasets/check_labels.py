import pandas as pd

# Adjust path if your CSV lives elsewhere
df = pd.read_csv("labeled.csv")

suspects = [
    "Abandoned Railway",
    "Cultivation",
    "Grass",
    "0.83 km",
]

for key in suspects:
    print(f"\n=== '{key}' ===")
    mask = df["Content"].str.contains(key, case=False, na=False)
    print(df.loc[mask, "Layer"].value_counts())
