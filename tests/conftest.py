import os
import sys

# Make both the eval/ harness and the scripts/ pipeline importable by bare name
# (import metrics, import query_index, ...), regardless of the pytest CWD.
ROOT = os.path.dirname(os.path.dirname(__file__))
for sub in ("eval", "scripts"):
    path = os.path.join(ROOT, sub)
    if path not in sys.path:
        sys.path.insert(0, path)

# No model_setup fake needed any more (plan 3.7, M1): importing query_index /
# build_index no longer loads a model — the heavy work moved into
# model_setup.setup_models(), called lazily from the entry points. Tests that
# exercise retrieval still monkeypatch query()/the retrievers; the integration
# test installs its own MockEmbedding.
