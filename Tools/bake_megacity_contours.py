"""Bake the two Mission 3 skyline silhouettes for the EMP shader (Pillow and NumPy)."""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps


ARENA = Path(__file__).resolve().parents[1] / "Assets/_Game/Levels/L01_Duel/Resources/Arena"
DISTANCE_RANGE = 4  # Source texels on each side of the silhouette. Shared with FinalBossEmpSweep.


def far_silhouette(image):
    # The far plate is opaque. Follow the first sustained roof or antenna in each
    # column, then fill below it so recessed windows and dark facades stay solid.
    height, width = image.shape[:2]
    structure = (image[:, :, 2] >= 25) & (image[:, :, 1] >= 18)
    # The projecting roof on the right has open sky beneath it. Trace it on
    # its own so filling the lower skyline preserves that opening.
    overhang = np.zeros((height, width), dtype=bool)
    overhang[138:163, 806:] = structure[138:163, 806:]
    structure[:163, 806:] = False
    supported = structure.copy()
    supported[:-2] &= structure[1:-1] & structure[2:]
    supported[-2:] = False
    roof = np.where(supported.any(axis=0), supported.argmax(axis=0), height)
    # Reduce one-pixel steps from sky grain but retain tall, narrow antennas.
    padded = np.pad(roof, 1, mode="edge")
    median = np.median(np.stack((padded[:-2], roof, padded[2:])), axis=0)
    roof = np.where(np.abs(median - roof) <= 3, median, roof)
    # Bridge a single missing sample between matching roof heights.
    gap = (roof == height) & (padded[:-2] == padded[2:])
    roof = np.where(gap, padded[:-2], roof)
    return (np.arange(height)[:, None] >= roof) | overhang


def near_silhouette(alpha):
    # Both tower groups reach the bottom of the plate. Discard detached flecks,
    # then fill enclosed glass detail while keeping open sky beneath the bridge.
    solid = Image.fromarray(np.uint8(alpha >= 128) * 255)
    connected = Image.new("L", (solid.width, solid.height + 1), 255)
    connected.paste(solid, (0, 0))
    ImageDraw.floodfill(connected, (0, solid.height), 128)
    grounded = connected.crop((0, 0, solid.width, solid.height)).point(lambda v: 255 if v == 128 else 0)
    outside = ImageOps.expand(grounded, 1)
    ImageDraw.floodfill(outside, (0, 0), 128)
    return np.asarray(outside)[1:-1, 1:-1] != 128


def signed_distance(solid):
    # Sample centers on opposite sides of a boundary are one texel apart.
    # Extend boundary pixels so the texture crop never produces an outline.
    reach = DISTANCE_RANGE + 1
    height, width = solid.shape
    padded = np.pad(solid, reach, mode="edge")
    distance = np.full(solid.shape, DISTANCE_RANGE, dtype=np.float32)
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            to_edge = np.hypot(dx, dy) - 0.5
            if not 0 < to_edge < DISTANCE_RANGE:
                continue
            x, y = reach + dx, reach + dy
            neighbor = padded[y:y + height, x:x + width]
            np.minimum(distance, to_edge, out=distance, where=solid != neighbor)
    signed = np.where(solid, -distance, distance)
    return np.uint8(np.rint((signed / DISTANCE_RANGE + 1) * 127.5))


if __name__ == "__main__":
    far = far_silhouette(np.array(Image.open(ARENA / "MegacityFar.png").convert("RGB")))
    near = near_silhouette(np.array(Image.open(ARENA / "MegacityNear.png").getchannel("A")))
    channels = (signed_distance(far), signed_distance(near), np.uint8(near) * 255)
    Image.fromarray(np.stack(channels, axis=2)).save(ARENA / "MegacityContours.png", optimize=True)
