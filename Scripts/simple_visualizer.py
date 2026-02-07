import cv2
import json
import os
import glob
import numpy as np
import random
import sys
import matplotlib.pyplot as plt

def visualize_dataset(dataset_dir):
    images_dir = os.path.join(dataset_dir, "images")
    annotations_path = os.path.join(dataset_dir, "annotations", "coco_annotations.json")
    
    if not os.path.exists(annotations_path):
        print(f"Error: Annotations not found at {annotations_path}")
        return

    print(f"Loading annotations from {annotations_path}...")
    with open(annotations_path, 'r') as f:
        coco = json.load(f)
    
    # Map image_id to filename
    img_map = {img['id']: img for img in coco['images']}
    
    # Map image_id to annotations
    ann_map = {}
    for ann in coco['annotations']:
        img_id = ann['image_id']
        if img_id not in ann_map:
            ann_map[img_id] = []
        ann_map[img_id].append(ann)

    # Get categories to display names
    cat_map = {cat['id']: cat['name'] for cat in coco['categories']}

    # Simple color map
    colors = [
        (255, 0, 0),    # Red
        (0, 255, 0),    # Green
        (0, 0, 255),    # Blue
        (255, 255, 0),  # Cyan
        (255, 0, 255),  # Magenta
        (0, 255, 255),  # Yellow
        (128, 0, 0),    # Dark Red
        (0, 128, 0),    # Dark Green
        (0, 0, 128)     # Dark Blue
    ]
    
    # Process standard images (skip depth/masks for now, just visualization)
    # Sort images by filename to be consistent
    image_ids = sorted(img_map.keys(), key=lambda k: img_map[k]['file_name'])
    
    # Create output dir
    output_dir = os.path.join(dataset_dir, "visualization")
    os.makedirs(output_dir, exist_ok=True)
    
    count = 0
    max_count = 10 # Limit to 10 for speed
    
    print(f"Visualizing up to {max_count} images to {output_dir}...")
    
    for img_id in image_ids:
        if count >= max_count:
            break
            
        img_info = img_map[img_id]
        filename = img_info['file_name']
        
        # Load image
        img_path = os.path.join(images_dir, filename)
        if not os.path.exists(img_path):
            print(f"Warning: Image {filename} not found.")
            continue
            
        img = cv2.imread(img_path)
        if img is None:
            continue
            
        # Draw overlay
        overlay = img.copy()
        
        anns = ann_map.get(img_id, [])
        
        for i, ann in enumerate(anns):
            cat_id = ann['category_id']
            color = colors[cat_id % len(colors)]
            
            # --- Draw Mask (Alpha 0.3) ---
            if 'segmentation' in ann and ann['segmentation']:
                # COCO segmentation is list of polygons
                for poly in ann['segmentation']:
                    pts = np.array(poly).reshape((-1, 1, 2)).astype(np.int32)
                    cv2.fillPoly(overlay, [pts], color)
            
            # --- Draw BBox (Opaque) ---
            if 'bbox' in ann:
                x, y, w, h = [int(v) for v in ann['bbox']]
                # Draw plain rectangle (no alpha)
                cv2.rectangle(img, (x, y), (x + w, y + h), color, 2)
                
                # --- Text Label (ID) ---
                label = f"ID:{ann['id']} {cat_map.get(cat_id, '?')}"
                cv2.putText(img, label, (x, y - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
                cv2.putText(overlay, label, (x, y - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)

        # Blend overlay
        alpha = 0.3
        cv2.addWeighted(overlay, alpha, img, 1 - alpha, 0, img)
        
        # Save
        out_path = os.path.join(output_dir, f"vis_{filename}")
        cv2.imwrite(out_path, img)
        print(f"Saved {out_path}")
        count += 1
        
    print("Done!")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        dataset_path = sys.argv[1]
    else:
        # Default assumption
        dataset_path = "strawberry_dataset"
        
    visualize_dataset(dataset_path)
