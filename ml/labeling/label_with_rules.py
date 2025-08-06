"""
label_with_rules_updated.py – enhanced labeling rules for CAD QA
----------------------------------------------------------------

This script improves on the original `label_with_rules.py` by not only
adding a `Label` column but also correcting the `Layer` for certain
cases before training.  The goal is to ensure that the training data
used for the machine‑learning classifier is as clean and consistent
as possible.

Key enhancements:

* **BlockReference keyword rules** – identical to the original implementation.
  If a block name contains a keyword like “HYDRANT” or “VALVE”, the
  layer is forcibly set to the expected value (e.g. `L‑WATR` for
  hydrants).

* **Section marker rules** – any DBText or MText entity whose text
  begins with `"L.S."` followed by a number is assumed to be a section
  marker and is assigned to layer `L‑SECTION‑LSD`.  This prevents
  ambiguous cases where “L.S.” might appear on other layers, which
  confused the classifier in earlier training runs.

The resulting CSV can be fed directly into the training notebook.
"""

import argparse
import pandas as pd
import re

# Map from block keywords to expected layers
keyword_to_layer = {
    "HYDRANT": "L-WATR",
    "VALVE": "L-MECH",
    "CATCHBAS": "L-DRAIN",
}


def fix_layer(row: pd.Series) -> pd.Series:
    """Apply layer-correction rules to a single row.

    * For BlockReference entities, enforce known layer mappings based on
      keywords (HYDRANT, VALVE, etc.).
    * For text entities (DBText or MText) that start with "L.S." and
      contain a numeric part, assign them to L-SECTION-LSD.
    """
    obj_type = row.get("ObjType", "")
    content = str(row.get("Content", "")).strip()
    layer = str(row.get("Layer", ""))

    # Normalize values for comparisons
    content_upper = content.upper()
    layer_upper = layer.upper()

    # Rule 1: Block keyword mapping
    if obj_type == "BlockReference":
        for kw, expected_layer in keyword_to_layer.items():
            if kw in content_upper:
                if layer_upper != expected_layer.upper():
                    row["Layer"] = expected_layer
                break

    # Rule 2: Section marker (e.g. "L.S. 2")
    if obj_type in {"DBText", "MText"}:
        # Match strings like "L.S. 2", "L.S.16", allowing spaces
        if re.match(r"^L\.S\.\s*\d", content_upper):
            if layer_upper != "L-SECTION-LSD":
                row["Layer"] = "L-SECTION-LSD"

    return row


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Label and correct dataset rows using deterministic rules"
    )
    parser.add_argument("--in", dest="inp", required=True, help="Input CSV path")
    parser.add_argument(
        "--out", dest="out", required=True, help="Output CSV path"
    )
    args = parser.parse_args()

    # Load the input CSV; ignore malformed lines gracefully
    df = pd.read_csv(args.inp, on_bad_lines="skip")

    # Apply layer-correction rules row by row
    df = df.apply(fix_layer, axis=1)

    # Indicate whether each row was already correct (True) or required changes (False)
    def already_ok(row: pd.Series) -> bool:
        original_layer = str(row.get("Layer", "")).upper()
        # Recompute layer after applying rules to see if it would have changed
        temp_row = row.copy()
        fixed_row = fix_layer(temp_row)
        return str(fixed_row.get("Layer", "")).upper() == original_layer

    df["Label"] = df.apply(already_ok, axis=1)

    # Write the corrected dataset out
    df.to_csv(args.out, index=False)
    print(f"✔ labeled → {args.out}")


if __name__ == "__main__":
    main()