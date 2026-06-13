"""One place to turn on logging for the CLI scripts and the service (plan 3.5).

Why this exists: the scripts used to log with bare `print()`, which had no
levels (you could not quiet the noise) and, in query_index.py, even replaced the
`print` built-in to force output to stderr. The standard `logging` module gives
us levels for free AND already writes to stderr by default — so stdout stays
clean for the JSON that query_index.py emits and the MCP protocol on
mcp_server.py.

Usage:
  - entry points (a script's `__main__`, the service's `main()`) call
    `setup_logging()` ONCE, before doing work;
  - every other module just does `logger = logging.getLogger(__name__)` and logs
    — it never configures logging itself.
"""
import logging
import sys

from config import settings


def setup_logging():
    """Configure the root logger once, from config.

    Level comes from LOG_LEVEL (default INFO; set LOG_LEVEL=DEBUG to see the
    per-step traces). The format adds the time, the level, and the module name,
    so a log line is self-describing. Everything goes to stderr, keeping stdout
    free for real output (JSON results, the MCP protocol).
    """
    level = settings.log_level.upper()
    logging.basicConfig(
        level=level,
        format="%(asctime)s %(levelname)-5s %(name)s: %(message)s",
        datefmt="%H:%M:%S",
        stream=sys.stderr,
    )
    # The Qdrant client logs every HTTP request through httpx/httpcore at INFO,
    # which would bury our own status lines. Keep them at WARNING unless the user
    # explicitly asked for DEBUG (then they want everything).
    if level != "DEBUG":
        for noisy in ("httpx", "httpcore"):
            logging.getLogger(noisy).setLevel(logging.WARNING)
