"""Unit tests for setup_logging (scripts/logging_setup.py, plan 3.5).

setup_logging() configures the root logger from settings.log_level. We test the
logic our code owns — that it passes the right level (upper-cased), stderr as
the stream, and a structured format to logging.basicConfig — by capturing the
basicConfig call. (We do NOT assert on the live root logger: pytest's own
logging plugin attaches handlers around each test, which makes basicConfig a
no-op and the global level unreliable to check here.)

Needs only stdlib logging + the config layer (pydantic-settings), no ML stack.
"""
import sys

import pytest

pytest.importorskip("pydantic_settings")

import logging_setup  # noqa: E402


@pytest.fixture
def captured_basic_config(monkeypatch):
    """Replace logging.basicConfig so we can inspect the kwargs setup_logging
    passes, instead of mutating the process-wide root logger."""
    captured = {}
    monkeypatch.setattr(
        logging_setup.logging, "basicConfig", lambda **kw: captured.update(kw)
    )
    return captured


def test_default_level_is_info(captured_basic_config, monkeypatch):
    monkeypatch.setattr(logging_setup.settings, "log_level", "INFO")
    logging_setup.setup_logging()
    assert captured_basic_config["level"] == "INFO"


def test_level_is_upper_cased(captured_basic_config, monkeypatch):
    # A lower-case LOG_LEVEL must still resolve to a valid level name.
    monkeypatch.setattr(logging_setup.settings, "log_level", "debug")
    logging_setup.setup_logging()
    assert captured_basic_config["level"] == "DEBUG"


def test_logs_to_stderr_with_structured_format(captured_basic_config, monkeypatch):
    monkeypatch.setattr(logging_setup.settings, "log_level", "INFO")
    logging_setup.setup_logging()
    # stderr keeps stdout free for JSON (query_index) and the MCP protocol.
    assert captured_basic_config["stream"] is sys.stderr
    # The format carries level + logger name so a line is self-describing.
    fmt = captured_basic_config["format"]
    assert "%(levelname)" in fmt
    assert "%(name)s" in fmt
    assert "%(message)s" in fmt
