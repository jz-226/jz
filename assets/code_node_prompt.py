def main(llm_text: str) -> dict:
    text = llm_text
    ingredient = ""
    cost = 0
    
    if "===META===" in text:
        meta = text.split("===META===")[1].split("===END===")[0]
        for line in meta.strip().split("\n"):
            if "主料" in line:
                ingredient = line.split("：")[1].strip()
            if "总价" in line:
                try:
                    cost = int(line.split("：")[1].strip().replace("元",""))
                except:
                    cost = 0
        user_text = text.split("===META===")[0].strip()
    else:
        user_text = text
    
    return {
        "user_text": user_text,
        "ingredient": ingredient,
        "cost": cost
    }
