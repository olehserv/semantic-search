*🇺🇦 Це український переклад. Оригінал англійською: `README.md`.*

# 🔍 Семантичний пошук по коду

Став питання своїй .NET-кодовій базі простою англійською — і отримуй потрібні файли у відповідь. 🎯

Він поєднує два види пошуку, тож ти отримуєш найкраще з обох:

- 🧠 **Семантичний пошук** — знаходить код за *змістом* (за допомогою векторів), тож "де ми перевіряємо пароль?" збігається з `LoginService` навіть без цих точних слів.
- 🔑 **Пошук за ключовими словами (BM25)** — знаходить *точні* збіги, як-от назву класу чи методу.

AI-агент (як-от **Claude Code**) може використовувати його як інструмент, щоб досліджувати твій код за тебе. Вектори зберігаються у базі даних [Qdrant](https://qdrant.tech/). 🗂️

> 🆕 Новачок у тутешніх ідеях (ембединги, косинусна подібність, BM25, RRF, переранжування)?
> Почни з [`docs/CONCEPTS.md`](docs/CONCEPTS.md) — там кожна з них пояснена простою англійською з аналогіями.

---

## 📦 Що тобі потрібно

| | |
|---|---|
| 🐍 **Python 3.11+** | `pip install -r requirements.txt` (використовуй `requirements-dev.txt` також для тестів/лінту) |
| 🐳 **Docker** | Qdrant запускає себе сам через `docker compose`. Немає плагіна compose? Запусти його вручну (див. нижче). |
| ⚡ **GPU** | Опційно — використовує CUDA, якщо доступний, інакше CPU. Повністю автоматично. |
| 💬 **[Ollama](https://ollama.com/)** | Тільки для `ask.py` (функція текстової відповіді). Модель за замовчуванням: `llama3`. |

Немає плагіна compose? Запусти Qdrant напряму:

```bash
docker run -d --name qdrant-local -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant
```

---

## 🚀 Швидкий старт

Скопіюй скрипти у свій проєкт під `.claude/scripts/`, потім запускай їх **з кореневої папки твого проєкту** (шляхи на кшталт `./.claude/cache/...` відносні до того місця, звідки ти їх запускаєш).

### 1️⃣ Побудуй індекс

Перетвори свій код на вектори, придатні для пошуку. Запусти це один раз для початку, і ще раз, коли код зміниться.

```bash
python .claude/scripts/build_index.py              # asks before replacing an existing index
python .claude/scripts/build_index.py --force      # replace without asking (good for scripts/CI)
python .claude/scripts/build_index.py --incremental # only re-do changed / new / deleted files (fast)
```

> 🔁 **Повна побудова проти `--incremental`** — Повна побудова є безпечним варіантом за замовчуванням: вона будує цілком новий індекс і підставляє його, лише коли завершиться, тож збій ніколи не зруйнує твій робочий індекс. `--incremental` набагато швидший для малих змін — він повторно ембедить лише ті файли, яких ти торкнувся, і прибирає файли, які ти видалив. Використовуй повну побудову після великих змін; використовуй `--incremental` щодня. (Якщо інкрементний запуск перервано, просто запусти його знову.)

### 2️⃣ Запусти сервіс пошуку

Завантажує модель **один раз** і лишається прогрітим, тож кожен пошук після першого є швидким (значно менше за секунду). ⚡

```bash
python .claude/scripts/service.py        # serves on http://localhost:8000
```

### 3️⃣ Пошук 🔎

```bash
curl -XPOST localhost:8000/search -H 'Content-Type: application/json' \
     -d '{"query":"where is authentication handled"}'
```

Ти отримуєш назад ранжовані результати у форматі JSON — `sources` (файли, що збіглися) і `context` (кожен фрагмент із його оцінкою та рангом):

```json
{
  "answer": "Top 8 relevant code snippets retrieved. See context for details.",
  "sources": ["LoginService.cs", "LoginUserCommand.cs"],
  "context": [
    {
      "rank": 1,
      "file": "LoginService.cs",
      "path": "src/Auth/LoginService.cs",
      "score": 0.873,
      "text": "public bool VerifyPassword(string user, string pw) { ... }"
    }
  ]
}
```

(Поле `text` — це фрагмент коду, обрізаний до ~800 символів.)

> 💡 Сервіс не запущений? Разовий пошук усе одно працює — він просто щоразу платить за повільний холодний старт:
> ```bash
> python .claude/scripts/query_index.py "where is authentication handled"
> ```

### 4️⃣ (Опційно) Попроси текстову відповідь 💬

Надсилає найкращі фрагменти до локальної LLM (Ollama) і пише відповідь словами.

```bash
python .claude/scripts/ask.py "how does the request pipeline work"
```

---

## 🤖 Використання з Claude Code (MCP)

Цей репозиторій постачає `.mcp.json`, який реєструє MCP-сервер `code-search`. Із запущеним сервісом (крок 2 вище) Claude Code може викликати інструмент **`search_codebase`** напряму. Якщо сервіс не працює, інструмент відповідає чіткою помилкою, яка пояснює, як його запустити. ✅

Щоб використати його в **іншому проєкті**, скопіюй скрипти до `.claude/scripts/` і додай це до `.mcp.json` того проєкту:

```json
{
  "mcpServers": {
    "code-search": {
      "command": "python3",
      "args": [".claude/scripts/mcp_server.py"]
    }
  }
}
```

Цьому `command` потрібен лише Python зі встановленими `mcp` та `requests` — прокладка MCP легка (без torch, без llama-index). 🪶

---

## 🛠️ Як це працює (під капотом)

```text
  📥 INDEX  (run once, and again when code changes)
     .cs / .csproj / .sln ──► build_index.py ──► 🗂️  Qdrant
                                                  (vectors + chunk text)

  🔎 SEARCH  (every question)
     Claude Code ─MCP─► mcp_server.py ─HTTP─► service.py ──► 🗂️  Qdrant
                                              (warm engine:       │
                                               query_index +      ▼
                                               ranking)     🎯 ranked JSON

  🗣️ ASK  (optional)
     ask.py ──► search ──► 💬 Ollama ──► written answer
```

1. **📥 Індекс** (`build_index.py`) — читає файли `.cs`/`.csproj`/`.sln`/`.slnx`, ріже їх на фрагменти, перетворює кожен фрагмент на вектор за допомогою локальної моделі HuggingFace і зберігає вектори (плюс текст) у Qdrant.
2. **🔎 Пошук + ранжування** (`query_index.py`) — виконує три пошуки (вектор top-6, BM25, вектор top-12) по кількох варіантах твого питання, прибирає дублікати і дає кожному результату комбіновану оцінку (`ranking.py`). BM25 перебудовується в пам'яті з Qdrant.
3. **🔥 Обслуговування** (`service.py`) — довготривалий сервіс Flask, який будує рушій **один раз** і відповідає на кожен `POST /search` з прогрітого рушія.
4. **🤝 Доступ для агента** (`mcp_server.py`) — справжній MCP-сервер (офіційний `mcp` SDK, stdio), який дає агентам інструмент `search_codebase` і пересилає кожен виклик до сервісу.
5. **🗣️ Відповідь** (`ask.py`) — надсилає найкращі фрагменти до локальної LLM і повертає текстову відповідь, обрізану до бюджету токенів (`MAX_CONTEXT_TOKENS`).

| Файл | Роль |
|------|------|
| `scripts/build_index.py` | Побудувати/перебудувати індекс (`--force` = без запиту; `--incremental` = тільки змінені файли) |
| `scripts/query_index.py` | Гібридний пошук + ранжування; CLI друкує JSON |
| `scripts/ranking.py` | Математика оцінювання (чистий numpy, покрита юніт-тестами) |
| `scripts/service.py` | Довготривалий сервіс пошуку з прогрітим рушієм |
| `scripts/mcp_server.py` | MCP-сервер (stdio) — надає агентам `search_codebase` |
| `scripts/ask.py` | Текстові LLM-відповіді зі знайденого коду |
| `scripts/qdrant.py` | Підключення до Qdrant + запуск контейнера |
| `scripts/model_setup.py` | Завантажує модель ембедингів і LLM (ліниво) |
| `eval/` | Вимірювання якості пошуку (див. `eval/README.md`) |

---

## 🔧 Конфігурація

Усі налаштування живуть в одному типізованому місці — `scripts/config.py` (pydantic-settings) — і беруться зі змінних середовища. Значення за замовчуванням нижче — це те, що постачається, тож воно працює одразу з коробки. 👇

| Налаштування | Змінна середовища | За замовчуванням |
|---------|---------|---------|
| Хост Qdrant | `QDRANT_HOST` | `localhost` |
| Порт Qdrant | `QDRANT_PORT` | `6333` |
| Колекція Qdrant | `QDRANT_COLLECTION` | `demo` |
| Автозапуск Qdrant (Docker) | `QDRANT_AUTOSTART` | `1` (увімкнено) |
| API-ключ Qdrant | `QDRANT_API_KEY` | `""` (вимкнено; встанови його для захищеного/віддаленого Qdrant) |
| HTTPS для Qdrant | `QDRANT_HTTPS` | `0` (встанови `1` для TLS/віддаленого Qdrant, напр. Qdrant Cloud) |
| Модель ембедингів | `EMBED_MODEL` | `BAAI/bge-base-en-v1.5` |
| Модель LLM (ask.py) | `LLM_MODEL` | `llama3` |
| Шлях до кешу ембедингів | `EMB_CACHE_PATH` | `./.claude/cache/embeddings.db` |
| Відсічення кандидатів для переранжування | `RERANK_CANDIDATES` | `30` |
| Розмір кешу результатів ретривера | `RETRIEVE_CACHE_SIZE` | `256` (обмеження LRU на прогрітий кеш по запитах) |
| Cross-encoder переранжувальник | `CROSS_ENCODER_MODEL` | `""` (вимкнено) |
| Порт сервісу | `PORT` | `8000` |
| Максимальний розмір тіла `/search` | `MAX_CONTENT_LENGTH` | `65536` байтів (більше → HTTP 413) |
| Бюджет контексту ask.py | `MAX_CONTEXT_TOKENS` | `4000` (токенів коду, надісланих до LLM) |
| Варіанти запиту | `QUERY_VARIANT_SUFFIXES` | `implementation` (через кому; додай доменні терміни, напр. `implementation,.NET core backend`) |
| Рівень логування | `LOG_LEVEL` | `INFO` (встанови `DEBUG` для покрокових трасувань; логи йдуть у stderr) |

Кілька речей, що живуть у коді, а не в змінних середовища:

| Річ | Де |
|-------|-------|
| Пристрій для ембедингів (CPU/GPU авто) | `model_setup.py` |
| Які типи файлів індексуються | `build_index.py` (`load_documents`) |
| Розбиття на фрагменти | `chunking.py` (C# ріжеться на межах типів/методів через tree-sitter; інші файли використовують `SentenceSplitter`) |
| Глибина пошуку, ваги оцінок | `query_index.py` / `ranking.py` |

---

## 💡 Корисно знати

- 🏗️ **Побудуй перед тим, як шукати.** Якщо колекція Qdrant порожня, пошук зупиняється з чітким повідомленням "спершу запусти `build_index.py`".
- 🐳 **Запускай це в Docker.** `Dockerfile` + запис `search-service` у `scripts/docker-compose.yml` запускають сервіс у контейнері. Дві кодові бази можуть ділити один Qdrant, використовуючи різні назви `QDRANT_COLLECTION`.
- 🧠 **Кеш ембедингів безпечно зберігати.** Ембединги часу запиту кешуються в `./.claude/cache/embeddings.db` (SQLite), за ключем `(назва моделі, текст)`. Зміни модель ембедингів — і він просто перерахує заново — без застарілих векторів, без ручного прибирання.
- 📋 **Статус проєкту та історія.** Код-рев'ю, рішення та дорожня карта є в `docs/reviews/`; живий стан роботи є в [`handoff.md`](handoff.md).
