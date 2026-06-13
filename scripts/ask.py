"""Answer a question in words, using the code the search found.

Pipeline on top of query(): search the code -> guess what kind of question it
is (locate / flow / debug / explain) -> build a prompt with the found code
(trimmed to a token budget) -> ask a local LLM (Ollama) once. Run it from the
command line:

    python scripts/ask.py "how does the request pipeline work"
"""
import logging

import model_setup
from query_index import query
from llama_index.core import Settings

from config import settings

logger = logging.getLogger(__name__)


def _format_chunk(c):
    """One labelled section for the prompt: rank, file, path, score, then code."""
    return f"[{c['rank']}] {c['file']} ({c['path']}) score={c['score']}\n{c['text']}"


def build_context(context, max_tokens):
    """Join chunk sections in rank order, stopping before the token budget.

    Counts TOKENS with the LLM tokenizer, not characters (review finding M3):
    code tokenizes very differently from prose, and the model's window is in
    tokens. Whole chunks only — never slice a snippet mid-way. The top chunk is
    always kept, even if it alone is over budget.
    """
    tokenizer = Settings.tokenizer
    sections = []
    used = 0
    for c in context:
        section = _format_chunk(c)
        n = len(tokenizer(section))
        if sections and used + n > max_tokens:
            break
        sections.append(section)
        used += n
    return "\n\n".join(sections)


def detect_mode(q):
    """Guess the kind of question from its words, to pick the right instruction.

    Modes: "locate" (where is X), "flow" (how does X work), "debug" (find a bug),
    or "explain" (anything else). This is a simple keyword check, so it is just a
    best-effort guess — the prompt still works if the guess is off.
    """
    q_lower = q.lower()

    if any(x in q_lower for x in ["where", "which file", "location", "located", "find file"]):
        return "locate"

    if any(x in q_lower for x in ["how", "flow", "process", "works"]):
        return "flow"

    if any(x in q_lower for x in ["bug", "issue", "problem", "error"]):
        return "debug"

    return "explain"


def get_instruction(mode):
    """Return the extra prompt instruction that matches the question mode."""
    instructions = {
        "explain": """
Explain how the system works based on the code.
Focus on architecture and relationships.
""",
        "locate": """
Find where the functionality is implemented.
Mention file names and locations.
""",
        "flow": """
Describe execution flow step by step.
""",
        "debug": """
Analyze code and identify potential issues or bugs.
"""
    }

    return instructions.get(mode, "")


def ask(q, mode=None):
    """Answer the question `q` in words, based only on the code we found.

    Searches the code, trims the context to the token budget, picks a mode,
    builds the prompt, and calls the LLM once. Returns the answer text, or a
    short message if nothing relevant was found.
    """
    model_setup.setup_models()  # ensure Settings.llm is ready (lazy, plan 3.7)
    # Fetch a few more chunks than the search default so the larger token budget
    # has something to fill; build_context() then trims to the budget.
    result = query(q, top_k=12)

    if not result["context"]:
        return "No relevant code found."

    context = build_context(result["context"], settings.max_context_tokens)

    if mode is None:
        mode = detect_mode(q)

    # With very little code found, "explain"/"flow" answers would be guesswork.
    # Fall back to "locate" — at least point to the files we did find.
    if len(result["context"]) < 3:
        if mode in ["explain", "flow"]:
            mode = "locate"

    logger.debug("Mode: %s", mode)

    prompt = f"""
You are a senior .NET engineer.

Analyze the code snippets and answer the question.

Instructions:
{get_instruction(mode)}

Rules:
- Base your answer ONLY on the provided code
- Do not hallucinate
- If unsure, say so clearly
- Always reference file names when possible
- Use structured explanations (bullet points if helpful)

Question:
{q}

Code:
{context}
"""

    try:
        return Settings.llm.complete(prompt).text
    except Exception as e:
        return f"LLM error: {str(e)}\n\nContext was:\n{context[:1000]}"



if __name__ == "__main__":
    import sys
    from logging_setup import setup_logging
    setup_logging()
    q = " ".join(sys.argv[1:])
    # The answer is the program's real output -> stdout. Logs go to stderr.
    print(ask(q))
