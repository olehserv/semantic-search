*🇺🇦 Це український переклад. Оригінал англійською: `docs/superpowers/plans/2026-06-01-continuous-search-service.md`.*

# План впровадження безперервного сервісу пошуку

> **Для агентних виконавців:** ОБОВ'ЯЗКОВА ПІДНАВИЧКА: Використовуйте superpowers:subagent-driven-development (рекомендовано) або superpowers:executing-plans, щоб впроваджувати цей план задача за задачею. Кроки використовують синтаксис чекбоксів (`- [ ]`) для відстеження.

**Мета:** Замінити підпроцес на кожен запит довгоживучим сервісом пошуку Flask, який тримає прогріті векторний/BM25/широкий ретривери, прибрати pickle-кеш BM25 (перебудовувати BM25 у пам'яті з Qdrant) і зробити `mcp_server.py` тонким HTTP-перенаправлювачем.

**Архітектура:** `build_index.py` залишається офлайн-індексатором. Новий `service.py` (Flask, один воркер) один раз прогріває рушій `query_index` при запуску — включно з BM25, перебудованим у пам'яті з вузлів, відновлених з колекції Qdrant — та обслуговує `POST /search`. `mcp_server.py` перенаправляє запити зі stdin до сервісу через HTTP. Сервіс працює в Docker поруч із Qdrant; ембединги автоматично визначають CUDA/CPU.

**Технологічний стек:** Python 3.11, LlamaIndex (core + qdrant + huggingface + bm25), Qdrant, Flask, Docker Compose, pytest.

---

## Нотатки для виконавця

- Скрипти цього репозиторію живуть у `scripts/` та імпортують одне одного за голою назвою модуля (`import qdrant`, `import model_setup`), тому вони працюють із `scripts/` у `sys.path` (CWD = `scripts/`, або розгорнутий `.claude/scripts/`).
- `query_index.py` перенаправляє свій `print` на рівні модуля у **stderr** навмисно — залишайте нові діагностичні друки як `print(...)`; вони автоматично підуть у stderr.
- Імпорт `query_index` запускає `import model_setup`, який завантажує модель ембедингів. Тестовий `conftest.py` (Задача 1) підставляє заглушку для `model_setup`, щоб тести не завантажували/не вантажили модель. Не прибирайте цю заглушку.
- Три тестові файли можна запускати в різних середовищах:
  - `tests/test_mcp_server.py` — потребує лише `requests` + `pytest` (без ML-стеку).
  - `tests/test_service.py`, `tests/test_node_loader.py` — потребують встановлених `llama-index-core`, `qdrant-client`, `flask`, `pytest` (заглушка `model_setup` уникає завантаження моделі ембедингів).
- Після кожної зміни коду запускайте `python -m py_compile` на змінених файлах як швидку перевірку.

---

## Задача 1: Тестова обв'язка + маніфести залежностей

**Файли:**
- Створити: `requirements.txt`
- Створити: `requirements-dev.txt`
- Створити: `tests/conftest.py`

- [ ] **Крок 1: Створити `requirements.txt`**

```text
llama-index-core
llama-index-vector-stores-qdrant
llama-index-embeddings-huggingface
llama-index-llms-ollama
llama-index-retrievers-bm25
qdrant-client
torch
numpy
requests
flask
```

- [ ] **Крок 2: Створити `requirements-dev.txt`**

```text
-r requirements.txt
pytest
```

- [ ] **Крок 3: Створити `tests/conftest.py`**

```python
import os
import sys
import types

# Make the scripts importable by bare module name (import qdrant, import service, ...).
SCRIPTS = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "scripts"))
if SCRIPTS not in sys.path:
    sys.path.insert(0, SCRIPTS)

# Stub model_setup so importing query_index/service does not download or load the
# HuggingFace embedding model during tests. query() is monkeypatched in service tests,
# and load_all_nodes does not use the embed model.
if "model_setup" not in sys.modules:
    sys.modules["model_setup"] = types.ModuleType("model_setup")
```

- [ ] **Крок 4: Перевірити, що pytest збирає тести, хоча їх ще немає**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest -q`
Очікувано: код виходу 5 / "no tests ran" (збір працює, без помилок).

- [ ] **Крок 5: Коміт**

```bash
git add requirements.txt requirements-dev.txt tests/conftest.py
git commit -m "chore: add dependency manifests and pytest scaffolding"
```

---

## Задача 2: `qdrant.py` — host/port із env + гейт автозапуску

**Файли:**
- Змінити: `scripts/qdrant.py`

- [ ] **Крок 1: Зробити host/port керованими через env**

Замінити:

```python
QDRANT_HOST = "localhost"
QDRANT_PORT = 6333
```

на:

```python
QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")
QDRANT_PORT = int(os.getenv("QDRANT_PORT", "6333"))
```

(`os` вже імпортовано.)

- [ ] **Крок 2: Додати гейт `QDRANT_AUTOSTART` до `ensure_qdrant()`**

Замінити всю функцію `ensure_qdrant` на:

```python
def ensure_qdrant():
    if get_Qdrant_client() is not None:
        return

    # Inside a container (QDRANT_AUTOSTART=0) we cannot run docker — just wait
    # for the Qdrant service (started via compose depends_on) to become reachable.
    if os.getenv("QDRANT_AUTOSTART", "1") != "1":
        for _ in range(30):
            if get_Qdrant_client() is not None:
                print("✅ Qdrant is ready")
                return
            time.sleep(1)
        raise RuntimeError("❌ Qdrant not reachable")

    print("🚀 Starting Qdrant container...")

    try:
        subprocess.run(
            ["docker", "compose", "-f", compose_file, "-p", "ai-agent", "up", "-d"],
            check=True
        )
    except (subprocess.CalledProcessError, FileNotFoundError):
        subprocess.run(
            ["docker-compose", "-f", compose_file, "-p", "ai-agent", "up", "-d"],
            check=True
        )

    for _ in range(15):
        if get_Qdrant_client() is not None:
            print("✅ Qdrant is ready")
            return
        time.sleep(1)

    raise RuntimeError("❌ Qdrant failed to start")
```

- [ ] **Крок 3: Перевірка компіляції**

Запустіть: `cd /home/kali/p/semantic-search && python -m py_compile scripts/qdrant.py`
Очікувано: без виводу (успіх).

- [ ] **Крок 4: Коміт**

```bash
git add scripts/qdrant.py
git commit -m "feat: make Qdrant host/port env-driven and gate container autostart"
```

---

## Задача 3: `model_setup.py` — автовизначення обчислювального пристрою

**Файли:**
- Змінити: `scripts/model_setup.py`

- [ ] **Крок 1: Автовизначення пристрою**

Замінити:

```python
Settings.embed_model = HuggingFaceEmbedding(
    model_name="BAAI/bge-base-en-v1.5",
    device="cuda"
)
```

на:

```python
import torch

device = "cuda" if torch.cuda.is_available() else "cpu"

Settings.embed_model = HuggingFaceEmbedding(
    model_name="BAAI/bge-base-en-v1.5",
    device=device
)
```

(Розмістіть `import torch` разом з іншими імпортами вгорі файлу.)

- [ ] **Крок 2: Перевірка компіляції**

Запустіть: `cd /home/kali/p/semantic-search && python -m py_compile scripts/model_setup.py`
Очікувано: без виводу.

- [ ] **Крок 3: Коміт**

```bash
git add scripts/model_setup.py
git commit -m "feat: auto-detect cuda/cpu for the embedding model"
```

---

## Задача 4: `query_index.py` — прибрати pickle BM25, перебудувати BM25 з Qdrant

**Файли:**
- Змінити: `scripts/query_index.py`
- Тест: `tests/test_node_loader.py`

- [ ] **Крок 1: Написати тест, що падає, для завантажувача вузлів**

Створіть `tests/test_node_loader.py`:

```python
import types

import query_index


class FakeClient:
    """Returns successive (points, next_offset) tuples from scroll()."""

    def __init__(self, pages):
        self.pages = pages
        self.calls = 0

    def scroll(self, **kwargs):
        page = self.pages[self.calls]
        self.calls += 1
        return page


def _point(pid, text):
    # Payload without _node_content forces the metadata_dict_to_node fallback.
    return types.SimpleNamespace(id=pid, payload={"text": text})


def test_load_all_nodes_pages_and_preserves_ids():
    pages = [
        ([_point("a", "foo"), _point("b", "bar")], "offset-1"),
        ([_point("c", "baz")], None),
    ]
    client = FakeClient(pages)

    nodes = query_index.load_all_nodes(client, "col")

    assert [n.text for n in nodes] == ["foo", "bar", "baz"]
    assert [n.node_id for n in nodes] == ["a", "b", "c"]
    assert client.calls == 2
```

- [ ] **Крок 2: Запустити, щоб переконатися, що тест падає**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_node_loader.py -q`
Очікувано: FAIL — `AttributeError: module 'query_index' has no attribute 'load_all_nodes'`.

- [ ] **Крок 3: Прибрати механіку pickle BM25**

У `scripts/query_index.py`:

a) Видаліть константу шляху до кешу BM25. Приберіть цей рядок (залиште `EMB_CACHE_PATH`):

