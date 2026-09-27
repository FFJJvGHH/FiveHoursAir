# Organic Terrain V3

Generated offline by DeepTerrainArtBaker.Prepare(). Each of five materials has 47 eight-neighbor blob masks x 3 stable coordinate variations at 128 pixels per cell: 705 independent sprites. Diagonals are normalized unless both adjoining cardinal neighbors exist. Excavating a cell refreshes all eight neighbors, including concave inside corners.

Natural terrain has broad eroded edges, approximately 4–20 pixels of inset, 21-pixel rounded convex corners, and 12-pixel concave cutouts. All variations approach the same endpoint height and tangent, so adjoining exposed edges meet. Interior material boundaries remain filled. Manufactured metal intentionally retains a small machined bevel. Logical collision, pathfinding, pressure rooms and construction retain their editable grid.

Color, tangent normal and AO share the exact same coverage and atlas rectangles. Normal derives from the same relief; AO derives from recesses. Atlases are 1584 x 1584 with 2-pixel extruded gutters. Secondary textures are _NormalMap and _AOMap; use TerrainHD2D.mat and Light2D Normal Map Quality. The folder path remains TerrainV2 to preserve existing asset GUIDs.
