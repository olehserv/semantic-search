"""Set up the embedding model and the LLM, once, for the whole pipeline.

WHAT: `setup_models()` configures LlamaIndex's global `Settings` object —
`Settings.embed_model` (turns text into vectors) and `Settings.llm` (writes
answers in ask.py).

WHY a function, not import-time side effects (plan 3.7, finding M1): building
the embedding model downloads ~400 MB and pulls in torch. Doing that on `import
model_setup` made every importer (and every test) pay the cost, which is why the
tests used to fake this module. Now the heavy work happens only when a caller
asks for it — build_index/query_index/service/ask call `setup_models()` from
their own lazy entry points, mirroring get_engine()/_get_cache() in query_index.

The model choice still lives in one place (here), driven by config (plan 3.1).
"""


def setup_models():
    """Configure Settings.embed_model + Settings.llm. Safe to call repeatedly.

    Idempotent and a no-op for whichever is already set: a test that installs a
    MockEmbedding keeps it (we never override or re-download), and `import torch`
    only happens when the embedding model actually has to be built.
    """
    from llama_index.core import Settings

    # The backing fields are None until configured; the public properties would
    # lazily resolve a default (OpenAI) instead, so check the fields directly.
    need_embed = Settings._embed_model is None
    need_llm = getattr(Settings, "_llm", None) is None
    if not need_embed and not need_llm:
        return

    from config import settings

    if need_embed:
        import torch
        from llama_index.embeddings.huggingface import HuggingFaceEmbedding

        # Use the GPU when one is available, otherwise fall back to CPU.
        # (Previously hardcoded to "cuda", which crashed on CPU-only machines.)
        device = "cuda" if torch.cuda.is_available() else "cpu"
        Settings.embed_model = HuggingFaceEmbedding(
            model_name=settings.embed_model,
            device=device,
        )

    if need_llm:
        from llama_index.llms.ollama import Ollama

        # ask.py calls Settings.llm.complete(); without this LlamaIndex falls
        # back to its OpenAI default (needs OPENAI_API_KEY). Wire up a local
        # Ollama LLM. Requires `llama-index-llms-ollama` + a running Ollama
        # server (the client is created here but only connects on first use).
        Settings.llm = Ollama(model=settings.llm_model, request_timeout=120.0)
