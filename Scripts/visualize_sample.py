"""
Visualization script for strawberry dataset.
Decodes masks with NEW encoding: R=StrawberryID, G=PeduncleID, B=CategoryID
Draws BBoxes from JSON (green) and from mask pixels (blue) for comparison.
"""
import json
import cv2
import numpy as np
import os
import argparse

def visualize_sample(sample_idx=1):
    # Paths
    dataset_root = "strawberry_dataset"
    img_path = os.path.join(dataset_root, "images", f"{sample_idx:05d}.png")
    mask_path = os.path.join(dataset_root, "masks", f"{sample_idx:05d}.png")
    ann_path = os.path.join(dataset_root, "annotations.json")
    output_path = f"sample_vis_check_{sample_idx}.png"
    
    # Load data
    if not os.path.exists(img_path):
        print(f"❌ Image not found: {img_path}")
        return
    
    if not os.path.exists(mask_path):
        print(f"❌ Mask not found: {mask_path}")
        return
        
    print(f"Loading Sample {sample_idx}...")
    image = cv2.imread(img_path)
    mask = cv2.imread(mask_path)
    
    with open(ann_path, 'r') as f:
        coco = json.load(f)
    
    # Find image ID
    target_filename = f"{sample_idx:05d}.png"
    image_info = next((img for img in coco['images'] if img['file_name'] == target_filename), None)
    
    if not image_info:
        print(f"❌ No image info for {target_filename}")
        return
    
    image_id = image_info['id']
    anns = [a for a in coco['annotations'] if a['image_id'] == image_id]
    
    print(f"Image ID: {image_id} | Annotations: {len(anns)}")
    
    # Parse mask into instance map
    # BGR format: B=Category, G=PeduncleID, R=StrawberryID
    mask_b = mask[:, :, 0]  # Category
    mask_g = mask[:, :, 1]  # Peduncle ID
    mask_r = mask[:, :, 2]  # Strawberry ID
    
    # Create instance ID map: use R for strawberries, G for peduncles
    instance_map = np.where(mask_b == 3, mask_g, mask_r).astype(np.int32)
    
    unique_ids = np.unique(instance_map)
    unique_ids = unique_ids[unique_ids > 0]  # Remove background
    
    json_ids = sorted([a['instance_id'] for a in anns])
    
    print(f"\n📊 Statistics:")
    print(f"  IDs in JSON: {json_ids}")
    print(f"  IDs in Mask: {sorted(unique_ids.tolist())}")
    
    # Visualize
    overlay = image.copy()
    
    for ann in anns:
        inst_id = ann['instance_id']
        cat_id = ann['category_id']
        
        # JSON BBox (Green)
        jx, jy, jw, jh = [int(v) for v in ann['bbox']]
        cv2.rectangle(image, (jx, jy), (jx + jw, jy + jh), (0, 255, 0), 2)
        
        # Mask BBox (Blue) - calculate from pixels
        mask_bool = (instance_map == inst_id)
        pixel_count = np.count_nonzero(mask_bool)
        
        if pixel_count > 0:
            rows, cols = np.where(mask_bool)
            y0, y1 = np.min(rows), np.max(rows)
            x0, x1 = np.min(cols), np.max(cols)
            
            cv2.rectangle(image, (x0, y0), (x1, y1), (255, 0, 0), 2)
            
            # Overlay colored pixels
            np.random.seed(inst_id)
            color = tuple(np.random.randint(50, 255, 3).tolist())
            overlay[mask_bool] = color
            
            # Calculate accuracy
            json_area = jw * jh
            mask_area = (x1 - x0 + 1) * (y1 - y0 + 1)
            ratio = pixel_count / json_area if json_area > 0 else 0
            
            cat_name = ["ripe", "unripe", "half", "ped"][cat_id]
            print(f"  ID {inst_id:3d} ({cat_name:6s}): Pixels={pixel_count:5d}, JSON_Area={json_area:5d}, Ratio={ratio:.2f}")
            
            # Label
            label = f"ID:{inst_id}"
            cv2.putText(image, label, (jx, jy - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 255, 0), 1)
        else:
            print(f"  ⚠️ ID {inst_id} in JSON but NOT in mask!")
    
    # Blend overlay
    cv2.addWeighted(overlay, 0.3, image, 0.7, 0, image)
    
    # Legend
    cv2.putText(image, "Green: JSON BBox | Blue: Mask BBox", (10, 30), 
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
    
    cv2.imwrite(output_path, image)
    print(f"\n✅ Saved: {output_path}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--sample", type=int, default=1, help="Sample index to visualize")
    args = parser.parse_args()
    
    visualize_sample(args.sample)
