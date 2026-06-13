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
    model_config = SettingsConfigDict(extra="ignore", case_sensitive=False)

    # --- Qdrant connection + collection ---
    qdrant_host: str = "localhost"
    qdrant_port: int = 6333
    collection_name: str = Field("demo", validation_alias="QDRANT_COLLECTION")
    qdrant_autostart: bool = True

    # --- Models ---
    embed_model: str = "BAAI/bge-base-en-v1.5"
    llm_model: str = "llama3"

    # --- Cache paths ---
    emb_cache_path: str = "./.claude/cache/embeddings.pkl"

    # --- Query-time knobs (already env-driven before this task) ---
    rerank_candidates: int = 30
    cross_encoder_model: str = ""
    # Raw comma-separated string; split via the property below. Kept as a plain
    # str (not list/tuple) because pydantic parses complex env fields as JSON,
    # which would reject values like "implementation,.NET core backend".
    query_variant_suffixes_raw: str = Field(
        "implementation", validation_alias="QUERY_VARIANT_SUFFIXES"
    )

    # --- Service ---
    service_port: int = Field(8000, validation_alias="PORT")

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
