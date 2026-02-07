# %% [markdown]
# # 🍓 Strawberry Dataset Overview
# 
# This notebook provides a comprehensive overview of the synthetic strawberry dataset, demonstrating:
# 
# 1. **Dataset Structure** - Files and annotation format
# 2. **BBox Visualization** - Drawing bounding boxes from annotations
# 3. **Mask Visualization** - Rendering instance masks from segmentation colors
# 4. **Depth Analysis** - Loading depth maps and camera intrinsics
# 5. **Strawberry-Peduncle Matching** - Understanding parent-child relationships
# 6. **Training Integration** - Examples for popular frameworks

# %% [markdown]
# ## 1. Setup and Imports

# %%
import json
import numpy as np
from pathlib import Path
from PIL import Image
import cv2
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
from typing import Dict, List, Tuple, Optional

# Dataset path - adjust if needed
DATASET_PATH = Path("strawberry_dataset")
if not DATASET_PATH.exists():
    DATASET_PATH = Path("../strawberry_dataset")

# Category definitions
CATEGORIES = {
    0: {"name": "strawberry_ripe", "color": (0, 255, 0)},      # Green
    1: {"name": "strawberry_unripe", "color": (255, 0, 0)},    # Red
    2: {"name": "strawberry_half_ripe", "color": (255, 165, 0)}, # Orange
    3: {"name": "peduncle", "color": (139, 69, 19)}            # Brown
}

print(f"Dataset path: {DATASET_PATH.absolute()}")
print(f"Dataset exists: {DATASET_PATH.exists()}")

# %% [markdown]
# ## 2. Dataset Structure Overview

# %%
def explore_dataset(dataset_path: Path) -> Dict:
    """
    Explore dataset structure and return statistics.
    
    Args:
        dataset_path: Path to the dataset root folder
        
    Returns:
        Dictionary with dataset statistics
    """
    stats = {
        "images_count": 0,
        "labels_count": 0,
        "depth_count": 0,
        "masks_count": 0,
        "annotations": None,
        "categories": [],
        "total_annotations": 0
    }
    
    # Count files in each folder
    images_path = dataset_path / "images"
    if images_path.exists():
        stats["images_count"] = len(list(images_path.glob("*.png")))
    
    labels_path = dataset_path / "labels"
    if labels_path.exists():
        stats["labels_count"] = len(list(labels_path.glob("*.txt")))
    
    depth_path = dataset_path / "depth"
    if depth_path.exists():
        stats["depth_count"] = len(list(depth_path.glob("*.png")))
    
    masks_path = dataset_path / "masks"
    if masks_path.exists():
        stats["masks_count"] = len(list(masks_path.glob("*.png")))
    
    # Load annotations
    annotations_path = dataset_path / "annotations.json"
    if annotations_path.exists():
        with open(annotations_path, 'r') as f:
            annotations = json.load(f)
            stats["annotations"] = annotations
            stats["categories"] = annotations.get("categories", [])
            stats["total_annotations"] = len(annotations.get("annotations", []))
    
    return stats

# Explore the dataset
stats = explore_dataset(DATASET_PATH)
print("=" * 50)
print("DATASET STATISTICS")
print("=" * 50)
print(f"📷 Images: {stats['images_count']}")
print(f"🏷️ Labels (YOLO): {stats['labels_count']}")
print(f"📏 Depth maps: {stats['depth_count']}")
print(f"🎭 Masks: {stats['masks_count']}")
print(f"📝 Total annotations: {stats['total_annotations']}")
print("\n📂 Categories:")
for cat in stats['categories']:
    print(f"   {cat['id']}: {cat['name']}")

# %% [markdown]
# ## 3. Loading Annotations

# %%
def load_annotations(dataset_path: Path) -> Tuple[Dict, List, List]:
    """
    Load COCO-style annotations.
    
    Args:
        dataset_path: Path to the dataset root folder
        
    Returns:
        Tuple of (annotations_dict, images_list, annotations_list)
    """
    with open(dataset_path / "annotations.json", 'r') as f:
        data = json.load(f)
    
    return data, data.get("images", []), data.get("annotations", [])


def get_annotations_for_image(annotations: List, image_id: int) -> List:
    """
    Filter annotations for a specific image.
    
    Args:
        annotations: List of all annotations
        image_id: Target image ID
        
    Returns:
        List of annotations for the image
    """
    return [ann for ann in annotations if ann["image_id"] == image_id]


