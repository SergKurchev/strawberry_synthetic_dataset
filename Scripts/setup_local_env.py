"""
Local Testing Setup Script
Creates virtual environment and installs dependencies for testing notebooks locally (CPU mode)
"""

import subprocess
import sys
import os
from pathlib import Path

def run_command(cmd, description):
    """Run a command and print status"""
    print(f"\n{'='*60}")
    print(f"{description}")
    print(f"{'='*60}")
    try:
        subprocess.run(cmd, check=True, shell=True)
        print(f"✓ {description} completed successfully")
        return True
    except subprocess.CalledProcessError as e:
        print(f"✗ {description} failed: {e}")
        return False

def main():
    print("="*60)
    print("Strawberry Dataset - Local Testing Setup")
    print("="*60)
    
    # Get script directory
    script_dir = Path(__file__).parent
    venv_dir = script_dir.parent / "last_straw_venv"
    
    print(f"\nVirtual environment will be created at: {venv_dir}")
    
    # Check if venv already exists
    if venv_dir.exists():
        response = input(f"\nVirtual environment already exists. Recreate? (y/n): ")
        if response.lower() != 'y':
            print("Skipping venv creation")
        else:
            print(f"Removing existing venv...")
            import shutil
            shutil.rmtree(venv_dir)
    
    # Create virtual environment
    if not venv_dir.exists():
        if not run_command(
            f'python -m venv "{venv_dir}"',
            "Creating virtual environment"
        ):
            return False
    
    # Determine activation script
    if sys.platform == "win32":
        activate_script = venv_dir / "Scripts" / "activate.bat"
        pip_exe = venv_dir / "Scripts" / "pip.exe"
    else:
        activate_script = venv_dir / "bin" / "activate"
        pip_exe = venv_dir / "bin" / "pip"
    
    print(f"\nTo activate the environment, run:")
    if sys.platform == "win32":
        print(f"  {activate_script}")
    else:
        print(f"  source {activate_script}")
    
    # Upgrade pip
    if not run_command(
        f'"{pip_exe}" install --upgrade pip',
        "Upgrading pip"
    ):
        return False
    
    # Install requirements
    requirements_file = script_dir / "requirements_local.txt"
    if not requirements_file.exists():
        print(f"\n✗ Requirements file not found: {requirements_file}")
        return False
    
    if not run_command(
        f'"{pip_exe}" install -r "{requirements_file}"',
        "Installing dependencies (this may take several minutes)"
    ):
        return False
    
    # Install Jupyter
    if not run_command(
        f'"{pip_exe}" install jupyter notebook ipykernel',
        "Installing Jupyter"
    ):
        return False
    
    # Create kernel
    kernel_name = "strawberry_dataset"
    if not run_command(
        f'"{pip_exe}" install ipykernel && '
        f'"{venv_dir / ("Scripts" if sys.platform == "win32" else "bin") / "python"}" '
        f'-m ipykernel install --user --name={kernel_name} --display-name="Python (Strawberry Dataset)"',
        f"Creating Jupyter kernel '{kernel_name}'"
    ):
        print("Warning: Kernel creation failed, but you can still use the environment")
    
    print("\n" + "="*60)
    print("Setup Complete!")
    print("="*60)
    print("\nNext steps:")
    print("1. Activate the virtual environment:")
    if sys.platform == "win32":
        print(f"   {activate_script}")
    else:
        print(f"   source {activate_script}")
    print("\n2. Start Jupyter:")
    print("   jupyter notebook")
    print("\n3. Open any notebook and select kernel: 'Python (Strawberry Dataset)'")
    print("\nNotes:")
    print("- These notebooks are designed for Kaggle with GPU")
    print("- Local testing on CPU will be SLOW, especially for:")
    print("  * Task 1: YOLO training (use small model)")
    print("  * Task 2: Classification training")
    print("  * Task 4: Depth models (may not work on CPU)")
    print("- Consider using smaller batch sizes and fewer epochs for testing")
    
    return True

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
