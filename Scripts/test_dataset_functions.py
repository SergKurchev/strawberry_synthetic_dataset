# Test script for dataset_overview functions (no matplotlib display)

import json
import numpy as np
from pathlib import Path
from PIL import Image
import cv2

# Dataset path
DATASET_PATH = Path("../strawberry_dataset")

# Category definitions
CATEGORIES = {
    0: {"name": "strawberry_ripe", "color": (0, 255, 0)},
    1: {"name": "strawberry_unripe", "color": (255, 0, 0)},
    2: {"name": "strawberry_half_ripe", "color": (255, 165, 0)},
    3: {"name": "peduncle", "color": (139, 69, 19)}
}

def load_annotations(dataset_path):
    with open(dataset_path / "annotations.json", 'r') as f:
        data = json.load(f)
    return data, data.get("images", []), data.get("annotations", [])

def get_annotations_for_image(annotations, image_id):
    return [ann for ann in annotations if ann["image_id"] == image_id]

def draw_bboxes(image, annotations, categories=CATEGORIES, thickness=2, font_scale=0.5):
    result = image.copy()
    for ann in annotations:
        bbox = ann["bbox"]
        x, y, w, h = int(bbox[0]), int(bbox[1]), int(bbox[2]), int(bbox[3])
        cat_id = ann["category_id"]
        cat_info = categories.get(cat_id, {"name": "unknown", "color": (128, 128, 128)})
        color = cat_info["color"]
        cv2.rectangle(result, (x, y), (x + w, y + h), color, thickness)
    return result

def extract_instance_mask(mask_image, segmentation_color):
    r, g, b = segmentation_color
    mask = (
        (mask_image[:, :, 0] == r) &
        (mask_image[:, :, 1] == g) &
        (mask_image[:, :, 2] == b)
    )
    return (mask * 255).astype(np.uint8)

def visualize_masks_overlay(image, mask_image, annotations, alpha=0.5):
    # Ensure RGB (3 channels)
    if len(image.shape) == 3 and image.shape[-1] == 4:
        image = image[:, :, :3]
    
    result = image.copy().astype(np.float32)
    overlay = np.zeros((image.shape[0], image.shape[1], 3), dtype=np.float32)
    
    for ann in annotations:
        seg_color = ann["segmentation_color"]
        cat_id = ann["category_id"]
        instance_mask = extract_instance_mask(mask_image, seg_color)
        cat_color = CATEGORIES.get(cat_id, {"color": (128, 128, 128)})["color"]
        mask_bool = instance_mask > 0
        for c in range(3):
            overlay[:, :, c] = np.where(mask_bool, cat_color[c], overlay[:, :, c])
    
    mask_active = np.any(overlay > 0, axis=2)
    for c in range(3):
        result[:, :, c] = np.where(mask_active, 
                                    result[:, :, c] * (1 - alpha) + overlay[:, :, c] * alpha,
                                    result[:, :, c])
    
    return result.astype(np.uint8)

def load_depth_png(depth_path):
    """
    Load depth from PNG. Depth is encoded as 16-bit in R(high)+G(low) channels.
    """
    img = Image.open(depth_path)
    depth_arr = np.array(img)
    
    if len(depth_arr.shape) == 3 and depth_arr.shape[2] >= 2:
        # 16-bit value in R (high) and G (low) channels
        high = depth_arr[:, :, 0].astype(np.uint16)
        low = depth_arr[:, :, 1].astype(np.uint16)
        depth_mm = (high << 8) | low
        depth_m = depth_mm.astype(np.float32) / 1000.0
    else:
        depth_m = depth_arr.astype(np.float32) / 1000.0
    
    return depth_m

def load_depth_metadata(dataset_path):
    metadata_path = dataset_path / "depth_metadata.json"
    if not metadata_path.exists():
        return {}
    try:
        with open(metadata_path, 'r') as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        print(f"Warning: Invalid JSON in depth_metadata.json: {e}")
        return {}  # Return empty dict on parse error

def get_intrinsics_matrix(metadata):
    intr = metadata["camera_intrinsics"]
    K = np.array([
        [intr["fx"], 0,          intr["cx"]],
        [0,          intr["fy"], intr["cy"]],
        [0,          0,          1]
    ], dtype=np.float64)
    return K

def pixel_to_3d(u, v, depth, K):
    fx, fy = K[0, 0], K[1, 1]
    cx, cy = K[0, 2], K[1, 2]
    x = (u - cx) * depth / fx
    y = (v - cy) * depth / fy
    z = depth
    return np.array([x, y, z])

def find_matching_pairs(annotations, image_id):
    img_anns = get_annotations_for_image(annotations, image_id)
    strawberries = [a for a in img_anns if a["category_id"] in [0, 1, 2]]
    peduncles = [a for a in img_anns if a["category_id"] == 3]
    peduncle_map = {p["instance_id"]: p for p in peduncles}
    pairs = []
    for straw in strawberries:
        parent_id = straw["parent_id"]
        if parent_id in peduncle_map:
            pairs.append((straw, peduncle_map[parent_id]))
    return pairs

