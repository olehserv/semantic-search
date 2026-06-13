"""Security guard: no untrusted deserialization in the pipeline (plan 3.8, M2).

pickle.load (and torch.load / joblib / dill) run arbitrary code on load, so a
tampered cache file would be remote code execution. The project deliberately
uses SQLite + plain float32 bytes for the embedding cache and rebuilds BM25 from
Qdrant — there is no pickle anywhere. This test keeps it that way: it fails the
moment any such deserialization call reappears in the shipped code.

Pure stdlib, no imports of the pipeline — runs on every CI, fast.
"""
import glob
import os

ROOT = os.path.dirname(os.path.dirname(__file__))

# Call-like substrings, so prose mentioning "pickle" in a comment never trips it.
FORBIDDEN = (
    "pickle.load(",
    "pickle.loads(",
    "torch.load(",
    "joblib.load(",
    "dill.load(",
    "cloudpickle",
)


def _source_files():
    files = []
    for sub in ("scripts", "eval"):
        files += glob.glob(os.path.join(ROOT, sub, "*.py"))
    return files


def test_no_unsafe_deserialization():
    offenders = []
    for path in _source_files():
        with open(path, encoding="utf-8") as f:
            text = f.read()
        for pattern in FORBIDDEN:
            if pattern in text:
                offenders.append(f"{os.path.relpath(path, ROOT)}: {pattern}")
    assert not offenders, (
        "unsafe deserialization found (RCE risk — plan 3.8 / M2):\n"
        + "\n".join(offenders)
    )
