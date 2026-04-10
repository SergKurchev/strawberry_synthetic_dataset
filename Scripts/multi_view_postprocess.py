#!/usr/bin/env python3
"""
multi_view_postprocess.py
=========================
Post-processing script for one Multi-View Sample.

Called by Unity after all frames are captured:
    python multi_view_postprocess.py <sample_path>

The script expects:
    <sample_path>/
        rgb/         00000.png … 00019.png
        depth/       00000.npy … 00019.npy
        masks/       00000.png … 00019.png
        labels/      00000.txt … 00019.txt   (YOLO — already written by Unity)
        cameras.json
        color_map.json

And produces:
    <sample_path>/
        annotations.json      – COCO-style per-frame segmentation
        pointcloud.ply        – merged RGB + segmentation point cloud
        visualization.html    – self-contained interactive 3-D viewer
"""

from __future__ import annotations

import json
import math
import os
import struct
import sys
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
from PIL import Image

# ── optional dependency for contour-based COCO masks ──────────────────────────
try:
    import cv2
    HAS_CV2 = True
except ImportError:
    HAS_CV2 = False
    print("[WARNING] cv2 not found – COCO segmentation polygons will use bbox fallback.")

# ─────────────────────────────────────────────────────────────────────────────
#  Helpers
# ─────────────────────────────────────────────────────────────────────────────

CATEGORY_NAMES = {0: "strawberry_ripe", 1: "strawberry_unripe", 2: "strawberry_half_ripe"}

# Visualisation colours per category (R,G,B 0-255)
SEG_PALETTE = {
    0:  (220,  60,  60),   # ripe        – warm red
    1:  ( 60, 180,  60),   # unripe      – green
    2:  (230, 150,  40),   # half-ripe   – orange
   -1:  (100, 100, 100),   # background
}