# Load annotations
data, images, annotations = load_annotations(DATASET_PATH)
print(f"Loaded {len(images)} images and {len(annotations)} annotations")

# Show sample annotation
if annotations:
    sample = annotations[0]
    print("\n🔍 Sample annotation structure:")
    for key, value in sample.items():
        print(f"   {key}: {value}")

# %% [markdown]
# ## 4. BBox Visualization
# 
# Drawing bounding boxes from COCO annotations.

# %%
def draw_bboxes(
    image: np.ndarray,
    annotations: List,
    categories: Dict = CATEGORIES,
    thickness: int = 2,
    font_scale: float = 0.5
) -> np.ndarray:
    """
    Draw bounding boxes on an image.
    
    Args:
        image: RGB or RGBA image as numpy array
        annotations: List of annotations for this image
        categories: Category definitions with colors
        thickness: Line thickness
        font_scale: Font scale for labels
        
    Returns:
        Image with drawn bboxes (RGB)
    """
    # Ensure RGB
    if image.shape[-1] == 4:
        image = image[:, :, :3]
    
    result = image.copy()
    
    for ann in annotations:
        # Get bbox: [x, y, width, height]
        bbox = ann["bbox"]
        x, y, w, h = int(bbox[0]), int(bbox[1]), int(bbox[2]), int(bbox[3])
        
        # Get category info
        cat_id = ann["category_id"]
        cat_info = categories.get(cat_id, {"name": "unknown", "color": (128, 128, 128)})
        color = cat_info["color"]
        
        # Draw rectangle
        cv2.rectangle(result, (x, y), (x + w, y + h), color, thickness)
        
        # Draw label
        label = f"{cat_info['name'][:4]}_{ann['instance_id']}"
        label_size = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, font_scale, 1)[0]
        
        # Label background
        cv2.rectangle(result, (x, y - label_size[1] - 5), (x + label_size[0], y), color, -1)
        cv2.putText(result, label, (x, y - 3), cv2.FONT_HERSHEY_SIMPLEX, 
                    font_scale, (255, 255, 255), 1)
    
    return result


def visualize_bboxes(dataset_path: Path, image_id: int = 0) -> np.ndarray:
    """
    Load image and draw bboxes.
    
    Args:
        dataset_path: Path to dataset
        image_id: Image ID to visualize
        
    Returns:
        Image with bboxes
    """
    # Load data
    _, images, annotations = load_annotations(dataset_path)
    
    # Find image info
    img_info = next((img for img in images if img["id"] == image_id), None)
    if img_info is None:
        raise ValueError(f"Image {image_id} not found")
    
    # Load image (convert to RGB if needed)
    img_path = dataset_path / "images" / img_info["file_name"]
    image = np.array(Image.open(img_path).convert("RGB"))
    
    # Get annotations for this image
    img_annotations = get_annotations_for_image(annotations, image_id)
    
    # Draw bboxes
    result = draw_bboxes(image, img_annotations)
    
    print(f"Image {image_id}: {len(img_annotations)} objects")
    return result


# Visualize first image
bbox_viz = visualize_bboxes(DATASET_PATH, image_id=0)
plt.figure(figsize=(12, 12))
plt.imshow(bbox_viz)
plt.title("BBox Visualization")
plt.axis('off')
plt.show()

# %% [markdown]
# ## 5. Mask Visualization
# 
# Each annotation has a `segmentation_color` field [R, G, B] that encodes the instance in the mask image.

# %%
def extract_instance_mask(
    mask_image: np.ndarray,
    segmentation_color: List[int]
) -> np.ndarray:
    """
    Extract binary mask for a specific instance by its segmentation color.
    
    Args:
        mask_image: RGB mask image
        segmentation_color: [R, G, B] color of the instance
        
    Returns:
        Binary mask (0 or 255)
    """
    r, g, b = segmentation_color
    
    # Match all three channels
    mask = (
        (mask_image[:, :, 0] == r) &
        (mask_image[:, :, 1] == g) &
        (mask_image[:, :, 2] == b)
    )
    
    return (mask * 255).astype(np.uint8)


