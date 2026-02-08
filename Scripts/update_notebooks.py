import json
import os
from pathlib import Path

# The robust setup logic (without imports, which we will merge)
ROBUST_SETUP_LOGIC = r'''
# --- Robust Dataset Configuration ---
VERSION_TAG = "Dataset"
BASE_URL = f"https://github.com/SergKurchev/strawberry_synthetic_dataset/releases/download/{VERSION_TAG}"
FILES_TO_DOWNLOAD = [
    "strawberry_dataset.zip.001",
    "strawberry_dataset.zip.002",
    "strawberry_dataset.zip.003"
]
OUTPUT_ZIP = "strawberry_dataset.zip"

def reconstruct_metadata(dataset_root):
    """Reconstructs depth_metadata.json from individual files in metadata_temp/"""
    print("⚠️ 'depth_metadata.json' not found. Attempting reconstruction from 'metadata_temp/'...")
    temp_dir = dataset_root / "metadata_temp"
    if not temp_dir.exists():
        print(f"❌ metadata_temp directory not found at {temp_dir}")
        return False

    combined_metadata = {}
    json_files = list(temp_dir.glob("*_meta.json"))
    print(f"  Found {len(json_files)} metadata chunks.")
    
    for json_file in tqdm(json_files, desc="Reconstructing Metadata"):
        try:
            # Filename format: 00001_meta.json -> corresponds to 00001.png
            # We assume the content of the json is the metadata dict for that image
            img_id = json_file.name.replace("_meta.json", "")
            img_name = f"{img_id}.png"
            
            with open(json_file, 'r') as f:
                data = json.load(f)
                combined_metadata[img_name] = data
        except Exception as e:
            print(f"  Warning: Failed to read {json_file}: {e}")

    if not combined_metadata:
        print("❌ Failed to reconstruct any metadata.")
        return False

    target_path = dataset_root / "depth_metadata.json"
    print(f"💾 Saving reconstructed metadata to {target_path}...")
    with open(target_path, 'w') as f:
        json.dump(combined_metadata, f, indent=2)
        
    return True

def setup_dataset():
    # 1. Search for existing dataset
    print("🔍 Searching for existing dataset...")
    
    # Helper to validate a root candidate
    def validate_root(p):
        if (p / "depth_metadata.json").exists():
            return True
        if (p / "metadata_temp").exists():
            # Try to fix it
            return reconstruct_metadata(p)
        return False

    # Recursive search in current dir
    for root, dirs, files in os.walk(".", topdown=True):
        p = Path(root)
        # Start optimization: don't go too deep or into hidden dirs
        if ".git" in p.parts or "temp_download" in p.parts:
            continue
            
        if "images" in dirs and ("depth_metadata.json" in files or "metadata_temp" in dirs):
            if validate_root(p):
                print(f"✅ Dataset found/Fixed at: {p}")
                return p

    # Check standard paths
    search_paths = [
        Path("strawberry_dataset"),
        Path("dataset/strawberry_dataset"),
        Path("/kaggle/input/last-straw-dataset/strawberry_dataset"),
        Path("/kaggle/input/strawberry_synthetic_dataset/strawberry_dataset")
    ]
    for p in search_paths:
        if p.exists():
            if validate_root(p):
                print(f"✅ Dataset found/Fixed at: {p}")
                return p

    print("⬇️ Dataset not found. Downloading from GitHub Releases...")
    
    # 2. Prepare Download Directory
    if os.path.exists("temp_download"):
        shutil.rmtree("temp_download")
    os.makedirs("temp_download", exist_ok=True)
    
    if os.path.exists(OUTPUT_ZIP):
        os.remove(OUTPUT_ZIP)

    # 3. Download and Combine
    with open(OUTPUT_ZIP, 'wb') as outfile:
        for filename in FILES_TO_DOWNLOAD:
            file_path = Path("temp_download") / filename
            url = f"{BASE_URL}/{filename}"
            
            print(f"  Downloading {filename} from {url}...")
            r = requests.get(url, stream=True)
            if r.status_code != 200:
                raise RuntimeError(f"Download failed for {filename}: HTTP {r.status_code}")
            
            with open(file_path, 'wb') as f:
                for chunk in r.iter_content(chunk_size=8192):
                    f.write(chunk)
            
            file_size_mb = file_path.stat().st_size / 1024 / 1024
            print(f"  Downloaded {filename} ({file_size_mb:.2f} MB). Appending to zip...")
            
            with open(file_path, 'rb') as infile:
                shutil.copyfileobj(infile, outfile)

    # 4. Extract
    total_size_mb = os.path.getsize(OUTPUT_ZIP)/1024/1024
    print(f"📂 Extracting {OUTPUT_ZIP} ({total_size_mb:.2f} MB)...")
    
    try:
        with zipfile.ZipFile(OUTPUT_ZIP, 'r') as zip_ref:
            zip_ref.extractall(".")
            print("  Extraction complete.")
    except zipfile.BadZipFile as e:
        print(f"❌ BadZipFile Error: {e}")
        raise e
    
    shutil.rmtree("temp_download", ignore_errors=True)
    if os.path.exists(OUTPUT_ZIP):
        os.remove(OUTPUT_ZIP)

    # --- FIX: Handle potential backslash filenames on Linux ---
    print("🧹 Checking for backslash issues in filenames...")
    count = 0
    # Iterate over files in current directory to check for backslashes in names
    for filename in os.listdir("."):
        if "\\" in filename:
            # It's a file with backslashes in name, implying flattened structure
            new_path = filename.replace("\\", "/") # standardize to forward slash
            
            # Create parent dirs
            parent = os.path.dirname(new_path)
            if parent:
                os.makedirs(parent, exist_ok=True)
            
            # Move file
            try:
                shutil.move(filename, new_path)
                count += 1
            except Exception as e:
                print(f"  Failed to move {filename} -> {new_path}: {e}")
            
    if count > 0:
        print(f"✅ Fixed {count} filenames with backslashes. Directory structure restored.")
        
    # 5. Locate and Fix
    print("🔎 Locating dataset root...")
    for root, dirs, files in os.walk(".", topdown=True):
        p = Path(root)
        if "images" in dirs and ("depth_metadata.json" in files or "metadata_temp" in dirs):
            if validate_root(p):
                 print(f"✅ Dataset extracted and verified at: {p}")
                 return p
            
    return None

DATASET_PATH = setup_dataset()
if not DATASET_PATH: raise RuntimeError("Dataset setup failed: Could not locate or reconstruct metadata")
DATASET_ROOT = DATASET_PATH
'''

