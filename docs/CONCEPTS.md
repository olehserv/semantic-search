# 🧠 Concepts (plain English)

Welcome! 👋 This guide explains the AI ideas behind this project — **no heavy
math, no jargon**. If you've never touched embeddings or vector search before,
you're exactly who this is written for.

We follow the search pipeline **in order**. Read top to bottom and you'll see
how a plain-English question like *"where do we check the password?"* turns into
a ranked list of the right code files. 🎯

Each concept has the same shape:

- 💡 **The idea** — one or two sentences.
- 🧪 **A tiny example** — concrete, with real-ish numbers or code.
- 🤔 **Why we use it** — what problem it solves here.
- 🌍 **An analogy** — the same idea from everyday life.
- 🛠️ **In this project** — where it lives in the code.

> Quick mental model of the whole thing:
>
> ```text
> your question ─► turn into numbers ─► find the nearest code ─► rank it ─► answer
> ```

---

## 0. First, what is a "vector"? 🔢

Before we start: a **vector** is just a **list of numbers**. That's it.

```text
[0.12, -0.04, 0.91, 0.33, ...]
```

You can think of each number as a coordinate. Two numbers `(x, y)` place a point
on a flat map. Three numbers `(x, y, z)` place it in 3D space. This project uses
**768 numbers** per piece of text — so each text is a point in a 768-dimensional
space. We can't picture 768 dimensions, but the computer does the same simple
thing it does in 2D: measure how close two points are. 📍

Keep that picture in mind — everything below builds on it.

---

## 1. Embeddings (turning text into vectors) ✍️➡️🔢

💡 **The idea:** An *embedding* turns a piece of text into a vector (those
numbers above). The magic part: texts with **similar meaning** get **similar
numbers** — even if they use different words.

🧪 **Tiny example:** imagine a model that only uses 2 numbers. It might place:

```text
"login"          -> [0.91, 0.10]
"sign in"        -> [0.88, 0.14]   ← almost the same spot as "login"!
"delete account" -> [0.10, 0.95]   ← far away, different meaning
```

Notice "login" and "sign in" land close together **without sharing a single
word**. That's the whole point — the model captures *meaning*, not spelling.

🤔 **Why we use it:** Computers can't compare meaning directly, but they compare
numbers in a flash. So we turn every code chunk into a vector **once** (at index
time), and later just compare the question's vector to them.

🌍 **Analogy:** A map. Every city is a pair of numbers (latitude, longitude).
Cities close on the map have close numbers. Embeddings do the same for meaning:
close in meaning → close in numbers.

🛠️ **In this project:** `model_setup.py` loads the embedding model
(`BAAI/bge-base-en-v1.5`, which outputs 768 numbers). `build_index.py` uses it to
embed every code chunk.

---

## 2. Vector database (Qdrant) 🗂️

💡 **The idea:** A *vector database* stores lots of vectors and answers one
question very fast: **"which stored vectors are nearest to this one?"**

🧪 **Tiny example:** you've stored 5,000 code-chunk vectors. You hand Qdrant your
question vector and say "give me the 6 closest." It returns them in milliseconds
— without comparing against all 5,000 one by one (it uses a clever index).

🤔 **Why we use it:** A real codebase has thousands of chunks. Checking each one
by hand every query would be slow. Qdrant is built exactly for fast
nearest-neighbor lookup.

🌍 **Analogy:** A library with a brilliant librarian. You describe what you want
and they instantly hand you the few closest books — without re-reading every book
on every shelf. 📚

