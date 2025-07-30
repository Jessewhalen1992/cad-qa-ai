import argparse
import pandas as pd

# Map from block keywords to expected layers
keyword_to_layer = {
    "HYDRANT": "L-WATR",
    "VALVE": "L-MECH",
    "CATCHBAS": "L-DRAIN",
}

def is_ok(row: pd.Series) -> bool:
    """Return True if the entity already resides on the correct layer."""
    if row.get("ObjType") == "BlockReference":
        content = str(row.get("Content", "")).upper()
        for kw, layer in keyword_to_layer.items():
            if kw in content:
                return str(row.get("Layer", "")).upper() == layer
    return True

def main() -> None:
    parser = argparse.ArgumentParser(description="Label dataset rows using rules")
    parser.add_argument("--in", dest="inp", required=True, help="Input CSV path")
    parser.add_argument("--out", dest="out", required=True, help="Output CSV path")
    args = parser.parse_args()

    df = pd.read_csv(args.inp, on_bad_lines="skip")
    df["Label"] = df.apply(is_ok, axis=1)
    df.to_csv(args.out, index=False)
    print(f"\u2713 labeled \u2192 {args.out}")

if __name__ == "__main__":
    main()
