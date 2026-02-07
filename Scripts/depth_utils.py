# Depth Loading Helper Functions for Notebooks

def load_depth_png(depth_path):
    """
    Load depth from PNG file with proper 16-bit decoding.
    
    The depth is encoded as 16-bit value split across R and G channels:
    - R channel: high byte (depth_mm >> 8)
    - G channel: low byte (depth_mm & 0xFF)
    
    Args:
        depth_path: Path to depth PNG file (string or Path object)
        
    Returns:
        Depth in meters as float32 numpy array
    """
    from PIL import Image
    import numpy as np
    
    img = Image.open(depth_path)
    depth_arr = np.array(img)
    
    if len(depth_arr.shape) == 3 and depth_arr.shape[2] >= 2:
        # RGB encoded - 16-bit value in R (high) and G (low) channels
        high = depth_arr[:, :, 0].astype(np.uint16)
        low = depth_arr[:, :, 1].astype(np.uint16)
        depth_mm = (high << 8) | low  # Reconstruct 16-bit value
        depth_m = depth_mm.astype(np.float32) / 1000.0  # mm to m
    else:
        # Single channel - assume already in mm
        depth_m = depth_arr.astype(np.float32) / 1000.0
    
    return depth_m


def load_depth_npy(depth_path):
    """
    Load depth from NPY file (already in meters).
    
    Args:
        depth_path: Path to depth NPY file
        
    Returns:
        Depth in meters as float32
    """
    import numpy as np
    return np.load(depth_path).astype(np.float32)


# Example usage in notebooks:
# 
# # Load depth from PNG
# depth_meters = load_depth_png("path/to/depth/00000.png")
# 
# # Or load from NPY (faster, already in meters)
# depth_meters = load_depth_npy("path/to/depth_npy/00000.npy")