🛠️ **In this project:** `qdrant.py` connects to [Qdrant](https://qdrant.tech/)
and starts it in Docker when needed. `build_index.py` writes vectors in;
`query_index.py` reads the nearest ones out.

---

## 3. Cosine similarity (how "close" two vectors are) 📐

💡 **The idea:** *Cosine similarity* is a score (roughly **0 to 1**) for how much
two vectors point in the **same direction**. Same direction → near **1** (very
alike). Right angle → **0** (unrelated).

🧪 **Tiny example** (2D, so we can see it):

```text
A = "login"   = [0.91, 0.10]
B = "sign in" = [0.88, 0.14]   → cosine(A, B) ≈ 0.99   (almost identical) ✅
C = "delete"  = [0.10, 0.95]   → cosine(A, C) ≈ 0.21   (very different)  ❌
```

It looks at the **angle** between the arrows, not how long they are. That's good:
we care about *meaning*, not how long the text was.

🤔 **Why we use it:** It's our ruler for "how similar in meaning are these two
texts?" — the core question of semantic search.

🌍 **Analogy:** Two people pointing at the night sky. Point the same way → you
mean the same star (≈1). Point in very different directions → different things
(≈0). The *distance you stand apart* doesn't matter, only the *direction*. ✨

🛠️ **In this project:** `ranking.py` → `cosine()`.

---

## 4. Keyword search (BM25) vs semantic search 🔑 vs 🧠

💡 **The idea:** Two very different ways to search:
- **Semantic search** — by *meaning* (uses the vectors above).
- **Keyword search (BM25)** — by *exact words*. BM25 is a classic, battle-tested
  formula that rewards rare matching words and doesn't over-reward long documents.

🧪 **Tiny example** — each one wins a different case:

| You search… | Semantic 🧠 | Keyword 🔑 |
|---|---|---|
| "where do we handle login" | ✅ finds `AuthService` even without the word "login" | 🤷 may miss it if the word "login" isn't there |
| `VerifyPassword` (exact method name) | 🤷 might drift to "similar" code | ✅ nails the exact match |

🤔 **Why we use both:** Code is full of exact names — a class `AuthService`, a
method `Validate`. Meaning-search is great for fuzzy questions but can miss an
exact identifier; keyword-search nails identifiers but misses paraphrases. Code
search needs **both**, so we run both.

🌍 **Analogy:** Looking for a book. Semantic search is asking a friend *"that
book about a boy wizard"* (meaning). Keyword search is typing the exact title
into the catalog (words). Sometimes you know the vibe; sometimes you know the
title. 🪄

🛠️ **In this project:** the semantic side is the vector retrievers; the keyword
side is `BM25Retriever`, rebuilt in memory from Qdrant in `query_index.py`.

---

## 5. Hybrid search + RRF fusion 🔀

💡 **The idea:** *Hybrid search* runs several searches and **merges** their
result lists into one. We merge with **RRF (Reciprocal Rank Fusion)**, which
combines lists by **rank** (1st, 2nd, 3rd…) — **not** by raw score.

🧮 **The formula** (don't worry, it's small): for each result, add up
`1 / (k + rank)` across every list it appears in. `k` is a small constant that
softens the gap between 1st and 2nd place. This project uses **`k = 60`**
(`RRF_K` in `ranking.py`).

🧪 **Worked example** — two searches return these top-3 lists:

```text
Semantic:  1) A    2) B    3) C
Keyword:   1) B    2) D    3) A
```

Score each file (k = 60):

```text
A: 1/(60+1)  + 1/(60+3)  = 0.0164 + 0.0159 = 0.0323   🥇
B: 1/(60+2)  + 1/(60+1)  = 0.0161 + 0.0164 = 0.0325   🥇 (just ahead)
C: 1/(60+3)                = 0.0159
D: 1/(60+2)                = 0.0161
```

**Merged order: B, A, D, C.** Notice B wins because it ranked high in *both*
lists — appearing in two searches is a strong signal. 💪

🤔 **Why rank, not raw score:** different searches use different scales. Cosine
is 0–1; BM25 can be any size (10, 50, …). Adding those raw numbers is unfair —
the big one drowns out the small one. But "being 1st" means the same thing in
every list, so rank is a fair common currency.

🌍 **Analogy:** Three judges rank the same contestants. One scores out of 10,
another out of 100. You shouldn't add their raw scores — instead you reward the
**place** each judge gave (1st, 2nd…). Fair, and that's RRF. 🏅

🛠️ **In this project:** `ranking.py` → `rrf_scores()` does the fusion;
`query_index.py` runs the searches and passes their ranked lists in. (It actually
runs **three** retrievers across a few question variants, then fuses all of them.)

---

## 6. Re-ranking: bi-encoder vs cross-encoder 🔬

💡 **The idea:** After fusion gives us a short list of good candidates, we take a
**closer look** and re-score them. Two ways:

- **Bi-encoder (cosine, the default):** compare the question's vector with each
  chunk's pre-made vector. ⚡ Fast, because the chunk vectors already exist.
- **Cross-encoder (optional):** feed the question **and** the chunk into the
  model **together** and score the pair. 🧐 Slower, but sharper judgment.

🧪 **Tiny example:** for the question "how do we hash passwords?" and a chunk
about `BCrypt.HashPassword(...)`:
- the bi-encoder sees two separate vectors that happen to be close;
- the cross-encoder reads *both at once* and can notice the chunk **directly
  answers** the question — so it may bump that chunk from rank 4 up to rank 1.

🤔 **Why we offer both:** the cross-encoder ranks better but must run once **per
candidate, per question** (it can't pre-compute), so it's slower. It's **off by
default** and only turns on when you set `CROSS_ENCODER_MODEL`.

⚖️ **One more point:** even when it's on, the cross-encoder score is **blended**
with the RRF score, not used alone. Used alone, it tended to ignore exact keyword
matches — which code search really needs.

🌍 **Analogy:** Hiring. 📄 The bi-encoder is matching résumés to a job by
keywords — quick and rough. The cross-encoder is a real **interview** — you read
the candidate and the job side by side. Better judgment, but it takes time, so
you only interview the short list, not everyone.

🛠️ **In this project:** `ranking.py` → `score_candidates()` (cosine) and
`blend_cross_encoder()` (cross-encoder). `query_index.py` picks which to use.

---

## 7. Chunking (splitting code before embedding) ✂️

💡 **The idea:** *Chunking* splits big files into smaller pieces **before**
embedding. For C# we split on **type and method borders**, not on a fixed number
of lines.

🧪 **Tiny example** — one file becomes clean, whole chunks:

```text
LoginService.cs
 ├── chunk 1:  class LoginService { ...fields... }
 ├── chunk 2:  public bool VerifyPassword(string user, string pw) { ... }
 └── chunk 3:  public void Logout() { ... }
```

A naïve "cut every 40 lines" approach might slice `VerifyPassword` in half — half
its logic in one chunk, half in another — and **both** chunks become confusing.
Cutting on real code borders keeps each piece whole. We also keep the **type
name** (`LoginService`) on every chunk, so a chunk always says where it came
from.

🤔 **Why we use it:** A whole file is too big for one vector to represent well
(too many ideas squeezed into one point). Small, meaningful chunks embed far
better — and well-chunked code was the **biggest single quality win** in this
project's evaluation. 📈

🌍 **Analogy:** Cutting a cookbook into recipes. You cut **between** recipes, not
in the middle of one — and each page keeps its recipe title at the top, so you
always know what you're reading. 🍪

🛠️ **In this project:** `chunking.py` uses **tree-sitter** to find the code
borders. Files that aren't C# (or don't parse) fall back to a simple sentence
splitter in `build_index.py`.

---

## 🎬 Putting it all together

Let's trace one question through the whole pipeline:

> ❓ **"where is the user password verified at login?"**

1. ✍️➡️🔢 **Embed** the question into a 768-number vector (concept 1).
2. 🗂️ Ask **Qdrant** for the nearest chunk vectors (concept 2), scored by
   **cosine similarity** (concept 3).
3. 🔑 At the same time, run **BM25 keyword** search for words like "password",
   "verify", "login" (concept 4).
4. 🔀 **Fuse** all those ranked lists with **RRF** so results strong in *several*
   searches rise to the top (concept 5).
5. 🔬 **Re-rank** the short list for a final, sharper order (concept 6).
6. 📦 Return the top chunks — each a clean, whole piece of code thanks to
   **chunking** (concept 7) — as ranked JSON.

The result: `LoginService.cs` and friends, in sensible order. 🎉

Want to see the moving parts in code? Start at `scripts/query_index.py`
(`query()`), which calls into `ranking.py` for all the scoring. And the
[README](../README.md) shows how to run the whole thing.