def visualize_masks_overlay(
    image: np.ndarray,
    mask_image: np.ndarray,
    annotations: List,
    alpha: float = 0.5
) -> np.ndarray:
    """
    Overlay colored instance masks on the RGB image.
    
    Args:
        image: RGB image
        mask_image: Segmentation mask image
        annotations: List of annotations
        alpha: Transparency of overlay
        
    Returns:
        Image with mask overlay (RGB)
    """
    # Ensure RGB (3 channels)
    if image.shape[-1] == 4:
        image = image[:, :, :3]
    
    result = image.copy().astype(np.float32)
    overlay = np.zeros((image.shape[0], image.shape[1], 3), dtype=np.float32)
    
    for ann in annotations:
        seg_color = ann["segmentation_color"]
        cat_id = ann["category_id"]
        
        # Extract instance mask
        instance_mask = extract_instance_mask(mask_image, seg_color)
        
        # Get category color
        cat_color = CATEGORIES.get(cat_id, {"color": (128, 128, 128)})["color"]
        
        # Apply color to overlay where mask is active
        mask_bool = instance_mask > 0
        for c in range(3):
            overlay[:, :, c] = np.where(mask_bool, cat_color[c], overlay[:, :, c])
    
    # Blend - broadcast mask_active properly
    mask_active = np.any(overlay > 0, axis=2)
    for c in range(3):
        result[:, :, c] = np.where(mask_active, 
                                    result[:, :, c] * (1 - alpha) + overlay[:, :, c] * alpha,
                                    result[:, :, c])
    
    return result.astype(np.uint8)


def visualize_masks(dataset_path: Path, image_id: int = 0) -> np.ndarray:
    """
    Load and visualize masks for an image.
    
    Args:
        dataset_path: Path to dataset
        image_id: Image ID to visualize
        
    Returns:
        Image with mask overlay
    """
    # Load data
    _, images, annotations = load_annotations(dataset_path)
    
    # Find image info
    img_info = next((img for img in images if img["id"] == image_id), None)
    if img_info is None:
        raise ValueError(f"Image {image_id} not found")
    
    # Load image and mask
    img_path = dataset_path / "images" / img_info["file_name"]
    mask_path = dataset_path / "masks" / img_info["file_name"]
    
    image = np.array(Image.open(img_path).convert("RGB"))
    mask = np.array(Image.open(mask_path).convert("RGB"))
    
    # Get annotations
    img_annotations = get_annotations_for_image(annotations, image_id)
    
    # Create overlay
    result = visualize_masks_overlay(image, mask, img_annotations)
    
    return result


# Visualize masks
mask_viz = visualize_masks(DATASET_PATH, image_id=0)
plt.figure(figsize=(12, 12))
plt.imshow(mask_viz)
plt.title("Mask Overlay Visualization")
plt.axis('off')
plt.show()

# %% [markdown]
# ## 6. Depth Visualization
# 
# Depth maps are stored in two formats:
# - **PNG (16-bit)**: `depth/*.png` - millimeters encoded as uint16
# - **NPY (float32)**: `depth_npy/*.npy` - direct meters as float32

# %%
def load_depth_png(depth_path: Path) -> np.ndarray:
    """
    Load depth from PNG file.
    
    The depth is encoded as 16-bit value split across R and G channels:
    - R channel: high byte (depth_mm >> 8)
    - G channel: low byte (depth_mm & 0xFF)
    
    Args:
        depth_path: Path to depth PNG file
        
    Returns:
        Depth in meters as float32
    """
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


def load_depth_npy(depth_path: Path) -> np.ndarray:
    """
    Load depth from NPY (already in meters).
    
    Args:
        depth_path: Path to depth NPY file
        
    Returns:
        Depth in meters as float32
    """
    return np.load(depth_path).astype(np.float32)


def visualize_depth(depth: np.ndarray, cmap: str = 'turbo') -> np.ndarray:
    """
    Visualize depth map with colormap.
    
    Args:
        depth: Depth in meters
        cmap: Matplotlib colormap name
        
    Returns:
        Colored depth visualization
    """
    # Normalize depth for visualization
    valid_mask = depth > 0
    if not np.any(valid_mask):
        return np.zeros((*depth.shape, 3), dtype=np.uint8)
    
    depth_normalized = depth.copy()
    depth_min = depth[valid_mask].min()
    depth_max = depth[valid_mask].max()
    
    depth_normalized = (depth - depth_min) / (depth_max - depth_min + 1e-8)
    depth_normalized = np.clip(depth_normalized, 0, 1)
    depth_normalized[~valid_mask] = 0
    
    # Apply colormap
    colormap = plt.get_cmap(cmap)
    colored = (colormap(depth_normalized)[:, :, :3] * 255).astype(np.uint8)
    
    return colored


