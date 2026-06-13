import torch

from llama_index.embeddings.huggingface import HuggingFaceEmbedding
from llama_index.llms.ollama import Ollama
from llama_index.core import Settings

from config import settings

# Local LLM used by ask.py. Swap it via the LLM_MODEL env var (plan 3.1).
LLM_MODEL = settings.llm_model

# Use the GPU when one is available, otherwise fall back to CPU. (Previously
# hardcoded to "cuda", which crashed on CPU-only machines — e.g. the eval demo.)
device = "cuda" if torch.cuda.is_available() else "cpu"

Settings.embed_model = HuggingFaceEmbedding(
    model_name=settings.embed_model,
    device=device
)

# ask.py calls Settings.llm.complete(); without this it silently fell back to
# LlamaIndex's default (OpenAI), needing OPENAI_API_KEY. Wire up a local Ollama
# LLM instead. Requires `llama-index-llms-ollama` and a running Ollama server.
Settings.llm = Ollama(model=LLM_MODEL, request_timeout=120.0)