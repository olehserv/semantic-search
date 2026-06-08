from query_index import query
from llama_index.core import Settings

DEBUG = True

def format_context(context):
    return "\n\n".join(
        f"[{c['rank']}] {c['file']} ({c['path']}) score={c['score']}\n{c['text']}"
        for c in context
    )

MAX_CONTEXT_CHARS = 6000


def detect_mode(q):
    q_lower = q.lower()

    if any(x in q_lower for x in ["where", "which file", "location", "located", "find file"]):
        return "locate"

    if any(x in q_lower for x in ["how", "flow", "process", "works"]):
        return "flow"

    if any(x in q_lower for x in ["bug", "issue", "problem", "error"]):
        return "debug"

    return "explain"


def get_instruction(mode):
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
    response_text = response.text

    if tried:
        return response
    
    if not any(x in response_text.lower() for x in [
        "not enough information",
        "insufficient",
        "cannot determine",
        "not clear"
    ]):
        return response

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
    result = query(q)
        
    if not result["context"]:
        return "No relevant code found."

    context = format_context(result["context"])

    if len(context) > MAX_CONTEXT_CHARS:
        trimmed = context[:MAX_CONTEXT_CHARS]
        context = trimmed.rsplit("\n\n", 1)[0] if "\n\n" in trimmed else trimmed

    if mode is None:
        mode = detect_mode(q)

    if len(result["context"]) < 3:
        if mode in ["explain", "flow"]:
            mode = "locate"

    if DEBUG:
        print(f"[DEBUG] Mode: {mode}")

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
    q = " ".join(sys.argv[1:])
    print(ask(q))