# Load and visualize depth
depth_png_path = DATASET_PATH / "depth" / "00000.png"
if depth_png_path.exists():
    depth = load_depth_png(depth_png_path)
    depth_viz = visualize_depth(depth)
    
    fig, axes = plt.subplots(1, 2, figsize=(16, 8))
    
    # Original image
    img = np.array(Image.open(DATASET_PATH / "images" / "00000.png"))
    axes[0].imshow(img)
    axes[0].set_title("RGB Image")
    axes[0].axis('off')
    
    # Depth
    axes[1].imshow(depth_viz)
    axes[1].set_title(f"Depth (range: {depth[depth > 0].min():.2f}m - {depth[depth > 0].max():.2f}m)")
    axes[1].axis('off')
    
    plt.tight_layout()
    plt.show()

# %% [markdown]
# ## 7. Camera Intrinsics Matrix
# 
# Camera parameters are stored in `depth_metadata.json` per image.

# %%
def load_depth_metadata(dataset_path: Path) -> Dict:
    """
    Load depth metadata for all images.
    
    Args:
        dataset_path: Path to dataset
        
    Returns:
        Dictionary of metadata per image
    """
    metadata_path = dataset_path / "depth_metadata.json"
    if not metadata_path.exists():
        return {}
    
    try:
        with open(metadata_path, 'r') as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        print(f"⚠️ Warning: Invalid JSON in depth_metadata.json: {e}")
        print("   Using default camera intrinsics instead.")
        return {}


def get_intrinsics_matrix(metadata: Dict) -> np.ndarray:
    """
    Build 3x3 camera intrinsics matrix from metadata.
    
    Args:
        metadata: Metadata for one image
        
    Returns:
        3x3 intrinsics matrix K
    """
    intr = metadata["camera_intrinsics"]
    
    # Build K matrix
    K = np.array([
        [intr["fx"], 0,          intr["cx"]],
        [0,          intr["fy"], intr["cy"]],
        [0,          0,          1]
    ], dtype=np.float64)
    
    return K


def pixel_to_3d(u: int, v: int, depth: float, K: np.ndarray) -> np.ndarray:
    """
    Convert pixel coordinates to 3D point using depth and intrinsics.
    
    Args:
        u: Pixel x coordinate
        v: Pixel y coordinate
        depth: Depth in meters
        K: Camera intrinsics matrix
        
    Returns:
        3D point [x, y, z] in camera frame
    """
    fx, fy = K[0, 0], K[1, 1]
    cx, cy = K[0, 2], K[1, 2]
    
    x = (u - cx) * depth / fx
    y = (v - cy) * depth / fy
    z = depth
    
    return np.array([x, y, z])


# Load metadata and show intrinsics
depth_metadata = load_depth_metadata(DATASET_PATH)

if depth_metadata:
    first_image = list(depth_metadata.keys())[0]
    meta = depth_metadata[first_image]
    
    print(f"📷 Camera Intrinsics for {first_image}:")
    K = get_intrinsics_matrix(meta)
    print(f"\nK matrix:\n{K}")
    
    print(f"\n📐 Depth Range:")
    print(f"   Min: {meta['depth_range']['min_meters']:.4f} m")
    print(f"   Max: {meta['depth_range']['max_meters']:.4f} m")
    
    print(f"\n🎯 Camera Position: {meta['camera_extrinsics']['position']}")
    print(f"🔄 Camera Rotation (quaternion): {meta['camera_extrinsics']['rotation']}")
else:
    # Default intrinsics for 1024x1024 image with 60° FOV
    print("⚠️ depth_metadata.json not available or invalid")
    print("Using default camera intrinsics:")
    K = np.array([
        [886.81, 0, 512.0],
        [0, 886.81, 512.0],
        [0, 0, 1]
    ])
    print(f"\nK matrix (default for 1024x1024, 60° FOV):\n{K}")

# %% [markdown]
# ## 8. Strawberry-Peduncle Matching
# 
# Each strawberry has a `parent_id` that links it to the associated peduncle.
# Peduncles have `parent_id = 0`.

