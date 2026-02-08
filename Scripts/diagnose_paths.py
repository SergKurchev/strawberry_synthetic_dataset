import os
import glob
from pathlib import Path

def print_tree(start_path, max_depth=2):
    print(f"Directory structure of '{start_path}':")
    start_path = Path(start_path)
    if not start_path.exists():
        print("  <Path does not exist>")
        return

    for root, dirs, files in os.walk(start_path):
        level = len(Path(root).relative_to(start_path).parts)
        if level > max_depth:
            continue
        indent = "  " * level
        print(f"{indent}{os.path.basename(root)}/")
        sub_indent = "  " * (level + 1)
        for f in files[:5]: # Show first 5 files
            print(f"{sub_indent}{f}")
        if len(files) > 5:
            print(f"{sub_indent}... ({len(files)-5} more)")

print("=== DIAGNOSIS START ===")
print_tree(".")

print("\nSearching for potentially nested 'depth_metadata.json' or '*_meta.json':")
for root, dirs, files in os.walk("."):
    for f in files:
        if f == "depth_metadata.json" or f.endswith("_meta.json"):
             print(f"FOUND: {os.path.join(root, f)}")

print("=== DIAGNOSIS END ===")