def load_json(path: Path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


# ─────────────────────────────────────────────────────────────────────────────
#  Step 1 – Mask PNG → COCO annotations
# ─────────────────────────────────────────────────────────────────────────────

def masks_to_coco(sample_path: Path, color_map: dict) -> dict:
    """
    Build a COCO-style annotation dict from the per-frame mask PNGs.

    color_map keys are instance_id strings; values contain:
        instance_id, category_id, ripeness, color=[R,G,B]
    """
    masks_dir = sample_path / "masks"
    rgb_dir   = sample_path / "rgb"

    # Build a lookup: (R,G,B) tuple → SegInfo dict
    color_to_info: Dict[Tuple[int,int,int], dict] = {}
    for _key, info in color_map.items():
        c = tuple(info["color"])          # (R, G, B)
        color_to_info[c] = info

    coco_images      = []
    coco_annotations = []
    ann_id           = 1

    mask_files = sorted(masks_dir.glob("*.png"))

    for frame_idx, mask_path in enumerate(mask_files):
        mask_img = np.array(Image.open(mask_path).convert("RGB"))
        H, W     = mask_img.shape[:2]

        # Corresponding RGB for image entry
        rgb_name = mask_path.name

        coco_images.append({
            "id":        frame_idx,
            "file_name": f"rgb/{rgb_name}",
            "width":     W,
            "height":    H,
        })

        # Find all unique non-black colours
        flat = mask_img.reshape(-1, 3)
        unique_colors = {tuple(c) for c in flat if any(c)}  # exclude (0,0,0)

        for color in unique_colors:
            if color not in color_to_info:
                continue   # unknown colour – skip
            info = color_to_info[color]

            # Binary mask for this instance
            instance_mask = np.all(mask_img == np.array(color), axis=2).astype(np.uint8)
            pixel_count   = int(instance_mask.sum())
            if pixel_count == 0:
                continue

            # Bounding box
            ys, xs = np.where(instance_mask)
            x1, y1 = int(xs.min()), int(ys.min())
            x2, y2 = int(xs.max()), int(ys.max())
            bw, bh = x2 - x1 + 1, y2 - y1 + 1

            # Segmentation polygon(s)
            if HAS_CV2:
                contours, _ = cv2.findContours(
                    instance_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
                segmentation = []
                for cnt in contours:
                    if len(cnt) < 3:
                        continue
                    poly = cnt.flatten().tolist()
                    if len(poly) >= 6:
                        segmentation.append(poly)
            else:
                # Fallback: encode as rectangle
                segmentation = [[x1, y1, x2, y1, x2, y2, x1, y2]]

            if not segmentation:
                continue

            coco_annotations.append({
                "id":            ann_id,
                "image_id":      frame_idx,
                "category_id":   info["category_id"],
                "instance_id":   info["instance_id"],
                "bbox":          [x1, y1, bw, bh],
                "area":          pixel_count,
                "segmentation":  segmentation,
                "ripeness":      info["ripeness"],
                "iscrowd":       0,
            })
            ann_id += 1

    coco_categories = [
        {"id": cid, "name": name}
        for cid, name in CATEGORY_NAMES.items()
    ]

    return {
        "images":      coco_images,
        "annotations": coco_annotations,
        "categories":  coco_categories,
    }


# ─────────────────────────────────────────────────────────────────────────────
#  Step 2 – Depth + Camera → merged point cloud (.ply)
# ─────────────────────────────────────────────────────────────────────────────

def build_point_cloud(sample_path: Path, cameras: list, color_map: dict) -> np.ndarray:
    """
    Project every valid depth pixel into world space and accumulate.

    Returns an (N, 8) float32 array with columns:
        x, y, z, r, g, b, instance_id, category_id
    where r,g,b ∈ [0,255] and instance_id=-1 means background.
    """
    depth_dir = sample_path / "depth"
    rgb_dir   = sample_path / "rgb"
    masks_dir = sample_path / "masks"

    # color → info lookup
    color_to_info: Dict[Tuple[int,int,int], dict] = {}
    for _k, info in color_map.items():
        color_to_info[tuple(info["color"])] = info

    all_points: List[np.ndarray] = []

    for cam_data in cameras:
        fi = cam_data["frame_index"]
        name = f"{fi:05d}"

        depth_path = depth_dir / (name + ".npy")
        rgb_path   = rgb_dir   / (name + ".png")
        mask_path  = masks_dir / (name + ".png")

        if not depth_path.exists() or not rgb_path.exists():
            continue

        depth = np.load(str(depth_path))          # (H, W) float32 metres
        rgb   = np.array(Image.open(rgb_path).convert("RGB"))   # (H,W,3) uint8
        mask  = np.array(Image.open(mask_path).convert("RGB")) if mask_path.exists() \
                else np.zeros_like(rgb)

        H, W = depth.shape
        intr  = cam_data["intrinsics"]
        fx, fy, cx, cy = intr["fx"], intr["fy"], intr["cx"], intr["cy"]

        # Camera → world rotation matrix from quaternion (x,y,z,w)
        qx, qy, qz, qw = cam_data["rotation"]
        R = quaternion_to_rotation_matrix(qx, qy, qz, qw)
        t = np.array(cam_data["position"], dtype=np.float32)

        # Build pixel grid
        u = np.arange(W, dtype=np.float32)
        v = np.arange(H, dtype=np.float32)
        uu, vv = np.meshgrid(u, v)            # (H,W)

        Z = depth                              # (H,W) metres
        valid = Z > 0.001                      # exclude zero/invalid

        X_cam = (uu - cx) * Z / fx
        Y_cam = (vv - cy) * Z / fy
        Z_cam = Z

        # Stack (H, W, 3) camera-space
        pts_cam = np.stack([X_cam, Y_cam, Z_cam], axis=-1)   # (H,W,3)

        # Transform to world space: P_world = R @ P_cam + t
        pts_world = pts_cam @ R.T + t          # broadcast (H,W,3)

        # Gather RGB
        r_ch = rgb[:, :, 0].astype(np.float32)
        g_ch = rgb[:, :, 1].astype(np.float32)
        b_ch = rgb[:, :, 2].astype(np.float32)

        # Segmentation per pixel
        inst_id_img  = np.full((H, W), -1, dtype=np.int32)
        cat_id_img   = np.full((H, W), -1, dtype=np.int32)

        mask_r = mask[:, :, 0]
        mask_g = mask[:, :, 1]
        mask_b = mask[:, :, 2]

        # Strawberry pixels: R > 0
        straw_mask = mask_r > 0
        for color, info in color_to_info.items():
            pixel_mask = straw_mask & \
                         (mask_r == color[0]) & \
                         (mask_g == color[1]) & \
                         (mask_b == color[2])
            inst_id_img[pixel_mask] = info["instance_id"]
            cat_id_img[pixel_mask]  = info["category_id"]

        # Flatten & filter valid
        flat_valid = valid.ravel()
        flat_pts   = pts_world.reshape(-1, 3)
        flat_r     = r_ch.ravel()
        flat_g     = g_ch.ravel()
        flat_b     = b_ch.ravel()
        flat_inst  = inst_id_img.ravel()
        flat_cat   = cat_id_img.ravel()

        chunk = np.column_stack([
            flat_pts[flat_valid],
            flat_r[flat_valid],
            flat_g[flat_valid],
            flat_b[flat_valid],
            flat_inst[flat_valid].astype(np.float32),
            flat_cat[flat_valid].astype(np.float32),
        ]).astype(np.float32)

        all_points.append(chunk)

    if not all_points:
        return np.zeros((0, 8), dtype=np.float32)

    return np.concatenate(all_points, axis=0)


def quaternion_to_rotation_matrix(qx: float, qy: float, qz: float, qw: float) -> np.ndarray:
    """Convert unit quaternion to 3×3 rotation matrix (row-major)."""
    q = np.array([qx, qy, qz, qw], dtype=np.float64)
    q /= np.linalg.norm(q) + 1e-12

    x, y, z, w = q
    R = np.array([
        [1 - 2*(y*y + z*z),     2*(x*y - z*w),     2*(x*z + y*w)],
        [    2*(x*y + z*w), 1 - 2*(x*x + z*z),     2*(y*z - x*w)],
        [    2*(x*z - y*w),     2*(y*z + x*w), 1 - 2*(x*x + y*y)],
    ], dtype=np.float32)
    return R


def save_ply(points: np.ndarray, path: Path):
    """
    Save (N,8) array as ASCII PLY with properties:
    x y z red green blue instance_id category_id
    """
    N = len(points)
    header = (
        "ply\n"
        "format ascii 1.0\n"
        f"element vertex {N}\n"
        "property float x\n"
        "property float y\n"
        "property float z\n"
        "property uchar red\n"
        "property uchar green\n"
        "property uchar blue\n"
        "property int instance_id\n"
        "property int category_id\n"
        "end_header\n"
    )
    with open(path, "w", encoding="ascii") as f:
        f.write(header)
        for p in points:
            x, y, z = p[0], p[1], p[2]
            r = int(np.clip(p[3], 0, 255))
            g = int(np.clip(p[4], 0, 255))
            b = int(np.clip(p[5], 0, 255))
            inst = int(p[6])
            cat  = int(p[7])
            f.write(f"{x:.6f} {y:.6f} {z:.6f} {r} {g} {b} {inst} {cat}\n")


# ─────────────────────────────────────────────────────────────────────────────
#  Step 3 – HTML viewer (Three.js, self-contained)
# ─────────────────────────────────────────────────────────────────────────────

def build_html_viewer(points: np.ndarray, out_path: Path, sample_name: str):
    """
    Generate a self-contained HTML file with an interactive 3-D point cloud viewer.

    Features:
    - Orbit / pan / zoom via Three.js OrbitControls
    - RGB mode: points coloured using real camera RGB
    - Seg mode: points coloured by category_id with legend
    """

    if len(points) == 0:
        print("[HTML] No points — writing empty viewer.")
        _write_empty_html(out_path, sample_name)
        return

    # Downsample for browser performance (max 500 k points)
    MAX_PTS = 500_000
    if len(points) > MAX_PTS:
        idx    = np.random.choice(len(points), MAX_PTS, replace=False)
        points = points[idx]

    # Encode as compact JS arrays
    xs  = points[:, 0].tolist()
    ys  = points[:, 1].tolist()
    zs  = points[:, 2].tolist()
    rs  = np.clip(points[:, 3], 0, 255).astype(int).tolist()
    gs  = np.clip(points[:, 4], 0, 255).astype(int).tolist()
    bs  = np.clip(points[:, 5], 0, 255).astype(int).tolist()
    cats = points[:, 7].astype(int).tolist()

    # Category colours for seg mode
    cat_colors_js = json.dumps({
        str(k): list(v) for k, v in SEG_PALETTE.items()
    })

    def arr_to_typed(lst, dtype="Float32Array", precision=4):
        if dtype == "Float32Array":
            return f"new Float32Array([{','.join(f'{v:.{precision}f}' for v in lst)}])"
        elif dtype == "Uint8Array":
            return f"new Uint8Array([{','.join(str(v) for v in lst)}])"
        elif dtype == "Int32Array":
            return f"new Int32Array([{','.join(str(v) for v in lst)}])"

    js_xs   = arr_to_typed(xs,  "Float32Array")
    js_ys   = arr_to_typed(ys,  "Float32Array")
    js_zs   = arr_to_typed(zs,  "Float32Array")
    js_rs   = arr_to_typed(rs,  "Uint8Array")
    js_gs   = arr_to_typed(gs,  "Uint8Array")
    js_bs   = arr_to_typed(bs,  "Uint8Array")
    js_cats = arr_to_typed(cats,"Int32Array")

    N = len(points)

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Point Cloud — {sample_name}</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{
    background: #0d1117;
    color: #e6edf3;
    font-family: 'Segoe UI', system-ui, sans-serif;
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
  }}
  #header {{
    padding: 10px 20px;
    background: #161b22;
    border-bottom: 1px solid #30363d;
    display: flex;
    align-items: center;
    gap: 12px;
    flex-shrink: 0;
  }}
  #header h1 {{
    font-size: 15px;
    font-weight: 600;
    color: #f0f6fc;
    flex: 1;
  }}
  #header span.meta {{
    font-size: 12px;
    color: #8b949e;
  }}
  #controls {{
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 20px;
    background: #161b22;
    border-bottom: 1px solid #30363d;
    flex-shrink: 0;
  }}
  .btn {{
    padding: 6px 16px;
    border-radius: 6px;
    border: 1px solid #30363d;
    background: #21262d;
    color: #e6edf3;
    font-size: 13px;
    cursor: pointer;
    transition: background 0.15s, border-color 0.15s;
  }}
  .btn:hover {{ background: #30363d; }}
  .btn.active {{
    background: #1f6feb;
    border-color: #388bfd;
    color: #fff;
  }}
  #legend {{
    display: flex;
    gap: 12px;
    margin-left: auto;
    align-items: center;
  }}
  .leg-item {{
    display: flex;
    align-items: center;
    gap: 5px;
    font-size: 12px;
    color: #8b949e;
  }}
  .leg-dot {{
    width: 10px;
    height: 10px;
    border-radius: 50%;
  }}
  #canvas-container {{ flex: 1; position: relative; }}
  canvas {{ width: 100% !important; height: 100% !important; display: block; }}
  #info {{
    position: absolute;
    bottom: 12px;
    left: 16px;
    font-size: 11px;
    color: #484f58;
    pointer-events: none;
  }}
