import os
import json
import glob

def verify_dataset(dataset_path):
    print(f"Verifying dataset at: {dataset_path}")
    
    metadata_temp_path = os.path.join(dataset_path, "metadata_temp")
    master_metadata_path = os.path.join(dataset_path, "depth_metadata.json")
    annotations_path = os.path.join(dataset_path, "annotations.json")
    
    errors = []
    
    # 1. Check Metadata Temp Files
    temp_files = glob.glob(os.path.join(metadata_temp_path, "*_meta.json"))
    print(f"Found {len(temp_files)} temp metadata files.")
    
    # 2. Check Master Metadata File
    if not os.path.exists(master_metadata_path):
        errors.append("Master depth_metadata.json not found!")
        return errors
        
    try:
        with open(master_metadata_path, 'r') as f:
            master_meta = json.load(f)
        print(f"Master metadata contains {len(master_meta)} entries.")
    except json.JSONDecodeError as e:
        errors.append(f"Failed to parse master metadata: {e}")
        return errors

    # 3. Check Annotations File
    if not os.path.exists(annotations_path):
        errors.append("annotations.json not found!")
    else:
        try:
            with open(annotations_path, 'r') as f:
                ann_data = json.load(f)
            print(f"Annotations file contains {len(ann_data['images'])} images.")
            
            if len(ann_data['images']) != len(master_meta):
                errors.append(f"Mismatch: Annotations has {len(ann_data['images'])} images, but master metadata has {len(master_meta)} entries.")
        except json.JSONDecodeError as e:
            errors.append(f"Failed to parse annotations.json: {e}")

    # 4. Consistency Check
    # Check if every temp file is in master
    for temp_file in temp_files:
        filename = os.path.basename(temp_file)
        # 00001_meta.json -> 00001.png
        image_name = filename.replace("_meta.json", ".png")
        
        if image_name not in master_meta:
            errors.append(f"Temp file {filename} exists but {image_name} is NOT in master metadata!")
        else:
            # Verify content matches (checking depth_range as a proxy)
            with open(temp_file, 'r') as tf:
                t_data = json.load(tf)
                
            m_data = master_meta[image_name]
            
            # Simple check of min/max depth
            t_min = t_data['depth_range']['min_meters']
            m_min = m_data['depth_range']['min_meters']
            
            if abs(t_min - m_min) > 0.0001:
                errors.append(f"Data Mismatch for {image_name}: Temp min {t_min} != Master min {m_min}")

    # Check if every master entry has a temp file
    for image_name in master_meta:
        temp_filename = image_name.replace(".png", "_meta.json")
        temp_path = os.path.join(metadata_temp_path, temp_filename)
        
        if not os.path.exists(temp_path):
            errors.append(f"Entry {image_name} is in master metadata but {temp_filename} is missing from metadata_temp!")

    if not errors:
        print("\n✅ VERIFICATION SUCCESSFUL! All checks passed.")
    else:
        print("\n❌ VERIFICATION FAILED with errors:")
        for e in errors:
            print(f" - {e}")

if __name__ == "__main__":
    dataset_path = r"C:\Users\NeverGonnaGiveYouUp\Uniy_projects\LAST-Straw-dataset\LAST-Straw-dataset\strawberry_dataset_test"
    verify_dataset(dataset_path)
