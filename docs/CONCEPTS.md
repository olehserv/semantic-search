# Concepts (plain English)

This guide explains the AI ideas this project uses. No deep math. Each idea has
three parts: a **simple idea**, **why we use it**, and a **real-world analogy**.

The concepts follow the search pipeline, in order. If you read them top to
bottom, you will understand how a question turns into ranked code.

---

## 1. Embeddings (vectors)

**Simple idea:** An embedding turns a piece of text into a list of numbers (a
"vector"). Texts with a similar meaning get similar numbers.

**Why we use it:** Computers cannot compare meaning directly. But they can
compare numbers fast. So we turn every code chunk into numbers once, and later
compare the question's numbers to them.

**Analogy:** Think of a map. Every city is a pair of numbers (latitude,
longitude). Cities close on the map are close in the numbers too. Embeddings do
the same for meaning: close in meaning means close in the numbers.

*In this project:* `model_setup.py` loads the embedding model. `build_index.py`
uses it to embed every chunk.

---

## 2. Vector database (Qdrant)

**Simple idea:** A vector database stores many vectors and finds the ones
closest to a given vector, quickly.

**Why we use it:** We may have thousands of code chunks. Checking them one by one
is slow. A vector database is built to answer "which vectors are nearest?" fast.

**Analogy:** A library with a very good librarian. You describe what you want,
and the librarian instantly hands you the few closest books — without reading
every book on every shelf.

*In this project:* `qdrant.py` connects to [Qdrant](https://qdrant.tech/) and
starts it in Docker when needed.

---

## 3. Cosine similarity

**Simple idea:** Cosine similarity is a score (about 0 to 1) for how close two
vectors point in the same direction. Higher means more alike.

**Why we use it:** It is how we measure "how similar in meaning". It ignores
length and looks only at direction, which is what we want for meaning.

**Analogy:** Two people pointing at the sky. If they point the same way, they
mean the same star (score near 1). If they point in very different directions,
they mean different things (score near 0).

*In this project:* `ranking.py` → `cosine()`.

---

## 4. Keyword search (BM25) vs semantic search

**Simple idea:** Keyword search (BM25) finds exact words. Semantic search finds
meaning, even when the words differ.

**Why we use both:** Meaning search is great for questions like "where do we
handle login". But code has exact names — a class `AuthService`, a method
`Validate`. If you search that exact name, keyword search is better. Code search
needs both.

**Analogy:** Looking for a book. Semantic search is asking a friend "that book
about a boy wizard" (meaning). Keyword search is looking up the exact title in
the index (words). Sometimes you know the vibe; sometimes you know the title.

*In this project:* the semantic side is the vector retrievers; the keyword side
is `BM25Retriever`, built in `query_index.py`.

---

## 5. Hybrid search + RRF fusion

**Simple idea:** Hybrid search runs several searches and then merges their result
lists into one. We merge with **RRF** (Reciprocal Rank Fusion), which combines
lists by **rank** (1st, 2nd, 3rd...), not by raw score.

**Why we use rank, not score:** Different searches give scores on different
scales. Cosine is 0 to 1. BM25 can be any size. Mixing those raw numbers is
unfair — one would drown out the other. Rank is the same idea for everyone: being
1st means the same thing in every list.

**Analogy:** Three judges rank the same contestants. One judge scores out of 10,
another out of 100. You should not add their raw scores. Instead, you reward the
*place* each judge gave (1st, 2nd...). That is fair, and that is RRF.

*In this project:* `ranking.py` → `rrf_scores()` does the fusion. `query_index.py`
runs the searches and passes their ranked lists in.

---

## 6. Re-ranking: bi-encoder vs cross-encoder

**Simple idea:** After we have a short list of good candidates, we take a closer
look and re-score them. Two ways:

- **Bi-encoder (cosine, default):** compare the question's vector with each
  chunk's vector. Fast, because chunk vectors are made ahead of time.
- **Cross-encoder (optional):** read the question and the chunk *together* and
  score the pair. Slower, but more accurate.

**Why we offer both:** The cross-encoder ranks better, but it must run once per
candidate for every question, so it is slower. It is **off by default** and only
turned on when you set `CROSS_ENCODER_MODEL`.

**Analogy:** Hiring. The bi-encoder is like matching résumés to a job by keywords
— quick, rough. The cross-encoder is a real interview — you read the candidate
and the job side by side. Better judgment, but it takes more time, so you only
interview the short list.

**One more point:** even when on, the cross-encoder score is *blended* with the
RRF score, not used alone. Used alone it ignored exact keyword matches, which
code search needs.

*In this project:* `ranking.py` → `score_candidates()` (cosine) and
`blend_cross_encoder()` (cross-encoder). `query_index.py` picks which one to use.

---

## 7. Chunking

**Simple idea:** Chunking splits big files into smaller pieces before embedding.
For C# we split on type and method borders, not on a fixed number of lines.

**Why we use it:** A whole file is too big for one vector to capture well. But a
random cut can split a method in half, which makes a confusing chunk. Cutting on
code borders keeps each chunk whole and meaningful. We also keep the type
signature on every piece, so a chunk always says which class it came from.

**Analogy:** Cutting a cookbook into recipes. You cut between recipes, not in the
middle of one. Each page still has the recipe title on top, so you always know
what you are reading.

*In this project:* `chunking.py` uses tree-sitter to find the code borders.
Files that are not C# (or do not parse) fall back to a simple sentence splitter
in `build_index.py`.
