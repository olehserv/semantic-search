"""Unit tests for the env-driven config layer (scripts/config.py, plan 3.1).

These pin down defaults, env-var overrides, and the comma-separated parse of
QUERY_VARIANT_SUFFIXES (the one knob that feeds the eval). Needs only
pydantic-settings, so it runs on a bare interpreter — no ML stack.

Each test instantiates a fresh Settings() (not the module singleton, which is
built once at import) and clears the relevant env vars first, so the
developer's shell can't leak values into the assertions.
"""
import pytest

pytest.importorskip("pydantic_settings")

from config import Settings

# Every env var the config layer reads — cleared before each test.
CONFIG_ENV_VARS = (
    "QDRANT_HOST",
    "QDRANT_PORT",
    "QDRANT_COLLECTION",
    "QDRANT_AUTOSTART",
    "EMBED_MODEL",
    "LLM_MODEL",
    "EMB_CACHE_PATH",
    "EMB_CACHE_MAX_SIZE",
    "RERANK_CANDIDATES",
    "CROSS_ENCODER_MODEL",
    "PORT",
    "QUERY_VARIANT_SUFFIXES",
    "LOG_LEVEL",
    "QDRANT_API_KEY",
    "QDRANT_HTTPS",
    "MAX_CONTENT_LENGTH",
)


@pytest.fixture
def clean_env(monkeypatch):
    for name in CONFIG_ENV_VARS:
        monkeypatch.delenv(name, raising=False)
    return monkeypatch


def test_defaults(clean_env):
    s = Settings()
    assert s.qdrant_host == "localhost"
    assert s.qdrant_port == 6333
    assert s.collection_name == "demo"
    assert s.qdrant_autostart is True
    assert s.embed_model == "BAAI/bge-base-en-v1.5"
    assert s.llm_model == "llama3"
    assert s.emb_cache_path == "./.claude/cache/embeddings.db"
    assert s.emb_cache_max_size == 50000
    assert s.rerank_candidates == 30
    assert s.cross_encoder_model == ""
    assert s.service_port == 8000
    assert s.query_variant_suffixes == ("implementation",)
    assert s.log_level == "INFO"
    assert s.qdrant_api_key == ""
    assert s.qdrant_https is False
    assert s.max_content_length == 65536


def test_env_overrides(clean_env):
    clean_env.setenv("QDRANT_COLLECTION", "lfm")
    clean_env.setenv("QDRANT_PORT", "7000")
    clean_env.setenv("PORT", "9000")
    clean_env.setenv("QDRANT_AUTOSTART", "0")
    clean_env.setenv("RERANK_CANDIDATES", "50")
    clean_env.setenv("LOG_LEVEL", "DEBUG")
    clean_env.setenv("QDRANT_API_KEY", "secret")
    clean_env.setenv("QDRANT_HTTPS", "1")
    clean_env.setenv("MAX_CONTENT_LENGTH", "1024")
    s = Settings()
    assert s.collection_name == "lfm"
    assert s.qdrant_port == 7000
    assert isinstance(s.qdrant_port, int)
    assert s.service_port == 9000
    assert s.qdrant_autostart is False
    assert s.rerank_candidates == 50
    assert s.log_level == "DEBUG"
    assert s.qdrant_api_key == "secret"
    assert s.qdrant_https is True
    assert s.max_content_length == 1024


def test_query_variant_suffixes_default(clean_env):
    assert Settings().query_variant_suffixes == ("implementation",)


def test_query_variant_suffixes_comma_separated(clean_env):
    # Must NOT be parsed as JSON: a bare comma-separated string with a dotted
    # term has to survive intact (this is the load-bearing edge case).
    clean_env.setenv("QUERY_VARIANT_SUFFIXES", "implementation,.NET core backend")
    assert Settings().query_variant_suffixes == ("implementation", ".NET core backend")


def test_query_variant_suffixes_strips_and_drops_empties(clean_env):
    clean_env.setenv("QUERY_VARIANT_SUFFIXES", "a, , b,")
    assert Settings().query_variant_suffixes == ("a", "b")
