"""Long-running search service: holds the warm retrieval engine.

Start it from the project root you want to search (cache paths are relative
to the current directory):

    .venv/bin/python scripts/service.py

On startup it loads the embedding model, connects to Qdrant, rebuilds BM25
in memory from the collection, and keeps the three retrievers warm. After
that, every POST /search call reuses the warm engine — no cold start.

Endpoints:
    GET  /health  -> {"status": "ok"}
    POST /search  -> body {"query": "..."}; returns the query() JSON.
"""
from flask import Flask, request, jsonify

from config import settings
from query_index import query, get_engine

app = Flask(__name__)


@app.route("/health")
def health():
    return jsonify({"status": "ok"})


@app.route("/search", methods=["POST"])
def search():
    data = request.get_json(silent=True) or {}
    q = data.get("query")
    if not q:
        return jsonify({"error": "missing 'query'"}), 400
    try:
        return jsonify(query(q))
    except Exception as e:
        return jsonify({"error": str(e)}), 500


def main():
    # Heavy work happens only here, not at import time: configure the
    # embedding model + LLM, then warm the retrievers once so the first real
    # request does not pay the build cost.
    import model_setup  # noqa: F401  (import side effect: configures Settings)
    get_engine()
    app.run(host="0.0.0.0", port=settings.service_port)


if __name__ == "__main__":
    main()