</style>
</head>
<body>

<div id="header">
  <h1>🍓 Point Cloud Viewer — {sample_name}</h1>
  <span class="meta">{N:,} points</span>
</div>

<div id="controls">
  <button class="btn active" id="btnRGB"  onclick="setMode('rgb')">RGB</button>
  <button class="btn"        id="btnSeg"  onclick="setMode('seg')">Segmentation</button>

  <div id="legend" style="display:none">
    <div class="leg-item">
      <div class="leg-dot" style="background:rgb(220,60,60)"></div> Ripe
    </div>
    <div class="leg-item">
      <div class="leg-dot" style="background:rgb(60,180,60)"></div> Unripe
    </div>
    <div class="leg-item">
      <div class="leg-dot" style="background:rgb(230,150,40)"></div> Half-ripe
    </div>
    <div class="leg-item">
      <div class="leg-dot" style="background:rgb(100,100,100)"></div> Background
    </div>
  </div>
</div>

<div id="canvas-container">
  <div id="info">Scroll to zoom · Left drag to orbit · Right drag to pan</div>
</div>

<!-- Three.js + OrbitControls from CDN -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script>
// ── Inline OrbitControls (r128 compatible, minified subset) ─────────────────
// We embed a minimal OrbitControls to keep the file self-contained when offline.
// Full version loaded via additional script tag below.
</script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>

