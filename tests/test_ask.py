"""Tests for the Q&A CLI (scripts/ask.py, plan 3.9, M3).

ask.py imports query_index (needs llama-index), so these skip on a bare
interpreter. No real model or LLM: query(), Settings (its .llm + .tokenizer),
and model_setup.setup_models are all faked. The tokenizer fake counts words, so
the token budget is easy to reason about in the assertions.
"""
import types

import pytest

pytest.importorskip("llama_index.core")

import ask  # noqa: E402


class FakeLLM:
    """Records how many times complete() is called and returns a fixed answer."""

    def __init__(self, text="the answer"):
        self.text = text
        self.calls = 0

    def complete(self, prompt):
        self.calls += 1
        return types.SimpleNamespace(text=self.text)


@pytest.fixture
def fake_env(monkeypatch):
    """Fake Settings (llm + word-counting tokenizer) and a no-op setup_models."""
    llm = FakeLLM()
    monkeypatch.setattr(
        ask, "Settings",
        types.SimpleNamespace(llm=llm, tokenizer=lambda text: text.split()),
    )
    monkeypatch.setattr(ask.model_setup, "setup_models", lambda: None)
    return {"llm": llm}


def _ctx(n):
    """n fake result chunks, each ~ a few words of text."""
    return [
        {"rank": i + 1, "file": f"F{i}.cs", "path": f"src/F{i}.cs",
         "score": 0.9, "text": f"word word word chunk {i}"}
        for i in range(n)
    ]


# --- detect_mode ---

def test_detect_mode():
    assert ask.detect_mode("where is the password checked") == "locate"
    assert ask.detect_mode("how does login work") == "flow"
    assert ask.detect_mode("why is there a bug in auth") == "debug"
    assert ask.detect_mode("tell me about the design") == "explain"


# --- build_context (token budget, whole chunks) ---

def test_build_context_stops_at_token_budget(fake_env):
    # Each section is well over 5 "tokens" (words), so a 5-token budget keeps
    # only the first chunk; the rest are dropped (whole chunks, never sliced).
    out = ask.build_context(_ctx(3), max_tokens=5)
    assert "F0.cs" in out
    assert "F1.cs" not in out and "F2.cs" not in out


def test_build_context_keeps_top_chunk_even_if_over_budget(fake_env):
    # Budget of 0 still keeps the first chunk (better one chunk than none).
    out = ask.build_context(_ctx(2), max_tokens=0)
    assert "F0.cs" in out
    assert "F1.cs" not in out


def test_build_context_includes_all_when_budget_is_large(fake_env):
    out = ask.build_context(_ctx(3), max_tokens=10_000)
    assert "F0.cs" in out and "F1.cs" in out and "F2.cs" in out


# --- ask() ---

def test_ask_happy_path_single_llm_call(fake_env, monkeypatch):
    monkeypatch.setattr(ask, "query", lambda q, top_k=12: {"context": _ctx(4)})
    answer = ask.ask("how does login work")
    assert answer == "the answer"
    assert fake_env["llm"].calls == 1   # no retry — exactly one call


def test_ask_no_context_skips_llm(fake_env, monkeypatch):
    monkeypatch.setattr(ask, "query", lambda q, top_k=12: {"context": []})
    answer = ask.ask("anything")
    assert answer == "No relevant code found."
    assert fake_env["llm"].calls == 0   # nothing to ask about


def test_ask_llm_error_is_returned_not_raised(fake_env, monkeypatch):
    monkeypatch.setattr(ask, "query", lambda q, top_k=12: {"context": _ctx(2)})

    def boom(prompt):
        raise RuntimeError("ollama down")
    monkeypatch.setattr(ask.Settings.llm, "complete", boom)

    answer = ask.ask("x")
    assert answer.startswith("LLM error:")
    assert "ollama down" in answer
