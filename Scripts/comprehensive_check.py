"""
Comprehensive verification script for strawberry dataset.
Checks:
1. Color encoding correctness (R/G/B channels)
2. Bijection property (unique colors for unique objects)
3. JSON annotation structure
4. Paired object consistency (strawberry + peduncle)
5. Pixel threshold application
"""
import cv2
import numpy as np
import json
from collections import defaultdict

def verify_dataset(sample_id=1):
    print("=" * 80)
    print("COMPREHENSIVE DATASET VERIFICATION")
    print("=" * 80)
    
    # Load data
    mask = cv2.imread(f"strawberry_dataset/masks/{sample_id:05d}.png")
    with open("strawberry_dataset/annotations.json", "r") as f:
        coco = json.load(f)
    
    anns = [a for a in coco["annotations"] if a["image_id"] == sample_id]
    
    print(f"\n📁 Sample ID: {sample_id}")
    print(f"   Mask shape: {mask.shape}")
    print(f"   Annotations: {len(anns)}")
    
    # ========================================================================
    # VERIFICATION 1: Color Encoding
    # ========================================================================
    print("\n" + "=" * 80)
    print("✓ VERIFICATION 1: RGB Color Encoding")
    print("=" * 80)
    
    color_map = {}
    object_pixels = defaultdict(int)
    
    for y in range(mask.shape[0]):
        for x in range(mask.shape[1]):
            b, g, r = mask[y, x]
            
            if b == 0 and g == 0 and r == 0:
                continue  # Skip background
            
            color_tuple = (int(b), int(g), int(r))
            
            # Decode object info
            if b == 3:  # Peduncle
                obj_id = g
                obj_type = "peduncle"
            elif b in [0, 1, 2]:  # Strawberry
                obj_id = r
                obj_type = ["ripe", "unripe", "half_ripe"][b]
            else:
                print(f"⚠️ INVALID category: B={b} at ({x}, {y})")
                continue
            
            object_key = (obj_type, obj_id)
            object_pixels[object_key] += 1
            
            if color_tuple not in color_map:
                color_map[color_tuple] = object_key
            elif color_map[color_tuple] != object_key:
                print(f"❌ BIJECTION VIOLATION: Color {color_tuple} maps to both {color_map[color_tuple]} and {object_key}")
    
    print(f"\n✅ Unique colors in mask: {len(color_map)}")
    print(f"✅ Unique objects: {len(object_pixels)}")
    
    # Check for bijection
    if len(color_map) == len(object_pixels):
        print("✅ BIJECTION CONFIRMED: Each color maps to exactly one object")
    else:
        print(f"❌ BIJECTION BROKEN: {len(color_map)} colors for {len(object_pixels)} objects")
    
    # ========================================================================
    # VERIFICATION 2: Color Channel Usage
    # ========================================================================
    print("\n" + "=" * 80)
    print("✓ VERIFICATION 2: Channel Usage Analysis")
    print("=" * 80)
    
    print("\nSample colors (first 10):")
    for i, (color, obj) in enumerate(list(color_map.items())[:10]):
        b, g, r = color
        obj_type, obj_id = obj
        print(f"  {i+1}. BGR=({b:3d}, {g:3d}, {r:3d}) → {obj_type:10s} ID={obj_id:3d}")
    
    # Verify channel separation
    strawberry_colors = [(b, g, r) for (b, g, r), (t, _) in color_map.items() if t in ["ripe", "unripe", "half_ripe"]]
    peduncle_colors = [(b, g, r) for (b, g, r), (t, _) in color_map.items() if t == "peduncle"]
    
    print(f"\n📊 Channel usage:")
    print(f"   Strawberries ({len(strawberry_colors)} colors):")
    if strawberry_colors:
        g_vals = [g for b, g, r in strawberry_colors]
        r_vals = [r for b, g, r in strawberry_colors]
        print(f"     - G channel: min={min(g_vals)}, max={max(g_vals)} (should be 0)")
        print(f"     - R channel: min={min(r_vals)}, max={max(r_vals)} (IDs)")
        if max(g_vals) > 0:
            print(f"     ❌ WARNING: Strawberries use G channel (should be 0)!")
    
    print(f"   Peduncles ({len(peduncle_colors)} colors):")
    if peduncle_colors:
        g_vals = [g for b, g, r in peduncle_colors]
        r_vals = [r for b, g, r in peduncle_colors]
        print(f"     - G channel: min={min(g_vals)}, max={max(g_vals)} (IDs)")
        print(f"     - R channel: min={min(r_vals)}, max={max(r_vals)} (should be 0)")
        if max(r_vals) > 0:
            print(f"     ❌ WARNING: Peduncles use R channel (should be 0)!")
    
    # ========================================================================
    # VERIFICATION 3: JSON Annotation Structure
    # ========================================================================
    print("\n" + "=" * 80)
    print("✓ VERIFICATION 3: JSON Annotations")
    print("=" * 80)
    
    # Group by instance_id
    by_instance = defaultdict(list)
    for ann in anns:
        by_instance[ann["instance_id"]].append(ann)
    
    print(f"\nUnique instance IDs in JSON: {len(by_instance)}")
    
    # Check for paired objects
    paired_count = 0
    strawberry_only = 0
    peduncle_only = 0
    
    for inst_id, objs in by_instance.items():
        cats = [o["category_id"] for o in objs]
        has_strawberry = any(c in [0, 1, 2] for c in cats)
        has_peduncle = 3 in cats
        
        if has_strawberry and has_peduncle:
            paired_count += 1
        elif has_strawberry:
            strawberry_only += 1
        elif has_peduncle:
            peduncle_only += 1
    
    print(f"\n📊 Object pairing:")
    print(f"   ✅ Paired (strawberry + peduncle): {paired_count}")
    print(f"   ⚠️ Strawberry only: {strawberry_only}")
    print(f"   ⚠️ Peduncle only: {peduncle_only}")
    
    # ========================================================================
    # VERIFICATION 4: Pixel Threshold
    # ========================================================================
    print("\n" + "=" * 80)
    print("✓ VERIFICATION 4: Pixel Threshold (15 px)")
    print("=" * 80)
    
    json_objects = set()
    for ann in anns:
        if ann["category_id"] == 3:
            json_objects.add(("peduncle", ann["instance_id"]))
        else:
            cat_name = ["ripe", "unripe", "half_ripe"][ann["category_id"]]
            json_objects.add((cat_name, ann["instance_id"]))
    
    print(f"\nObjects in JSON: {len(json_objects)}")
    print(f"Objects in Mask: {len(object_pixels)}")
    print(f"Filtered out: {len(object_pixels) - len(json_objects)}")
    
    # Check if all JSON objects meet threshold
    violations = []
    for ann in anns:
        area = ann["area"]
        if area < 15:
            violations.append((ann["instance_id"], ann["category_id"], area))
    
    if violations:
        print(f"\n❌ THRESHOLD VIOLATIONS ({len(violations)} objects < 15px in JSON):")
        for inst_id, cat_id, area in violations[:5]:
            print(f"   ID {inst_id}, cat {cat_id}: {area} px")
    else:
        print(f"\n✅ All JSON objects meet 15px threshold")
    
    # Check filtered objects
    missing = []
    for (obj_type, obj_id), px_count in sorted(object_pixels.items(), key=lambda x: x[1])[:10]:
        if (obj_type, obj_id) not in json_objects:
            missing.append((obj_type, obj_id, px_count))
    
    if missing:
        print(f"\n✅ Sample filtered objects (< 15px, correctly excluded):")
        for obj_type, obj_id, px_count in missing:
            print(f"   {obj_type:10s} ID {obj_id:3d}: {px_count:3d} px")
    
    # ========================================================================
    # VERIFICATION 5: ID Offset
    # ========================================================================
    print("\n" + "=" * 80)
    print("✓ VERIFICATION 5: ID Offset (no ID=0)")
    print("=" * 80)
    
    all_ids = [obj_id for obj_type, obj_id in object_pixels.keys()]
    min_id = min(all_ids)
    max_id = max(all_ids)
    
    print(f"\nID range: {min_id} - {max_id}")
    
    if min_id == 0:
        print(f"❌ FOUND ID=0 (collision with background!)")
    elif min_id == 1:
        print(f"✅ IDs start from 1 (background collision avoided)")
    else:
        print(f"⚠️ IDs start from {min_id} (unusual)")
    
    # Check for ID 0 specifically
    has_zero = any(obj_id == 0 for obj_type, obj_id in object_pixels.keys())
    if has_zero:
        print(f"❌ CRITICAL: Found objects with ID=0")
        zero_objs = [(t, i) for (t, i) in object_pixels.keys() if i == 0]
        print(f"   Objects: {zero_objs}")
    else:
        print(f"✅ No ID=0 found")
    
    # ========================================================================
    # FINAL SUMMARY
    # ========================================================================
    print("\n" + "=" * 80)
    print("FINAL SUMMARY")
    print("=" * 80)
    
    checks = {
        "RGB Encoding": len(color_map) == len(object_pixels),
        "Bijection": len(color_map) == len(object_pixels),
        "Channel Separation": max([g for b, g, r in strawberry_colors] if strawberry_colors else [0]) == 0 and max([r for b, g, r in peduncle_colors] if peduncle_colors else [0]) == 0,
        "Threshold Applied": len(violations) == 0,
        "ID Offset": min_id >= 1 and not has_zero,
    }
    
    print()
    for check, passed in checks.items():
        status = "✅ PASS" if passed else "❌ FAIL"
        print(f"  {status}: {check}")
    
    all_passed = all(checks.values())
    print("\n" + "=" * 80)
    if all_passed:
        print("🎉 ALL CHECKS PASSED - SYSTEM VERIFIED")
    else:
        print("⚠️ SOME CHECKS FAILED - REVIEW NEEDED")
    print("=" * 80)

if __name__ == "__main__":
    verify_dataset(1)
