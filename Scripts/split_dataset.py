import os

def split_file(file_path, chunk_size=2 * 1024 * 1024 * 1024 - 100 * 1024 * 1024): # Slightly less than 2GB
    if not os.path.exists(file_path):
        print(f"File {file_path} not found.")
        return

    file_size = os.path.getsize(file_path)
    print(f"Splitting {file_path} ({file_size / (1024*1024*1024):.2f} GB)...")

    part_num = 1
    with open(file_path, 'rb') as f:
        while True:
            chunk = f.read(chunk_size)
            if not chunk:
                break
            
            part_name = f"{file_path}.{part_num:03d}"
            print(f"Writing {part_name} ({len(chunk) / (1024*1024):.2f} MB)...")
            with open(part_name, 'wb') as part_file:
                part_file.write(chunk)
            
            part_num += 1
    
    print("Done!")

if __name__ == "__main__":
    split_file("strawberry_dataset.zip")