```python
BM25_CACHE_PATH = "./.claude/cache/bm25.pkl"
```

b) Видаліть глобальну змінну кешу вузлів BM25. Приберіть:

```python
bm25_retriever_nodes_cache = None
```

c) У `cache_warmup()` приберіть гілку BM25 та її глобальну змінну. Функція стає такою:

```python
def cache_warmup():
    global _embedding_cache
    if os.path.exists(EMB_CACHE_PATH):
        with open(EMB_CACHE_PATH, "rb") as f:
            print("[DEBUG] Loading EMB cache ...")
            _embedding_cache = pickle.load(f)
            print(f"[DEBUG] EMB cache size: {len(_embedding_cache)}")
```

- [ ] **Крок 4: Додати завантажувач вузлів та переписати `get_bm25_retriever`**

Додайте ці імпорти вгорі `scripts/query_index.py` (разом з іншими імпортами `llama_index`):

```python
from llama_index.core.schema import TextNode
from llama_index.core.vector_stores.utils import metadata_dict_to_node
```

Замініть всю функцію `get_bm25_retriever` на:

```python
def load_all_nodes(client, collection_name):
    """Reconstruct all nodes from the Qdrant collection (preserves node IDs)."""
    nodes = []
    offset = None
    while True:
        points, offset = client.scroll(
            collection_name=collection_name,
            with_payload=True,
            with_vectors=False,
            limit=256,
            offset=offset,
        )
        for p in points:
            payload = p.payload or {}
            try:
                node = metadata_dict_to_node(payload, text=payload.get("text"))
            except Exception:
                node = TextNode(id_=str(p.id), text=payload.get("text", ""))
            nodes.append(node)
        if offset is None:
            break
    return nodes


def get_bm25_retriever(index):
    # BM25 is in-memory only: rebuild it from the nodes stored in Qdrant each
    # time the engine is built (once per long-lived process).
    client = qdrant.get_Qdrant_client()
    nodes = load_all_nodes(client, qdrant.COLLECTION_NAME)
    print(f"[DEBUG] BM25 nodes loaded from Qdrant: {len(nodes)}")
    return BM25Retriever.from_defaults(nodes=nodes)
```