# %%
def find_matching_pairs(annotations: List, image_id: int) -> List[Tuple]:
    """
    Find strawberry-peduncle pairs in an image.
    
    Matching logic:
    - Peduncles have category_id=3 and parent_id=0
    - Strawberries have their instance_id as parent_id for their peduncle
    - A peduncle's instance_id matches its associated strawberries' parent_id
    
    Args:
        annotations: All annotations
        image_id: Target image
        
    Returns:
        List of (strawberry_ann, peduncle_ann) tuples
    """
    img_anns = get_annotations_for_image(annotations, image_id)
    
    # Separate strawberries and peduncles
    strawberries = [a for a in img_anns if a["category_id"] in [0, 1, 2]]
    peduncles = [a for a in img_anns if a["category_id"] == 3]
    
    # Create mapping: peduncle instance_id -> peduncle annotation
    peduncle_map = {p["instance_id"]: p for p in peduncles}
    
    pairs = []
    for straw in strawberries:
        parent_id = straw["parent_id"]
        # Find matching peduncle
        if parent_id in peduncle_map:
            pairs.append((straw, peduncle_map[parent_id]))
    
    return pairs


def get_bbox_center(bbox: List) -> Tuple[int, int]:
    """Get center point of bbox [x, y, w, h]."""
    x, y, w, h = bbox
    return int(x + w / 2), int(y + h / 2)


def visualize_matching(dataset_path: Path, image_id: int = 0) -> np.ndarray:
    """
    Visualize strawberry-peduncle matching with connection lines.
    
    Args:
        dataset_path: Path to dataset
        image_id: Image to visualize
        
    Returns:
        Image with matching visualization
    """
    # Load data
    _, images, annotations = load_annotations(dataset_path)
    
    # Find image info
    img_info = next((img for img in images if img["id"] == image_id), None)
    if img_info is None:
        raise ValueError(f"Image {image_id} not found")
    
    # Load image
    img_path = dataset_path / "images" / img_info["file_name"]
    image = np.array(Image.open(img_path).convert("RGB"))
    result = image.copy()
    
    # Find pairs
    pairs = find_matching_pairs(annotations, image_id)
    
    print(f"Found {len(pairs)} strawberry-peduncle pairs")
    
    # Draw connections
    for straw, ped in pairs:
        straw_center = get_bbox_center(straw["bbox"])
        ped_center = get_bbox_center(ped["bbox"])
        
        # Get strawberry color based on ripeness
        straw_color = CATEGORIES[straw["category_id"]]["color"]
        ped_color = CATEGORIES[3]["color"]
        
        # Draw bboxes
        x, y, w, h = [int(v) for v in straw["bbox"]]
        cv2.rectangle(result, (x, y), (x+w, y+h), straw_color, 2)
        
        x, y, w, h = [int(v) for v in ped["bbox"]]
        cv2.rectangle(result, (x, y), (x+w, y+h), ped_color, 2)
        
        # Draw connection line
        cv2.line(result, straw_center, ped_center, (255, 255, 255), 2)
        cv2.circle(result, straw_center, 5, straw_color, -1)
        cv2.circle(result, ped_center, 5, ped_color, -1)
    
    return result


# Visualize matching
matching_viz = visualize_matching(DATASET_PATH, image_id=0)
plt.figure(figsize=(12, 12))
plt.imshow(matching_viz)
plt.title("Strawberry-Peduncle Matching")
plt.axis('off')
plt.show()

# %% [markdown]
# ## 9. Training Integration Examples
# 
# Below are code snippets for integrating this dataset with popular frameworks.

# %% [markdown]
# ### 9.1 YOLO Format
# 
# Labels are already in YOLO format in `labels/*.txt`:
# 
# ```python
# from ultralytics import YOLO
# 
# # Create data.yaml
# data_yaml = """
# path: ./strawberry_dataset
# train: images
# val: images
# 
# names:
#   0: strawberry_ripe
#   1: strawberry_unripe
#   2: strawberry_half_ripe
#   3: peduncle
# """
# 
# # Train YOLO
# model = YOLO('yolo11n-seg.pt')
# model.train(data='data.yaml', epochs=100, imgsz=1024)
# ```