<script>
"use strict";

// ── Point data ───────────────────────────────────────────────────────────────
const XS   = {js_xs};
const YS   = {js_ys};
const ZS   = {js_zs};
const RS   = {js_rs};
const GS   = {js_gs};
const BS   = {js_bs};
const CATS = {js_cats};
const N    = XS.length;

const CAT_COLORS = {cat_colors_js};

// ── Three.js setup ───────────────────────────────────────────────────────────
const container = document.getElementById('canvas-container');
const renderer  = new THREE.WebGLRenderer({{ antialias: false }});
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(container.clientWidth, container.clientHeight);
container.appendChild(renderer.domElement);

const scene  = new THREE.Scene();
scene.background = new THREE.Color(0x0d1117);

const camera = new THREE.PerspectiveCamera(
  60, container.clientWidth / container.clientHeight, 0.001, 100);
camera.position.set(0, 0.5, 1.5);

const controls = new THREE.OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.07;
controls.screenSpacePanning = true;

// ── Build geometry ───────────────────────────────────────────────────────────
let currentMode = 'rgb';

const geometry = new THREE.BufferGeometry();
geometry.setAttribute('position', new THREE.BufferAttribute(
  new Float32Array(N * 3), 3));
geometry.setAttribute('color',    new THREE.BufferAttribute(
  new Float32Array(N * 3), 3));