(`get_bm25_retriever` зберігає свій параметр `index` для сумісності з місцями виклику, але більше не читає `index.docstore`.)

- [ ] **Крок 5: Запустити тест завантажувача вузлів, щоб переконатися, що він проходить**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_node_loader.py -q`
Очікувано: PASS.

- [ ] **Крок 6: Підтвердити, що посилань на pickle BM25 не лишилося, та скомпілювати**

Запустіть: `cd /home/kali/p/semantic-search && grep -n "bm25" scripts/query_index.py; python -m py_compile scripts/query_index.py`
Очікувано: лишаються лише посилання `get_bm25_retriever` / `BM25Retriever` / на змінну-ретривер `bm25` (без `BM25_CACHE_PATH`, без `bm25_retriever_nodes_cache`, без `pickle.dump`/`pickle.load` для BM25); компіляція успішна.

- [ ] **Крок 7: Коміт**

```bash
git add scripts/query_index.py tests/test_node_loader.py
git commit -m "feat: rebuild BM25 in-memory from Qdrant, drop pickle cache"
```

---

## Задача 5: `build_index.py` — прибрати запис pickle вузлів BM25

**Файли:**
- Змінити: `scripts/build_index.py`

- [ ] **Крок 1: Прибрати блок запису pickle**

Видаліть запис кешу BM25, що йде після лога "Index successfully built":

```python
    # Persist nodes for BM25: load_index() rebuilds the index from the Qdrant
    # vector store, which leaves index.docstore.docs empty, so query_index's
    # BM25 retriever has no nodes to work with. Write them here for it to load.
    os.makedirs(os.path.dirname(BM25_CACHE_PATH), exist_ok=True)
    with open(BM25_CACHE_PATH, "wb") as f:
        pickle.dump(nodes, f)
    print(f"[DEBUG] [{datetime.now()}] ✅ Wrote {len(nodes)} nodes to {BM25_CACHE_PATH}")
