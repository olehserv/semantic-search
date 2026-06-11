# Container for the long-running search service (scripts/service.py).
# CPU by default. For GPU: switch to an nvidia/cuda Python base image and a
# CUDA torch wheel; model_setup.py auto-detects the device at runtime.
FROM python:3.11-slim

# HF_HOME points at a mounted volume so the embedding model is downloaded
# only once, not on every container start.
ENV PYTHONUNBUFFERED=1 \
    HF_HOME=/models

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY scripts/ ./scripts/

WORKDIR /app/scripts
CMD ["python", "service.py"]