# Required robust imports
ROBUST_IMPORTS = {
    "os", "sys", "json", "requests", "zipfile", "shutil", "glob", "inspect", "torch", "numpy", "matplotlib.pyplot", "PIL", "tqdm"
}

# Mapping from import name to line (simplified)
IMPORT_LINES = {
    "os": "import os",
    "sys": "import sys",
    "json": "import json",
    "requests": "import requests",
    "zipfile": "import zipfile",
    "shutil": "import shutil",
    "glob": "import glob",
    "inspect": "import inspect",
    "torch": "import torch",
    "numpy": "import numpy as np",
    "matplotlib.pyplot": "import matplotlib.pyplot as plt",
    "PIL": "from PIL import Image",
    "tqdm": "from tqdm.auto import tqdm",
    "pathlib": "from pathlib import Path"
}

TARGET_NOTEBOOKS = [
    "1_train_segmentation.ipynb",
    "2_train_classification.ipynb",
    "3_train_matching.ipynb",
    "4_evaluate_depth_models.ipynb",
    "6_evaluate_depth_anything_v2.ipynb", # Assuming exists or trying
    "kaggle_dataset_overview.ipynb"
]

def update_notebook(path):
    if not os.path.exists(path):
        print(f"Skipping {path} (not found)")
        return

    print(f"Updating {path}...")
    with open(path, "r", encoding="utf-8") as f:
        nb = json.load(f)

    # Find the cell with setup_dataset or defining DATASET_ROOT/Downloading
    target_cell_idx = -1
    for idx, cell in enumerate(nb["cells"]):
        if cell["cell_type"] == "code":
            source = "".join(cell["source"])
            if "setup_dataset" in source or "strawberry_synthetic_dataset" in source:
                target_cell_idx = idx
                break
    
    if target_cell_idx == -1:
        print(f"  Cannot find dataset setup cell in {path}")
        return

    original_source = nb["cells"][target_cell_idx]["source"]
    original_text = "".join(original_source)
    
    # Extract existing imports from the cell
    existing_imports = set()
    other_lines = []
    
    # Simple parser for existing imports in that cell
    # (So we preserve things like 'import ultralytics' or 'import cv2')
    lines_to_keep = []
    
    for line in original_source:
        l = line.strip()
        if l.startswith("import ") or l.startswith("from "):
            if "setup_dataset" in l: continue # skip if it was importing setup_dataset? unlikely
            lines_to_keep.append(line.rstrip() + "\n")
    
    # Deduplicate and merge imports
    imports_to_preserve = []
    
    # Simple normalization: keep lines that start with import/from
    # Standard robust imports are:
    # "imports os", "sys", "json", "requests", "zipfile", "shutil", "glob", "inspect", "torch", "numpy as np", "matplotlib.pyplot as plt", "from PIL import Image", "from tqdm.auto import tqdm", "from pathlib import Path"
    
    # We will build a list of robust import lines first
    robust_import_lines = [
        "import os\n",
        "import sys\n",
        "import json\n",
        "import requests\n",
        "import zipfile\n",
        "import shutil\n",
        "import glob\n",
        "import inspect\n",
        "import torch\n",
        "import numpy as np\n",
        "import matplotlib.pyplot as plt\n",
        "from PIL import Image\n",
        "from tqdm.auto import tqdm\n",
        "from pathlib import Path\n"
    ]
    
    # Check original source for additional imports
    present_import_signatures = {line.strip() for line in robust_import_lines}
    
    for line in original_source:
        l = line.strip()
        if (l.startswith("import ") or l.startswith("from ")) and l not in present_import_signatures:
             imports_to_preserve.append(line.rstrip() + "\n")
             present_import_signatures.add(l)

    # Combine
    final_source_lines = robust_import_lines + imports_to_preserve + ["\n"] + ROBUST_SETUP_LOGIC.splitlines(keepends=True)
    
    # Update cell
    nb["cells"][target_cell_idx]["source"] = final_source_lines
    
    with open(path, "w", encoding="utf-8") as f:
        json.dump(nb, f, indent=1)
    
    print(f"  Updated setup cell in {path}.")

if __name__ == "__main__":
    for nb_file in TARGET_NOTEBOOKS:
        # Check if full path or relative
        full_path = str(Path(nb_file).absolute())
        if not os.path.exists(full_path):
             # Try assuming script is in same dir as notebooks?
             # Or assume CWD is Scripts dir
             if os.path.exists(nb_file):
                 full_path = nb_file
             else:
                 print(f"Skipping {nb_file} (not found)")
                 continue
        
        update_notebook(full_path)