```

- [ ] **Крок 2: Прибрати тепер невикористовувану константу та імпорт**

Видаліть блок константи:

```python
# Must match query_index.BM25_CACHE_PATH. Duplicated rather than imported:
# importing query_index here would trigger its import-time side effects
# (cache warmup + a Qdrant connection).
BM25_CACHE_PATH = "./.claude/cache/bm25.pkl"
```

і приберіть `import pickle` (він більше не використовується в цьому файлі).

- [ ] **Крок 3: Підтвердити, що pickle прибрано, та скомпілювати**

Запустіть: `cd /home/kali/p/semantic-search && grep -n "pickle\|BM25_CACHE_PATH" scripts/build_index.py; python -m py_compile scripts/build_index.py`
Очікувано: збігів немає; компіляція успішна.

- [ ] **Крок 4: Коміт**

```bash
git add scripts/build_index.py
git commit -m "refactor: drop BM25 node pickle write from build_index"
```

---

## Задача 6: `service.py` — сервіс пошуку Flask, що тримає прогрітий рушій

**Файли:**
- Створити: `scripts/service.py`
- Тест: `tests/test_service.py`

- [ ] **Крок 1: Написати тести, що падають**

Створіть `tests/test_service.py`:

```python
import service


def test_health_ok():
    client = service.app.test_client()
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.get_json()["status"] == "ok"


def test_search_missing_query_is_400():
    client = service.app.test_client()
    resp = client.post("/search", json={})
    assert resp.status_code == 400
    assert "error" in resp.get_json()


def test_search_returns_query_result(monkeypatch):
    monkeypatch.setattr(service, "query", lambda q: {"sources": ["x.cs"], "context": []})
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "where is auth"})
    assert resp.status_code == 200
    assert resp.get_json()["sources"] == ["x.cs"]


def test_search_error_is_500(monkeypatch):
    def boom(q):
        raise RuntimeError("kaboom")
    monkeypatch.setattr(service, "query", boom)
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "x"})
    assert resp.status_code == 500
    assert "kaboom" in resp.get_json()["error"]
```

- [ ] **Крок 2: Запустити, щоб переконатися, що тести падають**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_service.py -q`
Очікувано: FAIL — `ModuleNotFoundError: No module named 'service'`.

- [ ] **Крок 3: Створити `scripts/service.py`**

```python
import os

from flask import Flask, request, jsonify

from query_index import query, get_engine

app = Flask(__name__)


@app.route("/health")
def health():
    return jsonify({"status": "ok"})


@app.route("/search", methods=["POST"])
def search():
    data = request.get_json(silent=True) or {}
    q = data.get("query")
    if not q:
        return jsonify({"error": "missing 'query'"}), 400
    try:
        return jsonify(query(q))
    except Exception as e:
        return jsonify({"error": str(e)}), 500


def main():
    # Configure the embedding model + LLM, then warm the retrievers once so the
    # first real request does not pay the build cost.
    import model_setup  # noqa: F401  (import side effect: configures Settings)
    get_engine()
    app.run(host="0.0.0.0", port=int(os.getenv("PORT", "8000")))


if __name__ == "__main__":
    main()
```

