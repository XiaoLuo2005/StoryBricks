import os
import time
from typing import Any, Dict, Optional

import requests
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


DASHSCOPE_API_BASE = "https://dashscope.aliyuncs.com/api/v1"
TEXT2IMAGE_URL = f"{DASHSCOPE_API_BASE}/services/aigc/text2image/image-synthesis"
TASK_URL_PREFIX = f"{DASHSCOPE_API_BASE}/tasks"
WANX_V1_ALLOWED_SIZES = {"1024*1024", "720*1280", "1280*720", "768*1152"}


class GenerateRequest(BaseModel):
    prompt: str = Field(..., min_length=1)
    model: str = "wanx-v1"
    size: str = "1024*1024"
    n: int = 1
    poll_interval_sec: float = 1.5
    timeout_sec: float = 90.0


class GenerateResponse(BaseModel):
    task_id: str
    image_url: str
    model: str


app = FastAPI(title="Local DashScope Bridge")


def _normalize_size(size: str) -> str:
    normalized = size.lower().replace("x", "*").replace(" ", "")
    parts = normalized.split("*")
    if len(parts) != 2:
        return "1024*1024"

    try:
        width = int(parts[0])
        height = int(parts[1])
    except ValueError:
        return "1024*1024"

    # wanx image generation requires width/height divisible by 8.
    width = max(8, (width // 8) * 8)
    height = max(8, (height // 8) * 8)
    return f"{width}*{height}"


def _normalize_size_for_model(model: str, size: str) -> str:
    base_size = _normalize_size(size)
    model_name = (model or "").strip().lower()

    # wanx-v1 only accepts a fixed size whitelist.
    if model_name == "wanx-v1":
        if base_size in WANX_V1_ALLOWED_SIZES:
            return base_size
        return "1024*1024"

    return base_size


def _require_api_key() -> str:
    api_key = os.getenv("DASHSCOPE_API_KEY", "").strip()
    if not api_key:
        raise HTTPException(
            status_code=500,
            detail="Missing DASHSCOPE_API_KEY environment variable.",
        )
    return api_key


def _submit_task(api_key: str, req: GenerateRequest) -> str:
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "X-DashScope-Async": "enable",
    }
    normalized_size = _normalize_size_for_model(req.model, req.size)
    if normalized_size != req.size:
        print(f"[DashScope] Size adjusted: {req.size} -> {normalized_size}")

    payload: Dict[str, Any] = {
        "model": req.model,
        "input": {"prompt": req.prompt},
        "parameters": {"size": normalized_size, "n": req.n},
    }
    response = requests.post(TEXT2IMAGE_URL, json=payload, headers=headers, timeout=30)
    if response.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"Submit failed ({response.status_code}): {response.text}",
        )

    body = response.json()
    task_id: Optional[str] = body.get("output", {}).get("task_id")
    if not task_id:
        raise HTTPException(status_code=502, detail=f"No task_id in response: {body}")
    return task_id


def _poll_image_url(api_key: str, task_id: str, interval: float, timeout: float) -> str:
    headers = {"Authorization": f"Bearer {api_key}"}
    deadline = time.time() + timeout
    task_url = f"{TASK_URL_PREFIX}/{task_id}"

    while time.time() < deadline:
        response = requests.get(task_url, headers=headers, timeout=30)
        if response.status_code >= 400:
            raise HTTPException(
                status_code=502,
                detail=f"Task polling failed ({response.status_code}): {response.text}",
            )

        body = response.json()
        output = body.get("output", {})
        status = output.get("task_status", "")

        if status == "SUCCEEDED":
            results = output.get("results") or []
            if not results:
                raise HTTPException(
                    status_code=502,
                    detail=f"Task succeeded but no results: {body}",
                )
            image_url = results[0].get("url")
            if not image_url:
                raise HTTPException(
                    status_code=502,
                    detail=f"Task succeeded but result url missing: {body}",
                )
            return image_url

        if status == "FAILED":
            raise HTTPException(status_code=502, detail=f"Task failed: {body}")

        time.sleep(interval)

    raise HTTPException(status_code=504, detail=f"Task timeout: {task_id}")


@app.get("/health")
def health() -> Dict[str, str]:
    return {"status": "ok"}


@app.post("/generate", response_model=GenerateResponse)
def generate_image(req: GenerateRequest) -> GenerateResponse:
    api_key = _require_api_key()
    task_id = _submit_task(api_key, req)
    image_url = _poll_image_url(
        api_key, task_id, req.poll_interval_sec, req.timeout_sec
    )
    return GenerateResponse(task_id=task_id, image_url=image_url, model=req.model)


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("server:app", host="127.0.0.1", port=8000, reload=False)
