import os
import re

texture_dir = r"e:\unity\전자오락원정대\Assets\Cainos\Pixel Art Top Down - Basic\Texture"
prefab_path = r"e:\unity\전자오락원정대\Assets\Cainos\Pixel Art Top Down - Basic\Tile Palette\TP Grass.prefab"

# 1. Extract GUIDs from prefab
with open(prefab_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Match entries like: - m_RefCount: 1\n    m_Data: {fileID: 11400000, guid: 780dbc86f9d96cb4ea6eb80cedc5297f, type: 2}
guids = re.findall(r'guid: ([a-f0-9]{32}), type: 2', content)

print(f"Found {len(guids)} Tile GUIDs in prefab.")

# 2. Get list of tile meta files in texture dir
# We sort them to match the expected order if they were added sequentially
tile_files = [f for f in os.listdir(texture_dir) if f.startswith("TX Tileset Grass") and f.endswith(".asset.meta")]

# Sort numerically to be safe (Grass 0, Grass 1, ..., Grass 10...)
def try_int(s):
    try: return int(s)
    except: return s

def sort_key(s):
    return [try_int(c) for c in re.split('([0-9]+)', s)]

tile_files.sort(key=sort_key)
print(f"Found {len(tile_files)} Tile meta files in texture dir.")

# 3. Perform mapping
for i, meta_file in enumerate(tile_files):
    if i >= len(guids):
        break
    
    old_guid = guids[i]
    meta_path = os.path.join(texture_dir, meta_file)
    
    with open(meta_path, 'r', encoding='utf-8') as f:
        meta_content = f.read()
    
    # Replace the current guid with the old one
    new_meta_content = re.sub(r'guid: [a-f0-9]{32}', f'guid: {old_guid}', meta_content)
    
    with open(meta_path, 'w', encoding='utf-8') as f:
        f.write(new_meta_content)
    
    print(f"Mapped {meta_file} -> {old_guid}")

print("GUID repair complete!")
