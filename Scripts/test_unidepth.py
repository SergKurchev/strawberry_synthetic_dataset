"""
Test script for UniDepthV2 installation and inference
This script tests if UniDepthV2 can be installed and run on CPU
"""

import sys
import os

print("="*60)
print("UniDepthV2 - Installation Test")
print("="*60)

# Step 1: Check Python version
print(f"\nPython version: {sys.version}")
import torch
print(f"PyTorch version: {torch.__version__}")
print(f"CUDA available: {torch.cuda.is_available()}")

# Step 2: Try to install UniDepth
print("\n" + "="*60)
print("Installing UniDepthV2...")
print("="*60)

import subprocess

# Clone repository
UNIDEPTH_DIR = "UniDepth"
if not os.path.exists(UNIDEPTH_DIR):
    print("\nCloning repository...")
    result = subprocess.run(
        ["git", "clone", "https://github.com/lpiccinelli-eth/UniDepth.git"],
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        print(f"Error cloning: {result.stderr}")
        sys.exit(1)
    print("✓ Repository cloned")
else:
    print("✓ Repository already exists")

# Install dependencies
print("\nInstalling dependencies...")

# Try to install from requirements
requirements_path = os.path.join(UNIDEPTH_DIR, "requirements.txt")
if os.path.exists(requirements_path):
    print(f"Installing from requirements.txt...")
    result = subprocess.run(
        [sys.executable, "-m", "pip", "install", "-r", requirements_path],
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        print(f"Warning: Some packages failed to install")
        print(result.stderr[:500])
    else:
        print("✓ Dependencies installed")

# Install the package itself
print("\nInstalling UniDepth package...")
result = subprocess.run(
    [sys.executable, "-m", "pip", "install", "-e", UNIDEPTH_DIR],
    capture_output=True,
    text=True
)

if result.returncode != 0:
    print(f"Error installing package: {result.stderr[:500]}")
    print("\nTrying alternative installation...")
    
    # Try installing just the core dependencies
    core_deps = ["torch", "torchvision", "numpy", "pillow", "opencv-python", "einops", "timm", "huggingface_hub"]
    for dep in core_deps:
        print(f"Installing {dep}...")
        subprocess.run([sys.executable, "-m", "pip", "install", dep], 
                      capture_output=True)
    
    # Add to path
    sys.path.insert(0, os.path.join(os.getcwd(), UNIDEPTH_DIR))
else:
    print("✓ Package installed")

# Step 3: Try to import and use
print("\n" + "="*60)
print("Testing UniDepthV2...")
print("="*60)

try:
    from unidepth.models import UniDepthV2
    print("✓ Successfully imported UniDepthV2")
    
    # Try to load model
    print("\nLoading model (this may take a while)...")
    device = torch.device('cpu')
    
    try:
        # Try loading from HuggingFace
        model = UniDepthV2.from_pretrained("lpiccinelli/unidepth-v2-vitl14")
        model = model.to(device)
        model.eval()
        print("✓ Model loaded successfully on CPU")
        
        # Create dummy image
        print("\nTesting inference on dummy image...")
        import numpy as np
        from PIL import Image
        
        dummy_img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
        dummy_img = Image.fromarray(dummy_img)
        
        # Run inference
        with torch.no_grad():
            predictions = model.infer(dummy_img)
        
        print(f"✓ Inference successful!")
        print(f"  Output keys: {predictions.keys()}")
        if 'depth' in predictions:
            print(f"  Depth shape: {predictions['depth'].shape}")
        
        print("\n" + "="*60)
        print("SUCCESS: UniDepthV2 works on CPU!")
        print("="*60)
        
    except Exception as e:
        print(f"✗ Error loading/running model: {e}")
        print(f"\nFull error: {str(e)}")
        
        # Try torch.hub as fallback
        print("\nTrying torch.hub loading method...")
        try:
            model = torch.hub.load("lpiccinelli-eth/UniDepth", "UniDepthV2", trust_repo=True)
            model = model.to(device)
            model.eval()
            print("✓ Model loaded via torch.hub")
            
            # Test inference
            dummy_img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
            dummy_img = Image.fromarray(dummy_img)
            
            with torch.no_grad():
                predictions = model.infer(dummy_img)
            
            print("✓ Inference successful via torch.hub!")
            print("\n" + "="*60)
            print("SUCCESS: UniDepthV2 works on CPU (via torch.hub)!")
            print("="*60)
            
        except Exception as e2:
            print(f"✗ torch.hub also failed: {e2}")
            print("\nUniDepthV2 cannot run on this system")
            sys.exit(1)
        
except ImportError as e:
    print(f"✗ Failed to import: {e}")
    print("\nTrying alternative import method...")
    
    # Try direct import
    sys.path.insert(0, os.path.join(os.getcwd(), UNIDEPTH_DIR))
    try:
        from unidepth.models import UniDepthV2
        print("✓ Alternative import successful")
    except Exception as e2:
        print(f"✗ Alternative import also failed: {e2}")
        print("\nUniDepthV2 cannot be installed on this system")
        sys.exit(1)