def run_tests():
    """Run all self-tests to verify functions work correctly."""
    print("=" * 60)
    print("RUNNING SELF-TESTS (without matplotlib visualization)")
    print("=" * 60)
    
    errors = []
    
    # Test 1: Load annotations
    print("\n✓ Test 1: load_annotations()")
    try:
        data, images, annotations = load_annotations(DATASET_PATH)
        assert len(images) > 0, "No images loaded"
        assert len(annotations) > 0, "No annotations loaded"
        print(f"   Loaded {len(images)} images, {len(annotations)} annotations")
    except Exception as e:
        errors.append(f"Test 1 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
        return False
    
    # Test 2: Get annotations for image
    print("\n✓ Test 2: get_annotations_for_image()")
    try:
        img_anns = get_annotations_for_image(annotations, 0)
        assert len(img_anns) > 0, "No annotations for image 0"
        print(f"   Image 0 has {len(img_anns)} annotations")
    except Exception as e:
        errors.append(f"Test 2 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 3: Load image
    print("\n✓ Test 3: Load image")
    try:
        img = np.array(Image.open(DATASET_PATH / "images" / "00000.png").convert("RGB"))
        assert img.shape == (1024, 1024, 3), f"Unexpected shape: {img.shape}"
        print(f"   Image shape: {img.shape}")
    except Exception as e:
        errors.append(f"Test 3 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 4: Draw bboxes
    print("\n✓ Test 4: draw_bboxes()")
    try:
        result = draw_bboxes(img, img_anns)
        assert result.shape == img.shape, "Output shape mismatch"
        print(f"   Drawing successful, output shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 4 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 5: Load mask
    print("\n✓ Test 5: Load mask")
    try:
        mask_img = np.array(Image.open(DATASET_PATH / "masks" / "00000.png").convert("RGB"))
        assert mask_img.shape == (1024, 1024, 3), f"Unexpected mask shape: {mask_img.shape}"
        print(f"   Mask shape: {mask_img.shape}")
    except Exception as e:
        errors.append(f"Test 5 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 6: Extract instance mask
    print("\n✓ Test 6: extract_instance_mask()")
    try:
        seg_color = img_anns[0]["segmentation_color"]
        instance_mask = extract_instance_mask(mask_img, seg_color)
        assert instance_mask.shape[:2] == mask_img.shape[:2], "Mask shape mismatch"
        print(f"   Mask extracted, non-zero pixels: {np.sum(instance_mask > 0)}")
    except Exception as e:
        errors.append(f"Test 6 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 7: Mask overlay
    print("\n✓ Test 7: visualize_masks_overlay()")
    try:
        result = visualize_masks_overlay(img, mask_img, img_anns)
        assert result.shape == (1024, 1024, 3), f"Overlay shape: {result.shape}"
        print(f"   Overlay created, shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 7 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 8: Load depth
    print("\n✓ Test 8: load_depth_png()")
    try:
        depth = load_depth_png(DATASET_PATH / "depth" / "00000.png")
        assert depth.shape == (1024, 1024), f"Unexpected depth shape: {depth.shape}"
        valid = depth[depth > 0]
        print(f"   Depth loaded, range: {valid.min():.2f}m - {valid.max():.2f}m")
    except Exception as e:
        errors.append(f"Test 8 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 9: Camera intrinsics
    K = None
    print("\n✓ Test 9: get_intrinsics_matrix()")
    try:
        metadata = load_depth_metadata(DATASET_PATH)
        if metadata:
            first_key = list(metadata.keys())[0]
            K = get_intrinsics_matrix(metadata[first_key])
            print(f"   Intrinsics from metadata: K shape {K.shape}")
        else:
            # Default fallback for 1024x1024, 60° FOV
            K = np.array([[886.81, 0, 512.0], [0, 886.81, 512.0], [0, 0, 1]])
            print(f"   Using default intrinsics (metadata unavailable): K shape {K.shape}")
        assert K.shape == (3, 3), f"Unexpected K shape: {K.shape}"
    except Exception as e:
        errors.append(f"Test 9 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 10: Pixel to 3D
    print("\n✓ Test 10: pixel_to_3d()")
    try:
        if K is not None:
            point_3d = pixel_to_3d(512, 512, 1.0, K)
            assert len(point_3d) == 3, f"Unexpected point shape"
            print(f"   3D point at center (1m depth): {point_3d}")
        else:
            print(f"   Skipped (K matrix not available)")
    except Exception as e:
        errors.append(f"Test 10 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 11: Matching pairs
    print("\n✓ Test 11: find_matching_pairs()")
    try:
        pairs = find_matching_pairs(annotations, 0)
        print(f"   Found {len(pairs)} strawberry-peduncle pairs")
    except Exception as e:
        errors.append(f"Test 11 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Summary
    print("\n" + "=" * 60)
    if errors:
        print(f"❌ {len(errors)} TESTS FAILED:")
        for err in errors:
            print(f"   - {err}")
    else:
        print("✅ ALL 11 TESTS PASSED!")
    print("=" * 60)
    
    return len(errors) == 0

if __name__ == "__main__":
    import sys
    success = run_tests()
    sys.exit(0 if success else 1)
