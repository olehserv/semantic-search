import torch

from llama_index.embeddings.huggingface import HuggingFaceEmbedding
from llama_index.llms.ollama import Ollama
from llama_index.core import Settings

# Local LLM used by ask.py. Change this to swap models (e.g. a code-tuned one).
LLM_MODEL = "llama3"

# Use the GPU when one is available, otherwise fall back to CPU. (Previously
# hardcoded to "cuda", which crashed on CPU-only machines — e.g. the eval demo.)
device = "cuda" if torch.cuda.is_available() else "cpu"

Settings.embed_model = HuggingFaceEmbedding(
    model_name="BAAI/bge-base-en-v1.5",
    device=device
)

# ask.py calls Settings.llm.complete(); without this it silently fell back to
# LlamaIndex's default (OpenAI), needing OPENAI_API_KEY. Wire up a local Ollama
# LLM instead. Requires `llama-index-llms-ollama` and a running Ollama server.
Settings.llm = Ollama(model=LLM_MODEL, request_timeout=120.0)