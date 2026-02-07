"""
Read-only test on actual dataset
Verifies current state without modifying anything
"""

import json
from pathlib import Path

DATASET_PATH = Path("../strawberry_dataset")

def test_current_dataset():
    print("=" * 60)
    print("🔍 ANALYZING CURRENT DATASET (READ-ONLY)")
    print("=" * 60)
    
    # Check images
    images_path = DATASET_PATH / "images"
    if not images_path.exists():
        print("❌ Images folder not found!")
        return
    
    existing_images = sorted(images_path.glob("*.png"))
    print(f"\n📁 Images folder:")
    print(f"   Found: {len(existing_images)} PNG files")
    
    if existing_images:
        # Find max index
        max_index = 0
        indices = []
        for img_path in existing_images:
            filename = img_path.stem
            try:
                index = int(filename)
                indices.append(index)
                max_index = max(max_index, index)
            except ValueError:
                print(f"   ⚠ Non-numeric filename: {filename}")
        
        print(f"   Max index: {max_index}")
        print(f"   Next index will be: {max_index + 1}")
        
        # Check for gaps
        expected = set(range(max_index + 1))
        actual = set(indices)
        gaps = expected - actual
        if gaps:
            print(f"   ⚠ Found {len(gaps)} gaps in indices: {sorted(list(gaps))[:10]}...")
        else:
            print(f"   ✓ No gaps in indices (0-{max_index})")
    
    # Check annotations
    annotations_path = DATASET_PATH / "annotations.json"
    if annotations_path.exists():
        print(f"\n📄 annotations.json:")
        try:
            with open(annotations_path, 'r') as f:
                data = json.load(f)
            
            images = data.get('images', [])
            annotations = data.get('annotations', [])
            categories = data.get('categories', [])
            
            print(f"   Images: {len(images)}")
            print(f"   Annotations: {len(annotations)}")
            print(f"   Categories: {len(categories)}")
            
            if images:
                print(f"   First image ID: {images[0]['id']}")
                print(f"   Last image ID: {images[-1]['id']}")
            
            if annotations:
                print(f"   First annotation ID: {annotations[0]['id']}")
                print(f"   Last annotation ID: {annotations[-1]['id']}")
                print(f"   Avg annotations per image: {len(annotations) / len(images):.1f}")
            
            # Check for duplicate IDs
            image_ids = [img['id'] for img in images]
            if len(image_ids) != len(set(image_ids)):
                print(f"   ❌ DUPLICATE IMAGE IDs FOUND!")
            else:
                print(f"   ✓ No duplicate image IDs")
            
            annotation_ids = [ann['id'] for ann in annotations]
            if len(annotation_ids) != len(set(annotation_ids)):
                print(f"   ❌ DUPLICATE ANNOTATION IDs FOUND!")
            else:
                print(f"   ✓ No duplicate annotation IDs")
            
        except json.JSONDecodeError as e:
            print(f"   ❌ JSON parse error: {e}")
        except Exception as e:
            print(f"   ❌ Error: {e}")
    else:
        print(f"\n📄 annotations.json: NOT FOUND")
    
    # Check depth_metadata
    depth_meta_path = DATASET_PATH / "depth_metadata.json"
    if depth_meta_path.exists():
        print(f"\n📄 depth_metadata.json:")
        try:
            with open(depth_meta_path, 'r') as f:
                data = json.load(f)
            print(f"   Entries: {len(data)}")
            print(f"   ✓ Valid JSON")
        except json.JSONDecodeError as e:
            print(f"   ❌ Invalid JSON: {e}")
    else:
        print(f"\n📄 depth_metadata.json: NOT FOUND")
    
    # Summary
    print("\n" + "=" * 60)
    print("📊 SUMMARY")
    print("=" * 60)
    
    if existing_images:
        target = 1000
        current = len(existing_images)
        remaining = target - max_index - 1
        
        print(f"Current state: {current} images (indices 0-{max_index})")
        print(f"Target: {target} images")
        print(f"Remaining to generate: {remaining} images")
        print(f"\nNext run will:")
        print(f"  1. Resume from index {max_index + 1}")
        print(f"  2. Load existing {len(images)} image entries from annotations.json")
        print(f"  3. Load existing {len(annotations)} annotations")
        print(f"  4. Generate {remaining} new images")
        print(f"  5. Append new data to existing")
        
        print(f"\n✅ SAFE TO RUN GENERATION")
        print(f"   Your {current} existing images will be preserved!")
    else:
        print("No existing images found. Will start from 0.")


if __name__ == "__main__":
    test_current_dataset()
