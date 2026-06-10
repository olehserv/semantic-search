#!/usr/bin/env bash
#
# End-to-end demo of the retrieval eval harness (see eval/README.md).
#
# It indexes the tiny sample .NET corpus into Qdrant and then runs the eval
# harness over eval/golden.jsonl, printing Recall@k / MRR / nDCG@k. This proves
# the whole pipeline works on a machine with no GPU and no pre-existing index.
#
# Requires: Docker (for Qdrant) and Python 3.11+. The first run downloads the
# embedding model (~400MB) and installs the ML stack into a local .venv.
#
# NOTE: it (re)builds the Qdrant collection named in scripts/qdrant.py
# (COLLECTION_NAME). If you already have a real index in that collection, run
# the demo against a throwaway Qdrant or change COLLECTION_NAME first.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CORPUS="$ROOT/eval/sample_corpus"
VENV="$ROOT/.venv"
K="${1:-5}"

echo "==> Setting up virtualenv + dependencies"
python3 -m venv "$VENV"
# shellcheck disable=SC1091
source "$VENV/bin/activate"
pip install --quiet --upgrade pip
pip install --quiet -r "$ROOT/eval/requirements-eval.txt"

echo "==> Building the index over the sample corpus (Qdrant auto-starts via Docker)"
# build_index.py uses PROJECT_PATH="./" and writes its caches relative to CWD,
# so we run it from inside the corpus. --force replaces an existing collection
# without prompting. scripts/ must be importable by bare module name.
cd "$CORPUS"
PYTHONPATH="$ROOT/scripts" python "$ROOT/scripts/build_index.py" --force

echo "==> Running the eval harness"
# Stay in the corpus dir so query_index reads the same BM25/embedding caches the
# build just wrote. Both eval/ (metrics) and scripts/ (query_index) on the path.
PYTHONPATH="$ROOT/scripts:$ROOT/eval" python "$ROOT/eval/run_eval.py" \
    --golden "$ROOT/eval/golden.jsonl" --k "$K"