# %% [markdown]
# ### 9.2 PyTorch Dataset Class
# 
# ```python
# import torch
# from torch.utils.data import Dataset
# from PIL import Image
# import json
# 
# class StrawberryDataset(Dataset):
#     def __init__(self, dataset_path, transform=None):
#         self.dataset_path = Path(dataset_path)
#         self.transform = transform
#         
#         with open(self.dataset_path / "annotations.json") as f:
#             data = json.load(f)
#         
#         self.images = data["images"]
#         self.annotations = data["annotations"]
#         
#         # Group annotations by image_id
#         self.img_to_anns = {}
#         for ann in self.annotations:
#             img_id = ann["image_id"]
#             if img_id not in self.img_to_anns:
#                 self.img_to_anns[img_id] = []
#             self.img_to_anns[img_id].append(ann)
#     
#     def __len__(self):
#         return len(self.images)
#     
#     def __getitem__(self, idx):
#         img_info = self.images[idx]
#         img_path = self.dataset_path / "images" / img_info["file_name"]
#         image = Image.open(img_path).convert("RGB")
#         
#         annotations = self.img_to_anns.get(img_info["id"], [])
#         
#         # Convert to tensors
#         boxes = []
#         labels = []
#         for ann in annotations:
#             x, y, w, h = ann["bbox"]
#             boxes.append([x, y, x + w, y + h])  # xyxy format
#             labels.append(ann["category_id"])
#         
#         target = {
#             "boxes": torch.tensor(boxes, dtype=torch.float32),
#             "labels": torch.tensor(labels, dtype=torch.int64)
#         }
#         
#         if self.transform:
#             image = self.transform(image)
#         
#         return image, target
# ```

# %% [markdown]
# ### 9.3 Detectron2 (COCO Format)
# 
# ```python
# from detectron2.data import DatasetCatalog, MetadataCatalog
# from detectron2.data.datasets import load_coco_json
# 
# # Register dataset
# DatasetCatalog.register(
#     "strawberry_train",
#     lambda: load_coco_json(
#         "strawberry_dataset/annotations.json",
#         "strawberry_dataset/images"
#     )
# )
# 
# MetadataCatalog.get("strawberry_train").set(
#     thing_classes=["strawberry_ripe", "strawberry_unripe", 
#                    "strawberry_half_ripe", "peduncle"]
# )
# ```

# %% [markdown]
# ## 10. Self-Tests
# 
# Verify all functions work correctly.

