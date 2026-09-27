"""Render grounded idle gestures, climbing and carrying from our existing original rig.

No source sprite is cropped, painted or resampled. Every pose is rendered from the
same authored Blender model/camera into aligned Color/Normal/AO texture families.
"""
import importlib.util
import math
from pathlib import Path
import bpy

folder = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('industrial', folder / 'build_industrial_assets.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
bpy.ops.wm.open_mainfile(filepath=str(m.SOURCE / 'survey_engineer.blend'))
scene = bpy.context.scene
scene.render.threads_mode = 'FIXED'
scene.render.threads = 4
scene.cycles.samples = 16
out = m.ROOT / 'Assets' / 'DeepPressure' / 'Art' / 'Workers'
out.mkdir(parents=True, exist_ok=True)
for material in bpy.data.materials:
    if material.use_nodes and 'source_hex' in material:
        m.MATERIALS[material.name] = material
head = bpy.data.objects['Head rig']
body = bpy.data.objects['Torso rig']
limbs = {name: bpy.data.objects[name] for name in ('Leg front', 'Leg back', 'Arm front', 'Arm back')}
pupils = [obj for obj in bpy.data.objects if obj.name.startswith('Expressive pupil')]
rests = {obj.name: (obj.location.copy(), obj.rotation_euler.copy(), obj.scale.copy())
         for obj in [head, body] + list(limbs.values()) + pupils}
for obj in bpy.data.objects['Tool rig'].children_recursive:
    obj.hide_render = True

def pose(kind, frame):
    for name, (location, rotation, scale) in rests.items():
        obj = bpy.data.objects[name]
        obj.location = location.copy()
        obj.rotation_euler = rotation.copy()
        obj.scale = scale.copy()
    phase = frame / 4 * math.tau
    if kind == 'idle_detail':
        # Boots remain planted; only shoulder/chest/head move, avoiding whole-body
        # bobbing and the visible foot drift caused by scaling around a centre.
        body.scale.z = 1 + (.012 if frame == 1 else -.007 if frame == 3 else 0)
        head.rotation_euler.z = [0, -.08, .14, 0][frame]
        head.rotation_euler.y = [0, .02, -.025, .01][frame]
        limbs['Arm back'].rotation_euler.y = [0, .035, -.06, 0][frame]
        if frame == 3:
            for pupil in pupils:
                pupil.scale.z *= .15
    elif kind == 'carry':
        limbs['Leg front'].rotation_euler.y = math.sin(phase) * .38
        limbs['Leg back'].rotation_euler.y = -math.sin(phase) * .38
        limbs['Arm front'].rotation_euler.y = -.85
        limbs['Arm back'].rotation_euler.y = -.68
        head.rotation_euler.y = -.035
    else:
        # Visible alternating reaching motion instead of playing a running loop
        # while travelling vertically. The simulation root follows the ladder.
        limbs['Arm front'].rotation_euler.y = -1.70 + math.sin(phase) * .55
        limbs['Arm back'].rotation_euler.y = -1.70 - math.sin(phase) * .55
        limbs['Leg front'].rotation_euler.y = math.sin(phase) * .48
        limbs['Leg back'].rotation_euler.y = -math.sin(phase) * .48
        head.rotation_euler.y = -.09
    bpy.context.view_layer.update()

for kind in ('idle_detail', 'carry', 'climb'):
    for frame in range(4):
        pose(kind, frame)
        for render_pass in ('Color', 'Normal', 'AO'):
            m.configure_materials(render_pass)
            scene.view_settings.view_transform = 'Standard' if render_pass == 'Color' else 'Raw'
            scene.view_settings.look = 'None'
            scene.render.filepath = str(out / f'engineer_{kind}{frame}_{render_pass}.png')
            bpy.ops.render.render(write_still=True)
        print('DETAIL_FRAME', kind, frame, flush=True)
