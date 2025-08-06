import argparse
import csv
import re
from collections import Counter

def tokenize(text: str):
    """Split text into non‑numeric tokens, ignoring punctuation and numbers."""
    for tok in re.split(r'[\s,;/()\[\]{}]+', text):
        tok = tok.strip()
        if not tok:
            continue
        if re.fullmatch(r'\d+(?:\.\d+)?', tok):
            continue  # skip pure numbers
        yield tok.upper()

def main():
    parser = argparse.ArgumentParser(description="Generate vocabulary of valid tokens from training dataset.")
    parser.add_argument("--in", dest="inp", required=True, help="Path to merged dataset CSV (e.g. master.csv)")
    parser.add_argument("--out", dest="out", default="spell_allowed_tokens.txt",
                        help="Path to output tokens file")
    parser.add_argument("--min_count", type=int, default=2,
                        help="Minimum occurrences for a token to be considered valid")
    args = parser.parse_args()

    counts = Counter()
    with open(args.inp, newline='', encoding='utf-8', errors='ignore') as f:
        reader = csv.reader(f)
        header = next(reader, None)
        # try to find a column called 'Content' or 'Text'; fall back to third column
        content_index = 2
        if header:
            for idx, col in enumerate(header):
                if col.lower() in ('content', 'text'):
                    content_index = idx
                    break

        for row in reader:
            if len(row) <= content_index:
                continue
            text = row[content_index]
            for tok in tokenize(str(text)):
                counts[tok] += 1

    tokens = [tok for tok, cnt in counts.items() if cnt >= args.min_count]
    tokens.sort()

    with open(args.out, 'w', encoding='utf-8') as f:
        for tok in tokens:
            f.write(tok + '\n')
    print(f"✔ Vocabulary written to {args.out} (n={len(tokens)})")

if __name__ == "__main__":
    main()