# %%
def run_tests():
    """Run all self-tests to verify functions work correctly."""
    print("=" * 60)
    print("RUNNING SELF-TESTS")
    print("=" * 60)
    
    errors = []
    
    # Test 1: Dataset exploration
    print("\n✓ Test 1: explore_dataset()")
    try:
        stats = explore_dataset(DATASET_PATH)
        assert stats["images_count"] > 0, "No images found"
        assert stats["annotations"] is not None, "Annotations not loaded"
        print(f"   Found {stats['images_count']} images, {stats['total_annotations']} annotations")
    except Exception as e:
        errors.append(f"Test 1 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 2: Load annotations
    print("\n✓ Test 2: load_annotations()")
    try:
        data, images, annotations = load_annotations(DATASET_PATH)
        assert len(images) > 0, "No images loaded"
        assert len(annotations) > 0, "No annotations loaded"
        print(f"   Loaded {len(images)} images, {len(annotations)} annotations")
    except Exception as e:
        errors.append(f"Test 2 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 3: Get annotations for image
    print("\n✓ Test 3: get_annotations_for_image()")
    try:
        img_anns = get_annotations_for_image(annotations, 0)
        assert len(img_anns) > 0, "No annotations for image 0"
        print(f"   Image 0 has {len(img_anns)} annotations")
    except Exception as e:
        errors.append(f"Test 3 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 4: Draw bboxes
    print("\n✓ Test 4: draw_bboxes()")
    try:
        img = np.array(Image.open(DATASET_PATH / "images" / "00000.png").convert("RGB"))
        result = draw_bboxes(img, img_anns)
        assert result.shape == img.shape, "Output shape mismatch"
        print(f"   Drawing successful, output shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 4 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 5: Extract instance mask
    print("\n✓ Test 5: extract_instance_mask()")
    try:
        mask_img = np.array(Image.open(DATASET_PATH / "masks" / "00000.png"))
        seg_color = img_anns[0]["segmentation_color"]
        instance_mask = extract_instance_mask(mask_img, seg_color)
        assert instance_mask.shape[:2] == mask_img.shape[:2], "Mask shape mismatch"
        print(f"   Mask extracted, non-zero pixels: {np.sum(instance_mask > 0)}")
    except Exception as e:
        errors.append(f"Test 5 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 6: Mask overlay
    print("\n✓ Test 6: visualize_masks_overlay()")
    try:
        result = visualize_masks_overlay(img, mask_img, img_anns)
        assert result.shape == img.shape, "Overlay shape mismatch"
        print(f"   Overlay created, shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 6 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 7: Load depth
    print("\n✓ Test 7: load_depth_png()")
    try:
        depth = load_depth_png(DATASET_PATH / "depth" / "00000.png")
        assert depth.shape == (1024, 1024), f"Unexpected depth shape: {depth.shape}"
        valid = depth[depth > 0]
        print(f"   Depth loaded, range: {valid.min():.2f}m - {valid.max():.2f}m")
    except Exception as e:
        errors.append(f"Test 7 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 8: Depth visualization
    print("\n✓ Test 8: visualize_depth()")
    try:
        depth_viz = visualize_depth(depth)
        assert depth_viz.shape == (1024, 1024, 3), f"Unexpected viz shape: {depth_viz.shape}"
        print(f"   Depth visualization created, shape: {depth_viz.shape}")
    except Exception as e:
        errors.append(f"Test 8 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 9: Camera intrinsics
    print("\n✓ Test 9: get_intrinsics_matrix()")
    try:
        metadata = load_depth_metadata(DATASET_PATH)
        first_key = list(metadata.keys())[0]
        K = get_intrinsics_matrix(metadata[first_key])
        assert K.shape == (3, 3), f"Unexpected K shape: {K.shape}"
        print(f"   Intrinsics matrix shape: {K.shape}")
    except Exception as e:
        errors.append(f"Test 9 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 10: Pixel to 3D
    print("\n✓ Test 10: pixel_to_3d()")
    try:
        point_3d = pixel_to_3d(512, 512, 1.0, K)
        assert len(point_3d) == 3, f"Unexpected point shape: {point_3d.shape}"
        print(f"   3D point at center (1m depth): {point_3d}")
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
    
    # Test 12: Full bbox visualization
    print("\n✓ Test 12: visualize_bboxes()")
    try:
        result = visualize_bboxes(DATASET_PATH, 0)
        assert result.shape[2] == 3, "Expected RGB output"
        print(f"   Visualization shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 12 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 13: Full mask visualization
    print("\n✓ Test 13: visualize_masks()")
    try:
        result = visualize_masks(DATASET_PATH, 0)
        assert result.shape[2] == 3, "Expected RGB output"
        print(f"   Visualization shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 13 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Test 14: Matching visualization
    print("\n✓ Test 14: visualize_matching()")
    try:
        result = visualize_matching(DATASET_PATH, 0)
        assert result.shape[2] == 3, "Expected RGB output"
        print(f"   Visualization shape: {result.shape}")
    except Exception as e:
        errors.append(f"Test 14 FAILED: {e}")
        print(f"   ❌ FAILED: {e}")
    
    # Summary
    print("\n" + "=" * 60)
    if errors:
        print(f"❌ {len(errors)} TESTS FAILED:")
        for err in errors:
            print(f"   - {err}")
    else:
        print("✅ ALL 14 TESTS PASSED!")
    print("=" * 60)
    
    return len(errors) == 0


# Run tests
all_passed = run_tests()

# %% [markdown]
# ## Summary
# 
# This notebook demonstrated:
# 
# 1. ✅ **Dataset structure** - Images, labels, depth, masks, annotations
# 2. ✅ **BBox visualization** - Drawing from COCO annotations
# 3. ✅ **Mask visualization** - Using segmentation_color to extract instances
# 4. ✅ **Depth analysis** - Loading PNG/NPY formats, colormap visualization
# 5. ✅ **Camera intrinsics** - Building K matrix, pixel-to-3D conversion
# 6. ✅ **Strawberry-Peduncle matching** - Using parent_id relationships
# 7. ✅ **Training integration** - YOLO, PyTorch, Detectron2 examples
# 
# All functions are tested and working on the sample dataset.