const posArr = geometry.attributes.position.array;
const colArr = geometry.attributes.color.array;

// Fill positions (flip Y because Unity is Y-up but we want the bush "up")
for (let i = 0; i < N; i++) {{
  posArr[i*3]     = XS[i];
  posArr[i*3 + 1] = YS[i];
  posArr[i*3 + 2] = ZS[i];
}}

function applyColors(mode) {{
  for (let i = 0; i < N; i++) {{
    let r, g, b;
    if (mode === 'rgb') {{
      r = RS[i] / 255;
      g = GS[i] / 255;
      b = BS[i] / 255;
    }} else {{
      const cat = CATS[i];
      const key = cat.toString();
      const col = CAT_COLORS[key] || CAT_COLORS['-1'];
      r = col[0] / 255;
      g = col[1] / 255;
      b = col[2] / 255;
    }}
    colArr[i*3]     = r;
    colArr[i*3 + 1] = g;
    colArr[i*3 + 2] = b;
  }}
  geometry.attributes.color.needsUpdate = true;
}}

applyColors('rgb');

const material = new THREE.PointsMaterial({{
  size: 0.003,
  vertexColors: true,
  sizeAttenuation: true,
}});

const points = new THREE.Points(geometry, material);
scene.add(points);

// Add axes helper
scene.add(new THREE.AxesHelper(0.1));

