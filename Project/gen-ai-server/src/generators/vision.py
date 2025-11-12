from transformers import AutoTokenizer, AutoModelForCausalLM
import torch
from PIL import Image
import json
import re
from pydantic import ValidationError
from src.models.responses import VisionResponse

class VisionGenerator:
    def __init__(self):
        self.model_name = "vikhyatk/moondream2"
        self.device = "cuda" if torch.cuda.is_available() else "cpu"

        print(f"[VISION] Loading moondream2 model on {self.device}...")
        self.tokenizer = AutoTokenizer.from_pretrained(
            self.model_name,
            trust_remote_code=True
        )
        self.model = AutoModelForCausalLM.from_pretrained(
            self.model_name,
            torch_dtype=torch.float16,
            trust_remote_code=True
        ).to(self.device)
        self.model.eval()
        print("[VISION] Model loaded successfully (~2GB VRAM)")

    async def describe_room(self, image_path: str) -> dict:
        print(f"[VISION] Analyzing image: {image_path}")
        image = Image.open(image_path).convert("RGB")

        prompt = """Analyze this 2D roguelike RPG game screenshot (top-down pixel art).

Provide a detailed description including:
- environment_type: what kind of area this is (1-2 words)
- atmosphere: describe the overall feeling and visual mood (2-3 sentences)
- features: list 5-7 specific things you see (terrain, structures, objects, enemies)
- mood: describe the emotional tone for ambient music generation (1-2 sentences)

Respond ONLY in this JSON format:
{"environment_type": "...", "atmosphere": "...", "features": ["...", "...", "...", "...", "..."], "mood": "..."}"""

        print("[VISION] Running inference...")
        with torch.no_grad():
            response = self.model.answer_question(
                self.model.encode_image(image),
                prompt,
                self.tokenizer
            )

        print(f"[VISION] Raw response: {response[:200]}...")

        json_match = re.search(r'\{.*\}', response, re.DOTALL)
        if json_match:
            try:
                data = json.loads(json_match.group(0))
                # Validate with Pydantic
                validated = VisionResponse(**data)
                print(f"[VISION] ✓ Validated: {validated.environment_type}")
                return validated.model_dump()
            except json.JSONDecodeError as e:
                print(f"[VISION] JSON parse error: {e}")
            except ValidationError as e:
                print(f"[VISION] Validation error: {e}")
                print(f"[VISION] Raw data: {data}")

        print("[VISION] Using fallback response")
        fallback = VisionResponse(
            environment_type="outdoor game area",
            atmosphere="colorful pixel art environment with diverse terrain and natural features",
            features=["grass areas", "water bodies", "pathways", "structures", "vegetation"],
            mood="adventurous exploration ambience with peaceful undertones"
        )
        return fallback.model_dump()
