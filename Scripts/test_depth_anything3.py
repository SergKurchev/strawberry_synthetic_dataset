"""
Test script for Depth Anything 3 installation and inference
This script tests if DA3 can be installed and run on CPU
"""

import sys
import os

print("="*60)
print("Depth Anything 3 - Installation Test")
print("="*60)

# Step 1: Check Python version
print(f"\nPython version: {sys.version}")
import torch
print(f"PyTorch version: {torch.__version__}")
print(f"CUDA available: {torch.cuda.is_available()}")

# Step 2: Try to install DA3
print("\n" + "="*60)
print("Installing Depth Anything 3...")
print("="*60)

import subprocess

# Clone repository
DA3_DIR = "Depth-Anything-3"
if not os.path.exists(DA3_DIR):
    print("\nCloning repository...")
    result = subprocess.run(
        ["git", "clone", "https://github.com/ByteDance-Seed/Depth-Anything-3.git"],
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        print(f"Error cloning: {result.stderr}")
        sys.exit(1)
    print("✓ Repository cloned")
else:
    print("✓ Repository already exists")

# Try to install without xformers first (CPU mode)
print("\nInstalling dependencies (CPU mode, without xformers)...")

# Read requirements and remove xformers
requirements_path = os.path.join(DA3_DIR, "requirements.txt")
if os.path.exists(requirements_path):
    with open(requirements_path, 'r') as f:
        requirements = f.readlines()
    
    # Filter out xformers
    filtered_reqs = [req for req in requirements if 'xformers' not in req.lower()]
    
    # Create temporary requirements file
    temp_req_path = "temp_requirements_da3.txt"
    with open(temp_req_path, 'w') as f:
        f.writelines(filtered_reqs)
    
    print(f"Installing from filtered requirements...")
    result = subprocess.run(
        [sys.executable, "-m", "pip", "install", "-r", temp_req_path],
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        print(f"Warning: Some packages failed to install")
        print(result.stderr[:500])
    else:
        print("✓ Dependencies installed")
    
    os.remove(temp_req_path)

# Install the package itself
print("\nInstalling Depth Anything 3 package...")
result = subprocess.run(
    [sys.executable, "-m", "pip", "install", "-e", DA3_DIR],
    capture_output=True,
    text=True
)

if result.returncode != 0:
    print(f"Error installing package: {result.stderr[:500]}")
    print("\nTrying alternative installation...")
    
    # Try installing just the core dependencies
    core_deps = ["torch", "torchvision", "numpy", "pillow", "opencv-python", "einops", "timm"]
    for dep in core_deps:
        print(f"Installing {dep}...")
        subprocess.run([sys.executable, "-m", "pip", "install", dep], 
                      capture_output=True)
    
    # Add to path
    sys.path.insert(0, os.path.join(os.getcwd(), DA3_DIR, "src"))
else:
    print("✓ Package installed")

# Step 3: Try to import and use
print("\n" + "="*60)
print("Testing Depth Anything 3...")
print("="*60)

try:
    from depth_anything_3.api import DepthAnything3
    print("✓ Successfully imported DepthAnything3")
    
    # Try to load model
    print("\nLoading model (this may take a while)...")
    device = torch.device('cpu')
    
    # Try smallest model first
    try:
        model = DepthAnything3.from_pretrained("depth-anything/DA3NESTED-GIANT-LARGE")
        model = model.to(device=device)
        model.eval()
        print("✓ Model loaded successfully on CPU")
        
        # Create dummy image
        print("\nTesting inference on dummy image...")
        import numpy as np
        from PIL import Image
        
        dummy_img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
        dummy_img = Image.fromarray(dummy_img)
        dummy_img.save("test_image.png")
        
        # Run inference
        prediction = model.inference(["test_image.png"])
        print(f"✓ Inference successful!")
        print(f"  Output shape: {prediction.depth.shape}")
        
        # Cleanup
        os.remove("test_image.png")
        
        print("\n" + "="*60)
        print("SUCCESS: Depth Anything 3 works on CPU!")
        print("="*60)
        
    except Exception as e:
        print(f"✗ Error loading/running model: {e}")
        print("\nThis is expected - DA3 may require GPU or specific dependencies")
        sys.exit(1)
        
except ImportError as e:
    print(f"✗ Failed to import: {e}")
    print("\nTrying alternative import method...")
    
    # Try direct import
    sys.path.insert(0, os.path.join(os.getcwd(), DA3_DIR, "src"))
    try:
        from depth_anything_3.api import DepthAnything3
        print("✓ Alternative import successful")
    except Exception as e2:
        print(f"✗ Alternative import also failed: {e2}")
        print("\nDepth Anything 3 cannot be installed on this system")
        sys.exit(1)