(`model_setup` та `get_engine()` відкладені всередину `main()`, щоб імпорт застосунку для тестів не вантажив модель ембедингів і не під'єднувався до Qdrant.)

- [ ] **Крок 4: Запустити тести, щоб переконатися, що вони проходять**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_service.py -q`
Очікувано: PASS (4 тести).

- [ ] **Крок 5: Коміт**

```bash
git add scripts/service.py tests/test_service.py
git commit -m "feat: add Flask search service holding the warm engine"
```

---

## Задача 7: `mcp_server.py` — перенаправлення до HTTP-сервісу

**Файли:**
- Змінити: `scripts/mcp_server.py`
- Тест: `tests/test_mcp_server.py`

- [ ] **Крок 1: Написати тести, що падають**

Створіть `tests/test_mcp_server.py`:

```python
import mcp_server


def test_search_code_unreachable(monkeypatch):
    def boom(*a, **k):
        raise mcp_server.requests.RequestException("no route")
    monkeypatch.setattr(mcp_server.requests, "post", boom)

    out = mcp_server.search_code("hi")
    assert "error" in out
    assert "unreachable" in out["error"]


def test_search_code_success(monkeypatch):
    class Resp:
        status_code = 200
        def json(self):
            return {"sources": ["a.cs"]}
    monkeypatch.setattr(mcp_server.requests, "post", lambda *a, **k: Resp())

    assert mcp_server.search_code("hi") == {"sources": ["a.cs"]}


def test_search_code_non_200(monkeypatch):
    class Resp:
        status_code = 500
        text = "boom"
        def json(self):
            return {}
    monkeypatch.setattr(mcp_server.requests, "post", lambda *a, **k: Resp())

    out = mcp_server.search_code("hi")
    assert out["error"].startswith("search service returned 500")
```

- [ ] **Крок 2: Запустити, щоб переконатися, що тести падають**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_mcp_server.py -q`
Очікувано: FAIL — `AttributeError: module 'mcp_server' has no attribute 'requests'` (або помилка імпорту, що посилається на стару версію з subprocess).

- [ ] **Крок 3: Переписати `scripts/mcp_server.py`**

```python
import json
import os
import sys

import requests

SEARCH_SERVICE_URL = os.getenv("SEARCH_SERVICE_URL", "http://localhost:8000")


def search_code(query):
    try:
        resp = requests.post(
            f"{SEARCH_SERVICE_URL}/search",
            json={"query": query},
            timeout=120,
        )
    except requests.RequestException as e:
        return {"error": f"search service unreachable: {e}"}

    if resp.status_code != 200:
        return {"error": f"search service returned {resp.status_code}", "body": resp.text}

    try:
        return resp.json()
    except ValueError:
        return {"error": "invalid JSON from search service", "body": resp.text}


def main():
    while True:
        try:
            line = input()
        except EOFError:
            break

        try:
            request = json.loads(line)
        except json.JSONDecodeError:
            print(json.dumps({"error": "invalid JSON request"}), flush=True)
            continue

        if request.get("tool") == "search_codebase":
            response = {"output": search_code(request["input"]["query"])}
        else:
            response = {"error": f"unknown tool: {request.get('tool')}"}

        print(json.dumps(response), flush=True)


if __name__ == "__main__":
    main()
```

- [ ] **Крок 4: Запустити модульні тести, щоб переконатися, що вони проходять**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest tests/test_mcp_server.py -q`
Очікувано: PASS (3 тести).

- [ ] **Крок 5: Димовий тест циклу stdin наскрізно**

Запустіть:
```bash
cd /home/kali/p/semantic-search && printf '%s\n' \
  'not json' \
  '{"tool":"bogus"}' \
  '{"tool":"search_codebase","input":{"query":"x"}}' \
  | SEARCH_SERVICE_URL=http://127.0.0.1:1 python scripts/mcp_server.py
```
Очікувано: три рядки JSON — помилка некоректного JSON, помилка невідомого інструмента та `{"output": {"error": "search service unreachable: ..."}}` (порт 1 закритий). Без трасування; чистий вихід по EOF.

- [ ] **Крок 6: Коміт**

```bash
git add scripts/mcp_server.py tests/test_mcp_server.py
git commit -m "feat: forward MCP search_codebase to the HTTP search service"
```

---

## Задача 8: Пакування в Docker

**Файли:**
- Створити: `Dockerfile`
- Створити: `.dockerignore`
- Змінити: `scripts/docker-compose.yml`

- [ ] **Крок 1: Створити `.dockerignore`**

```text
.git
.venv
__pycache__
*.pyc
cache
.claude
docs
tests
scripts.zip
```

- [ ] **Крок 2: Створити `Dockerfile`**

```dockerfile
FROM python:3.11-slim

# Cache the HuggingFace model under a mounted volume to avoid re-downloading.
ENV PYTHONUNBUFFERED=1 \
    HF_HOME=/models

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY scripts/ ./scripts/

WORKDIR /app/scripts
CMD ["python", "service.py"]
```

(GPU вмикається за бажанням: змініть базовий образ на Python-образ `nvidia/cuda` і встановіть CUDA-збірку torch; код автовизначення в `model_setup.py` тоді використовуватиме GPU, коли хост надає його через `--gpus`.)

- [ ] **Крок 3: Додати `search-service` до `scripts/docker-compose.yml`**

Додайте цей сервіс під `services:` (поруч із `qdrant`):

```yaml
  search-service:
    build:
      context: ..
      dockerfile: Dockerfile
    container_name: search-service
    depends_on:
      - qdrant
    ports:
      - "8000:8000"
    environment:
      - QDRANT_HOST=qdrant
      - QDRANT_PORT=6333
      - QDRANT_AUTOSTART=0
      - HF_HOME=/models
    volumes:
      - hf_models:/models
    restart: unless-stopped
```

І додайте іменований том під наявним блоком `volumes:`:

```yaml
  hf_models:
```

- [ ] **Крок 4: Валідувати файл compose**

Запустіть: `cd /home/kali/p/semantic-search/scripts && docker compose -f docker-compose.yml config >/dev/null && echo OK`
Очікувано: `OK` (синтаксис compose валідний). Якщо Docker недоступний у цьому середовищі, пропустіть і валідуйте на цільовій машині.

- [ ] **Крок 5: Коміт**

```bash
git add Dockerfile .dockerignore scripts/docker-compose.yml
git commit -m "feat: containerize the search service via docker-compose"
```

---

## Задача 9: Фінальна перевірка

**Файли:** немає (лише перевірка)

- [ ] **Крок 1: Скомпілювати кожен скрипт**

Запустіть: `cd /home/kali/p/semantic-search && python -m py_compile scripts/*.py`
Очікувано: без виводу.

- [ ] **Крок 2: Запустити повний набір тестів**

Запустіть: `cd /home/kali/p/semantic-search && python -m pytest -q`
Очікувано: усі тести проходять (завантажувач вузлів, service, mcp_server). Потрібні встановлені `llama-index-core`, `qdrant-client`, `flask`, `requests`, `pytest`.

- [ ] **Крок 3: Чек-лист часу виконання (цільова машина з Docker + Qdrant + залежностями)**

Задокументуйте та виконайте:
1. `cd scripts && docker compose up -d qdrant`
2. З кореня проєкту `python scripts/build_index.py`, щоб заповнити колекцію.
3. `cd scripts && docker compose up -d --build search-service`
4. `curl -s localhost:8000/health` → `{"status":"ok"}`.
5. `curl -s -XPOST localhost:8000/search -H 'Content-Type: application/json' -d '{"query":"where is authentication handled"}'` → ранжований JSON; підтвердьте, що з'являються збіги лише за ключовими словами (BM25 побудований з Qdrant, у пам'яті).
6. Прокрутіть `mcp_server.py` із піднятим сервісом:
   `printf '%s\n' '{"tool":"search_codebase","input":{"query":"db context"}}' | python scripts/mcp_server.py` → один рядок JSON `{"output": {...}}`.
7. Перезапустіть `search-service`; підтвердьте, що модель ембедингів завантажується з тому `hf_models` (без повторного завантаження) і рушій будується один раз при запуску, а не на кожен запит.

- [ ] **Крок 4: Фінальний коміт (якщо оновлено якісь документи/нотатки)**

```bash
git add -A
git commit -m "docs: continuous search service verification notes" || true
```

---

## Нотатки самоперевірки (для автора)

- **Покриття специфікації:** прибирання pickle (Задача 4/5), BM25 у пам'яті з Qdrant (Задача 4), прогрітий сервіс-сінглтон (Задача 6), HTTP-шим MCP (Задача 7), автопристрій (Задача 3), Qdrant із env + гейт автозапуску (Задача 2), Docker + compose + requirements (Задача 1/8). Усі розділи специфікації відображено.
- **Відкладено (за специфікацією):** R2/R4/R5, ендпойнт `/ask` сервісу, побудова індексу всередині сервісу — навмисно не входять до жодної задачі.
- **Узгодженість типів/назв:** `load_all_nodes(client, collection_name)`, `get_bm25_retriever(index)`, `search_code(query)`, `SEARCH_SERVICE_URL`, `service.app`, `service.query` використовуються узгоджено в усіх задачах і тестах.
