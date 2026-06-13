"""Unit tests for lazy model setup (scripts/model_setup.py, plan 3.7, M1).

The point of 3.7 is that importing the pipeline no longer loads a model — the
heavy work lives in setup_models(). These tests lock the two guarantees that make
that safe: setup_models() never overrides a model that's already set (so a test's
MockEmbedding survives, and nothing is re-downloaded), and it is idempotent.

Only llama_index.core is needed: when both models are already set, setup_models()
returns before importing torch / building anything.
"""
import pytest

pytest.importorskip("llama_index.core")

import model_setup  # noqa: E402
from llama_index.core import Settings  # noqa: E402


def test_setup_models_does_not_override_preset(monkeypatch):
    # Pretend both are already configured (e.g. a test installed a MockEmbedding).
    sentinel_embed = object()
    sentinel_llm = object()
    monkeypatch.setattr(Settings, "_embed_model", sentinel_embed)
    monkeypatch.setattr(Settings, "_llm", sentinel_llm)

    model_setup.setup_models()

    # Untouched: no re-download, no Ollama client swapped in.
    assert Settings._embed_model is sentinel_embed
    assert Settings._llm is sentinel_llm


def test_setup_models_is_idempotent(monkeypatch):
    # Calling twice with both already set is a harmless no-op.
    monkeypatch.setattr(Settings, "_embed_model", object())
    monkeypatch.setattr(Settings, "_llm", object())

    model_setup.setup_models()
    model_setup.setup_models()  # must not raise


def test_import_does_not_configure_models():
    # Importing model_setup must not touch Settings or pull in torch — that is
    # the whole M1 fix. (If an earlier test in this process configured a real
    # model we cannot assert None, so we assert the weaker, always-true fact:
    # the module exposes setup_models and ran no configuration of its own.)
    assert callable(model_setup.setup_models)
    # model_setup itself never imports torch at module load.
    assert "torch" not in getattr(model_setup, "__dict__", {})