// ── Auto-centre camera on point cloud ────────────────────────────────────────
geometry.computeBoundingBox();
const bb  = geometry.boundingBox;
const ctr = new THREE.Vector3();
bb.getCenter(ctr);
const sz  = new THREE.Vector3();
bb.getSize(sz);
camera.position.set(ctr.x, ctr.y + sz.y * 0.5, ctr.z + sz.z * 2);
controls.target.copy(ctr);
controls.update();

// ── UI controls ───────────────────────────────────────────────────────────────
function setMode(mode) {{
  currentMode = mode;
  document.getElementById('btnRGB').classList.toggle('active', mode === 'rgb');
  document.getElementById('btnSeg').classList.toggle('active', mode === 'seg');
  document.getElementById('legend').style.display = mode === 'seg' ? 'flex' : 'none';
  applyColors(mode);
}}

// ── Resize handler ────────────────────────────────────────────────────────────
const ro = new ResizeObserver(() => {{
  const w = container.clientWidth;
  const h = container.clientHeight;
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h);
}});
ro.observe(container);

// ── Render loop ───────────────────────────────────────────────────────────────
function animate() {{
  requestAnimationFrame(animate);
  controls.update();
  renderer.render(scene, camera);
}}
animate();
</script>
</body>
</html>
"""

    with open(out_path, "w", encoding="utf-8") as f:
        f.write(html)


def _write_empty_html(out_path: Path, sample_name: str):
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(f"<html><body><h1>No point cloud data for {sample_name}</h1></body></html>")


# ─────────────────────────────────────────────────────────────────────────────
#  Entry point
# ─────────────────────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) < 2:
        print("Usage: multi_view_postprocess.py <sample_path>")
        sys.exit(1)

    sample_path = Path(sys.argv[1]).resolve()
    if not sample_path.is_dir():
        print(f"[ERROR] Sample path not found: {sample_path}")
        sys.exit(1)

    sample_name = sample_path.name
    print(f"[PostProcess] Processing {sample_name} …")

    # ── Load shared data ──────────────────────────────────────────────────────
    color_map_path = sample_path / "color_map.json"
    cameras_path   = sample_path / "cameras.json"

    if not color_map_path.exists():
        print(f"[ERROR] color_map.json not found in {sample_path}")
        sys.exit(1)
    if not cameras_path.exists():
        print(f"[ERROR] cameras.json not found in {sample_path}")
        sys.exit(1)

    color_map = load_json(color_map_path)
    cameras   = load_json(cameras_path)

    # ── Step 1: COCO annotations ──────────────────────────────────────────────
    print("[PostProcess] Step 1/3 — Generating COCO annotations …")
    coco = masks_to_coco(sample_path, color_map)
    ann_path = sample_path / "annotations.json"
    with open(ann_path, "w", encoding="utf-8") as f:
        json.dump(coco, f, indent=2)
    print(f"  ✓ {len(coco['annotations'])} annotations across {len(coco['images'])} frames → {ann_path.name}")

    # ── Step 2: Point cloud ───────────────────────────────────────────────────
    print("[PostProcess] Step 2/3 — Building merged point cloud …")
    points = build_point_cloud(sample_path, cameras, color_map)
    ply_path = sample_path / "pointcloud.ply"
    save_ply(points, ply_path)
    print(f"  ✓ {len(points):,} points → {ply_path.name}")

    # ── Step 3: HTML viewer ────────────────────────────────────────────────────
    print("[PostProcess] Step 3/3 — Generating HTML viewer …")
    html_path = sample_path / "visualization.html"
    build_html_viewer(points, html_path, sample_name)
    print(f"  ✓ HTML viewer → {html_path.name}")

    print(f"[PostProcess] ✅ Done — {sample_name}")


if __name__ == "__main__":
    main()
