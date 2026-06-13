"""Answer a question in words, using the code the search found.

Pipeline on top of query(): search the code -> guess what kind of question it
is (locate / flow / debug / explain) -> build a prompt with the found code ->
ask a local LLM (Ollama) -> if the answer says it lacked context, retry once
with more code. Run it from the command line:

    python scripts/ask.py "how does the request pipeline work"
"""
import logging

from query_index import query
from llama_index.core import Settings

logger = logging.getLogger(__name__)


def format_context(context):
    """Turn the search results into one text block for the LLM prompt.

    Each chunk becomes a labelled section: rank, file, path, score, then the code.
    """
    return "\n\n".join(
        f"[{c['rank']}] {c['file']} ({c['path']}) score={c['score']}\n{c['text']}"
        for c in context
    )

MAX_CONTEXT_CHARS = 6000


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

def ensure_answer_fallback(response, q, tried=False):
    """If the answer admits it lacked context, retry once with more code.

    The `tried` flag makes this a one-shot retry (it calls itself once with
    tried=True), so we never loop forever.

    How we detect a weak answer: we look for phrases like "not enough
    information". This is a simple keyword check, so it is best-effort — a weak
    answer worded differently will slip through. Good enough as a safety net.
    """
    response_text = response.text

    # Second pass already happened -> accept whatever we got.
    if tried:
        return response

    # The first answer did NOT complain about missing context -> keep it.
    if not any(x in response_text.lower() for x in [
        "not enough information",
        "insufficient",
        "cannot determine",
        "not clear"
    ]):
        return response

    # Otherwise: fetch more chunks (top_k=12) and ask again, just once.
    extra = query(q, top_k=12)
    context2 = format_context(extra["context"])

    prompt2 = f"""
You are a senior .NET engineer.

The previous answer was insufficient because context was too limited.

Now you have more code snippets.

Instructions:
- Re-evaluate the question
- Use the extended context
- Provide a complete answer

Question:
{q}

Extended code:
{context2}
"""

    new_response = Settings.llm.complete(prompt2)

    return ensure_answer_fallback(new_response, q, tried=True)


def ask(q, mode=None):
    """Answer the question `q` in words, based only on the code we found.

    Searches the code, trims the context to a safe size, picks a mode, builds
    the prompt, and calls the LLM (with the one-shot fallback). Returns the
    answer text, or a short message if nothing relevant was found.
    """
    result = query(q)

    if not result["context"]:
        return "No relevant code found."

    context = format_context(result["context"])

    # Keep the prompt under the size limit. Cut on a chunk border ("\n\n") so we
    # do not slice a code snippet in half.
    if len(context) > MAX_CONTEXT_CHARS:
        trimmed = context[:MAX_CONTEXT_CHARS]
        context = trimmed.rsplit("\n\n", 1)[0] if "\n\n" in trimmed else trimmed

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
        response = ensure_answer_fallback(Settings.llm.complete(prompt), q)
        return response.text
    except Exception as e:
        return f"LLM error: {str(e)}\n\nContext was:\n{context[:1000]}"



if __name__ == "__main__":
    import sys
    from logging_setup import setup_logging
    setup_logging()
    q = " ".join(sys.argv[1:])
    # The answer is the program's real output -> stdout. Logs go to stderr.
    print(ask(q))
