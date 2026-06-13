"""Typed, env-driven configuration for the search pipeline (plan 3.1, H7).

Single place where deployment config is read from the environment, instead of
scattered `os.getenv` calls and hardcoded constants across the scripts. Other
modules keep their existing public names (e.g. `qdrant.COLLECTION_NAME`) but now
source the value from the `settings` singleton below.

Import-cheap on purpose: this module pulls in only pydantic-settings — no torch,
no llama-index, no model load, no file reads. Importing it never triggers heavy
work, so tests can read config without the ML stack.

Env var names are kept bare (no prefix) so docker-compose and the docs keep
working unchanged. Field names that differ from their env var get an explicit
validation alias.
"""
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    # pydantic-settings reads each field below from an environment variable of
    # the same name. `case_sensitive=False` means QDRANT_HOST and qdrant_host
    # both match; `extra="ignore"` means unrelated env vars are not an error.
    model_config = SettingsConfigDict(extra="ignore", case_sensitive=False)

    # --- Qdrant connection + collection ---
    qdrant_host: str = "localhost"
    qdrant_port: int = 6333
    # `validation_alias` binds this field to a DIFFERENTLY named env var: the
    # field is `collection_name`, but it is read from QDRANT_COLLECTION (the
    # name docker-compose and the docs already use).
    collection_name: str = Field("demo", validation_alias="QDRANT_COLLECTION")
    qdrant_autostart: bool = True

    # --- Models ---
    embed_model: str = "BAAI/bge-base-en-v1.5"
    llm_model: str = "llama3"

    # --- Cache paths ---
    # SQLite file for the embedding cache (plan 3.2). Keyed by
    # (model_name, text_hash), so switching models can never mix vectors.
    emb_cache_path: str = "./.claude/cache/embeddings.db"
    # Max rows kept in the embedding cache. When the table grows past this,
    # the least-recently-used rows are evicted (LRU). Stops the cache from
    # growing without bound and ages out vectors from old models.
    emb_cache_max_size: int = 50000

    # --- Query-time knobs (already env-driven before this task) ---
    rerank_candidates: int = 30
    # Max distinct queries kept in the in-memory retriever-results cache
    # (query_index._retrieve_cache). The warm service is long-lived, so this is
    # LRU-capped to stop the cache growing without bound (review finding M7).
    retrieve_cache_size: int = Field(256, validation_alias="RETRIEVE_CACHE_SIZE")
    cross_encoder_model: str = ""
    # Raw comma-separated string; split via the property below. Kept as a plain
    # str (not list/tuple) because pydantic parses complex env fields as JSON,
    # which would reject values like "implementation,.NET core backend".
    query_variant_suffixes_raw: str = Field(
        "implementation", validation_alias="QUERY_VARIANT_SUFFIXES"
    )

    # --- Service ---
    service_port: int = Field(8000, validation_alias="PORT")

    # --- Logging (plan 3.5, M11) ---
    # Root log level for the scripts: DEBUG shows the per-step traces, INFO (the
    # default) shows only status/progress. Read from LOG_LEVEL; setup_logging()
    # in logging_setup.py applies it.
    log_level: str = Field("INFO", validation_alias="LOG_LEVEL")

    # --- Security (plan 3.8, M10) ---
    # Qdrant auth + transport. Empty key = unauthenticated (the local Docker
    # default); set both for a secured/remote Qdrant (e.g. Qdrant Cloud, which
    # is API-key + TLS). qdrant.py passes these to the one QdrantClient.
    qdrant_api_key: str = Field("", validation_alias="QDRANT_API_KEY")
    qdrant_https: bool = Field(False, validation_alias="QDRANT_HTTPS")
    # Max bytes accepted on POST /search. Flask returns 413 for anything bigger,
    # before the body is parsed or embedded. Queries are short; 64 KB is ample.
    max_content_length: int = Field(65536, validation_alias="MAX_CONTENT_LENGTH")

    # --- ask.py (plan 3.9, M3) ---
    # Token budget for the code context ask.py sends to the LLM, counted with the
    # LLM tokenizer (not characters). llama3 has an 8k window; this leaves room
    # for the prompt scaffold + question + answer.
    max_context_tokens: int = Field(4000, validation_alias="MAX_CONTEXT_TOKENS")

    @property
    def query_variant_suffixes(self) -> tuple[str, ...]:
        """Comma-separated suffixes for extra query variants. Reproduces the
        exact parse query_index.py used inline (split, strip, drop empties)."""
        return tuple(
            s.strip()
            for s in self.query_variant_suffixes_raw.split(",")
            if s.strip()
        )


settings = Settings()
