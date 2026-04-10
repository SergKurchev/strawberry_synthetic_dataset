import numpy as np
from PIL import Image
import json

sample = 'multiview_dataset/sample_00000'
color_map = json.load(open(f'{sample}/color_map.json'))

mask = np.array(Image.open(f'{sample}/masks/00000.png').convert('RGB'))
print('Mask shape:', mask.shape, 'dtype:', mask.dtype)

r, g, b = mask[:,:,0], mask[:,:,1], mask[:,:,2]
straw = (r > 0)
print('Pixels where R>0:', np.sum(straw))

if np.sum(straw) > 0:
    unique_colors = np.unique(mask[straw].reshape(-1, 3), axis=0)
    print('Unique non-black colors in mask (first 20):')
    for c in unique_colors[:20]:
        print(f'  R={c[0]:3d} G={c[1]:3d} B={c[2]:3d}')

print('\nColor map expected colors:')
for k, v in color_map.items():
    c = v['color']
    print(f'  id={v["instance_id"]} cat={v["category_id"]} color=[{c[0]},{c[1]},{c[2]}]')
