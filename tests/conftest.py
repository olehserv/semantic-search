import os
import sys
import types

# Make both the eval/ harness and the scripts/ pipeline importable by bare name
# (import metrics, import query_index, ...), regardless of the pytest CWD.
ROOT = os.path.dirname(os.path.dirname(__file__))
for sub in ("eval", "scripts"):
    path = os.path.join(ROOT, sub)
    if path not in sys.path:
        sys.path.insert(0, path)

# Stub model_setup so importing run_eval/query_index in a test never downloads or
# loads the HuggingFace embedding model (which also needs torch). Tests that
# exercise retrieval monkeypatch query() directly; the pure metric tests never
# touch this. Mirrors the pattern in docs/superpowers/plans/.
if "model_setup" not in sys.modules:
    sys.modules["model_setup"] = types.ModuleType("model_setup")
