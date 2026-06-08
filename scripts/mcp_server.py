import subprocess
import json

def search_code(query):
    result = subprocess.run(
        ["python", ".claude/scripts/query_index.py", query],
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        return {"error": "query_index failed", "stderr": result.stderr}
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        return {"error": "invalid JSON from query_index", "stdout": result.stdout}

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
            query = request["input"]["query"]
            response = {"output": search_code(query)}
        else:
            response = {"error": f"unknown tool: {request.get('tool')}"}

        print(json.dumps(response), flush=True)

if __name__ == "__main__":
    main()
