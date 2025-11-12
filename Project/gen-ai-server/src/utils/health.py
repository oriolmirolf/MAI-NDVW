import ollama

def check_ollama(model: str = "llama2") -> bool:
    try:
        response = ollama.list()
        models = [m.get("name", m.get("model", "")) for m in response.get("models", [])]

        if not models:
            print("WARNING: No models found in Ollama")
            print(f"Run: ollama pull {model}")
            return False

        if not any(model in m for m in models):
            print(f"WARNING: Model '{model}' not found in Ollama")
            print(f"Available models: {', '.join(models)}")
            print(f"Run: ollama pull {model}")
            return False

        print(f"Ollama connected - using model '{model}'")
        return True
    except Exception as e:
        print(f"ERROR: Cannot connect to Ollama - {e}")
        print("Make sure Ollama is running (ollama serve)")
        return False
