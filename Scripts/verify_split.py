import os
import zipfile

def verify_split_files(base_name="strawberry_dataset.zip"):
    parts = []
    i = 1
    while True:
        part_name = f"{base_name}.{i:03d}"
        if os.path.exists(part_name):
            parts.append(part_name)
            i += 1
        else:
            break
    
    if not parts:
        print("No split files found!")
        return

    print(f"Found {len(parts)} parts: {parts}")
    
    output_test = "test_combined.zip"
    print(f"Recombining to {output_test} for verification...")
    
    with open(output_test, 'wb') as outfile:
        for part in parts:
            print(f"Reading {part}...")
            with open(part, 'rb') as infile:
                outfile.write(infile.read())
                
    print("Recombination complete.")
    
    if zipfile.is_zipfile(output_test):
        print("SUCCESS: The recombined file is a valid ZIP archive.")
        try:
            with zipfile.ZipFile(output_test, 'r') as zf:
                ret = zf.testzip()
                if ret is not None:
                     print(f"First bad file in zip: {ret}")
                else:
                    print("SUCCESS: Zip file content integrity check passed (testzip).")
        except Exception as e:
            print(f"ERROR: Could not open/test zip file: {e}")
    else:
        print("ERROR: The recombined file is NOT a valid ZIP archive.")
        
    # Clean up test file
    if os.path.exists(output_test):
        os.remove(output_test)
        print("Test file removed.")

if __name__ == "__main__":
    verify_split_files()
