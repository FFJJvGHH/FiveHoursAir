"""Build and render three original modular industrial sprites with Blender.

Run: blender --background --threads 4 --python Tools/Blender/build_industrial_assets.py
     -- --asset all --size 512 --samples 32
All geometry is authored here; no external textures, add-ons or downloads are used.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "DeepPressure" / "Art" / "Industrial"
SOURCE = ROOT / "ArtSource" / "Blender"
ORTHO_SCALE = 5.5
COLORS = {
    "ivory": "D8DBCE", "ivory_dark": "A4B5A7", "teal": "287C7C",
    "teal_dark": "153B43", "copper": "BB784A", "copper_dark": "71492E",
    "steel": "53656D", "black": "142A33", "rubber": "202C32",
    "glass": "4BC0B6", "glass_dark": "215461", "white": "E8F2DC",
    "red": "D6754F", "yellow": "E0BF6C", "green": "9ADC99",
}
MATERIALS = {}


def linear_channel(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def rgba(code):
    return tuple(linear_channel(int(code[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1,)


def material(name):
    if name not in MATERIALS:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = rgba(COLORS[name])
        mat["source_hex"] = COLORS[name]
        MATERIALS[name] = mat
    return MATERIALS[name]


def assign(obj, mat, name=None):
    obj.data.materials.append(material(mat))
    if name:
        obj.name = name
    return obj


def bevel(obj, amount=0.06, segments=3):
    mod = obj.modifiers.new("Machined rounded edges", "BEVEL")
    mod.width = amount
    mod.segments = segments
    mod.harden_normals = True
    weighted = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True


def box(name, position, size, mat="teal", radius=0.045):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = assign(bpy.context.object, mat, name)
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if radius:
        bevel(obj, radius)
    return obj


def cylinder(name, position, radius, depth, mat="steel", axis="Z", vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=position)
    obj = assign(bpy.context.object, mat, name)
    if axis == "Y":
        obj.rotation_euler.x = math.pi / 2
    elif axis == "X":
        obj.rotation_euler.y = math.pi / 2
    for p in obj.data.polygons:
        p.use_smooth = len(p.vertices) == 4
    bevel(obj, min(radius * .13, .035), 3)
    return obj


def sphere(name, position, scale, mat="teal"):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1, location=position)
    obj = assign(bpy.context.object, mat, name)
    obj.scale = scale
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def torus(name, position, major, minor, mat="copper", axis="Z"):
    bpy.ops.mesh.primitive_torus_add(major_segments=48, minor_segments=12,
                                   location=position, major_radius=major, minor_radius=minor)
    obj = assign(bpy.context.object, mat, name)
    if axis == "Y":
        obj.rotation_euler.x = math.pi / 2
    elif axis == "X":
        obj.rotation_euler.y = math.pi / 2
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def pipe(name, points, radius=.09, mat="copper"):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 16
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, location in zip(spline.bezier_points, points):
        point.co = location
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign(obj, mat)
    return obj


def bolt(position, size=.035, mat="steel"):
    return cylinder("Hex fastener", position, size, .028, mat, "Y", 6)


def panel(name, center, width, height, mat="ivory"):
    x, y, z = center
    box(name + " gasket", (x, y + .02, z), (width + .09, .06, height + .09), "black", .04)
    box(name, center, (width, .08, height), mat, .04)
    for dx in (-width / 2 + .095, width / 2 - .095):
        for dz in (-height / 2 + .095, height / 2 - .095):
            bolt((x + dx, y - .057, z + dz))


def gauge(position, radius=.22):
    x, y, z = position
    cylinder("Pressure gauge shell", (x, y, z), radius, .12, "copper", "Y")
    cylinder("Gauge dark rim", (x, y - .074, z), radius * .85, .025, "black", "Y")
    cylinder("Gauge ivory face", (x, y - .093, z), radius * .75, .013, "white", "Y")
    for i in range(9):
        angle = math.radians(-125 + i * 31.25)
        xx, zz = math.sin(angle), math.cos(angle)
        tick = box("Pressure scale tick", (x + xx * radius * .60, y - .108,
                                           z + zz * radius * .60),
                   (.015, .008, radius * .13), "teal_dark", .002)
        tick.rotation_euler.y = angle
    needle = box("Gauge needle", (x + radius * .14, y - .12, z + radius * .12),
                 (.017, .008, radius * .60), "red", .003)
    needle.rotation_euler.y = .92
    cylinder("Gauge hub", (x, y - .13, z), radius * .10, .014, "black", "Y", 16)


def port(name, position, axis="X", mat="copper"):
    x, y, z = position
    cylinder(name + " neck", position, .13, .30, mat, axis)
    offset = Vector((math.copysign(.17, x), 0, 0) if axis == "X" else (0, -.17, 0) if axis == "Y" else (0, 0, .17))
    flange = Vector(position) + offset
    cylinder(name + " flange", flange, .205, .075, "steel", axis)
    mouth = flange + offset.normalized() * .043
    cylinder(name + " socket", mouth, .133, .015, "black", axis)
    torus(name + " seal", mouth, .15, .021, mat, axis)
    anchor = bpy.data.objects.new(name.upper().replace(" ", "_") + "_ANCHOR", None)
    anchor.location = mouth
    anchor.empty_display_type = "SPHERE"
    anchor.empty_display_size = .12
    bpy.context.collection.objects.link(anchor)
    anchor["purpose"] = "Visual attachment point; gameplay cell port comes from building definition."


def feet(width=2.7):
    box("Steel mounting rail", (0, 0, .26), (width, 1.04, .18), "steel", .035)
    for x in (-width * .36, width * .36):
        box("Isolation foot", (x, 0, .09), (.55, .92, .18), "rubber", .055)
        box("Teal support", (x, 0, .43), (.21, .68, .29), "teal_dark", .035)


def vent(center, count=6, width=.75):
    x, y, z = center
    for i in range(count):
        box("Cooling vent", (x, y, z + (i - (count - 1) / 2) * .10),
            (width, .035, .037), "black", .012)


def label(center, text, size=.13, mat="teal_dark"):
    curve = bpy.data.curves.new("Machine label", "FONT")
    curve.body = text
    curve.size = size
    curve.align_x = "CENTER"
    curve.extrude = .001
    obj = bpy.data.objects.new("Label " + text, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = center
    obj.rotation_euler.x = math.pi / 2
    assign(obj, mat)


def compressor():
    feet(3.8)
    box("Main compressor housing", (0, 0, 1.2), (3.25, .95, 1.47), "teal", .15)
    panel("Control faceplate", (.7, -.54, 1.31), 1.1, 1.14)
    panel("Fan access panel", (-.83, -.54, 1.20), 1.38, 1.03, "teal_dark")
    cylinder("Intake fan outer bezel", (-.83, -.64, 1.2), .42, .09, "steel", "Y")
    cylinder("Intake fan black insert", (-.83, -.70, 1.2), .35, .02, "black", "Y")
    for angle in range(0, 360, 60):
        blade = box("Fan grille spoke", (-.83, -.724, 1.2), (.60, .03, .025), "ivory_dark", .008)
        blade.rotation_euler.y = math.radians(angle)
    for radius in (.12, .23, .31):
        torus("Fan grille ring", (-.83, -.735, 1.2), radius, .014, "ivory_dark", "Y")
    gauge((.7, -.655, 1.53), .225)
    for x, mat in ((.49, "green"), (.90, "red")):
        cylinder("Status light socket", (x, -.615, 1.03), .095, .035, "steel", "Y")
        cylinder("Status light", (x, -.64, 1.03), .062, .02, mat, "Y")
    label((.72, -.605, .79), "CP-04", .12)
    cylinder("Compression head", (-.61, 0, 2.23), .42, 1.5, "ivory", "X")
    for x in (-1.23, -1.04, -.85, -.66, -.47, -.28, -.09):
        cylinder("Cooling fin", (x, 0, 2.23), .45, .055, "steel", "X")
    cylinder("Motor coupling", (.36, 0, 2.23), .27, .32, "copper", "X")
    cylinder("Motor casing", (.89, 0, 2.23), .34, .8, "teal", "X")
    for x in (.61, .79, .97, 1.15):
        cylinder("Motor rib", (x, 0, 2.23), .36, .03, "teal_dark", "X")
    pipe("Copper discharge loop", [(-1.36, .02, 2.29), (-1.65, -.25, 2.48),
                                  (-1.63, -.38, 2.9), (-.10, -.38, 2.9),
                                  (.15, -.38, 2.70), (.16, -.38, 1.89)], .08)
    port("Gas inlet", (-1.84, 0, 1.68), "X", "teal")
    port("Compressed outlet", (1.84, 0, 1.68), "X")
    for x in (-1.5, 1.5):
        box("Lifting lug", (x, .1, 1.96), (.23, .4, .22), "steel", .08)


def separator():
    feet(2.6)
    box("Separator rear frame", (0, .24, 2.2), (2.48, .42, 3.4), "teal_dark", .10)
    for x in (-.80, .80):
        cylinder("Fractionation column", (x, -.05, 2.30), .40, 2.65, "ivory")
        sphere("Dished column top", (x, -.05, 3.625), (.40, .40, .29), "ivory")
        sphere("Dished column bottom", (x, -.05, .98), (.40, .40, .29), "ivory_dark")
        for z in (1.19, 3.38):
            cylinder("Column retaining band", (x, -.05, z), .426, .14, "teal")
        box("Column front sightglass gasket", (x, -.435, 2.39), (.26, .055, 1.32), "black", .075)
        box("Column luminous fluid", (x, -.472, 2.36), (.17, .018, 1.16), "glass", .046)
        for z in (1.95, 2.15, 2.35, 2.55, 2.75):
            box("Sightglass graduation", (x + .041, -.49, z), (.07, .02, .015), "ivory", .002)
    panel("Central telemetry", (0, -.56, 2.10), .65, 1.35)
    gauge((0, -.66, 2.41), .20)
    vent((0, -.62, 1.82), 4, .37)
    label((0, -.62, 1.53), "SEP-2", .085)
    pipe("Upper linking manifold", [(-.80, -.05, 3.9), (-.80, -.05, 4.12),
                                   (.8, -.05, 4.12), (.8, -.05, 3.9)], .087)
    pipe("Crossflow pipe", [(-.82, -.58, 3.18), (-.36, -.66, 3.17),
                           (-.23, -.66, 3.36), (.26, -.66, 3.36),
                           (.39, -.66, 3.14), (.83, -.58, 3.14)], .064)
    pipe("Lower waste manifold", [(-.8, -.1, .81), (-.8, -.24, .65),
                                  (.8, -.24, .65), (.8, -.1, .81)], .075)
    for x in (-.8, .8):
        cylinder("Front manifold socket", (x, -.57, 3.15), .12, .18, "steel", "Y")
    port("Raw gas inlet", (-1.40, 0, 1.30), "X", "teal")
    port("Product outlet", (1.40, 0, 3.25), "X")
    port("Waste outlet", (1.4, 0, 1.3), "X", "teal_dark")
    box("Top warning plate", (0, -.09, 3.69), (.43, .10, .25), "yellow", .025)
    label((0, -.15, 3.645), "!", .18)


def tank():
    feet(3.10)
    cylinder("Main pressure vessel", (-.42, .06, 2.16), .85, 2.66, "ivory")
    sphere("Pressure vessel upper dome", (-.42, .06, 3.49), (.85, .85, .48), "ivory")
    sphere("Pressure vessel lower dome", (-.42, .06, .83), (.85, .85, .40), "ivory_dark")
    for z in (1.08, 3.19):
        cylinder("Vessel retaining belt", (-.42, .06, z), .876, .15, "teal")
        box("Belt buckle", (-.42, -.831, z), (.27, .08, .23), "steel", .025)
        bolt((-.42, -.883, z), .048)
    panel("Pressure station plate", (-.42, -.807, 2.61), .76, .82, "teal")
    gauge((-.42, -.914, 2.64), .255)
    label((-.42, -.83, 2.02), "HP / 240", .17, "teal_dark")
    label((-.42, -.83, 1.77), "PRESSURE", .10, "teal_dark")
    box("Vessel safety stripe", (-.42, -.77, 1.47), (.65, .05, .18), "yellow", .02)
    for x in (-.64, -.42, -.20):
        marker = box("Safety stripe hatch", (x, -.804, 1.47), (.065, .016, .17), "black", .002)
        marker.rotation_euler.y = -.45
    cylinder("Buffer vessel", (1.02, .05, 1.63), .34, 1.88, "teal")
    sphere("Buffer upper dome", (1.02, .05, 2.57), (.34, .34, .28), "teal")
    sphere("Buffer lower dome", (1.02, .05, .69), (.34, .34, .24), "teal_dark")
    for z in (.95, 2.24):
        cylinder("Buffer clamp", (1.02, .05, z), .36, .09, "steel")
    pipe("Copper bypass", [(-.42, .04, 3.98), (-.42, -.12, 4.24),
                           (.95, -.12, 4.24), (1.05, -.12, 2.84)], .074)
    cylinder("Regulator pedestal", (-.42, .04, 4.09), .18, .22, "steel")
    cylinder("Relief stem", (-.85, -.28, 3.88), .065, .29, "copper")
    torus("Relief valve handwheel", (-.85, -.28, 4.04), .22, .035, "red")
    for angle in (0, math.pi / 2):
        obj = box("Handwheel spoke", (-.85, -.28, 4.04), (.43, .04, .034), "red", .012)
        obj.rotation_euler.z = angle
    port("Pressure inlet", (-1.51, .03, 1.58), "X", "teal")
    port("Pressure outlet", (1.56, .03, 1.58), "X")
    pipe("Buffer feed", [(1.01, .03, 1.58), (1.27, .03, 1.58), (1.43, .03, 1.58)], .09)


def configure_materials(mode):
    for name, mat in MATERIALS.items():
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        if mode == "Preview":
            shader = nodes.new("ShaderNodeBsdfPrincipled")
            shader.inputs["Base Color"].default_value = mat.diffuse_color
            shader.inputs["Roughness"].default_value = .39 if name in ("copper", "steel") else .61
            shader.inputs["Metallic"].default_value = .63 if name in ("copper", "steel") else .14
            links.new(shader.outputs["BSDF"], output.inputs["Surface"])
            continue
        shader = nodes.new("ShaderNodeEmission")
        links.new(shader.outputs[0], output.inputs["Surface"])
        if mode == "Color":
            shader.inputs["Color"].default_value = mat.diffuse_color
        elif mode == "AO":
            ao = nodes.new("ShaderNodeAmbientOcclusion")
            ao.inputs["Distance"].default_value = .62
            ao.samples = 16
            links.new(ao.outputs["AO"], shader.inputs["Color"])
        elif mode == "Normal":
            geometry = nodes.new("ShaderNodeNewGeometry")
            transform = nodes.new("ShaderNodeVectorTransform")
            transform.vector_type = "NORMAL"
            transform.convert_from = "WORLD"
            transform.convert_to = "CAMERA"
            scale = nodes.new("ShaderNodeVectorMath")
            scale.operation = "MULTIPLY"
            # Cycles' shader camera transform uses +Z into the screen. Sprite
            # tangent-space +Z points at the viewer, so explicitly reverse Z.
            scale.inputs[1].default_value = (.5, .5, -.5)
            bias = nodes.new("ShaderNodeVectorMath")
            bias.operation = "ADD"
            bias.inputs[1].default_value = (.5, .5, .5)
            links.new(geometry.outputs["Normal"], transform.inputs["Vector"])
            links.new(transform.outputs["Vector"], scale.inputs[0])
            links.new(scale.outputs[0], bias.inputs[0])
            links.new(bias.outputs[0], shader.inputs["Color"])


def area_light(name, location, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0, 0, 2)) - obj.location).to_track_quat("-Z", "Y").to_euler()


def save_layout(name, scene, size):
    """Export useful authoring metadata, independent of game simulation cells."""
    bpy.context.view_layer.update()
    camera_from_world = scene.camera.matrix_world.inverted()

    def project(point):
        p = camera_from_world @ Vector(point)
        return [round(.5 + p.x / ORTHO_SCALE, 6), round(.5 + p.y / ORTHO_SCALE, 6)]

    metadata = {
        "asset": name,
        "imageSize": [size, size],
        "orthographicSizeWorld": ORTHO_SCALE,
        "pixelsPerBlenderUnit": size / ORTHO_SCALE,
        "unitySpritePivotNormalized": project((0, 0, 0)),
        "pivotConvention": "Unity normalized sprite coordinates: bottom-left (0,0), top-right (1,1)",
        "normalConvention": "X right; Y up; Z toward viewer. Raw RGB = normal * 0.5 + 0.5.",
        "ports": [
            {"name": o.name, "worldPosition": list(o.location), "spritePositionNormalized": project(o.location)}
            for o in scene.objects if o.type == "EMPTY" and o.name.endswith("_ANCHOR")
        ],
        "gameplayNote": "Visual port coordinates only. Define occupancy and gas network ports separately in building data.",
    }
    (OUTPUT / f"{name}_Layout.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


def setup_scene(size, samples):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    MATERIALS.clear()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 4
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 40
    scene.world.color = (.12, .12, .12)
    camera_data = bpy.data.cameras.new("Orthographic sprite camera")
    camera = bpy.data.objects.new("Orthographic sprite camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ORTHO_SCALE
    # Identical scale and framing for all three assets. Vertical model origin is the floor.
    target = Vector((0, 0, 2.18))
    camera.location = target + Vector((0, -15, 3.1))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    area_light("Large cool key", (-3, -5, 7), 700, 5, (.72, .88, 1.0))
    area_light("Warm side light", (4, -1, 4), 500, 4, (1.0, .73, .43))
    area_light("Soft front fill", (0, -6, 2), 130, 4, (.53, 1.0, .91))
    scene["sprite_ortho_scale_world_units"] = ORTHO_SCALE
    scene["sprite_pixels_per_unit"] = size / ORTHO_SCALE
    scene["sprite_origin"] = "World (0,0,0), floor at bottom of feet; camera is fixed for all assets."
    scene["normal_convention"] = "Camera-space normals, RGB=XYZ*.5+.5; +X right, +Y up, +Z toward viewer."
    return scene


def main():
    global OUTPUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset", choices=("all", "compressor", "separator", "tank"), default="all")
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--samples", type=int, default=32)
    parser.add_argument("--passes", nargs="+", default=["Color", "Normal", "AO", "Preview"])
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    OUTPUT = args.output.resolve()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    builders = {"compressor": compressor, "separator": separator, "tank": tank}
    for name, build in builders.items():
        if args.asset not in ("all", name):
            continue
        scene = setup_scene(args.size, args.samples)
        build()
        save_layout(name, scene, args.size)
        for render_pass in args.passes:
            if render_pass not in ("Color", "Normal", "AO", "Preview"):
                raise ValueError("Unknown pass: " + render_pass)
            configure_materials(render_pass)
            scene.cycles.use_denoising = render_pass == "Preview"
            scene.view_settings.view_transform = "Standard" if render_pass in ("Color", "Preview") else "Raw"
            scene.view_settings.look = "None"
            scene.render.filepath = str(OUTPUT / f"{name}_{render_pass}.png")
            bpy.ops.render.render(write_still=True)
            print(f"ASSET_RENDERED {name} {render_pass}: {scene.render.filepath}", flush=True)
        # The source opens with useful materials and lights, ready for further art edits.
        configure_materials("Preview")
        scene.cycles.use_denoising = True
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "None"
        scene.render.filepath = str(OUTPUT / f"{name}_Preview.png")
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / f"{name}.blend"))
        print(f"ASSET_COMPLETE {name}", flush=True)


if __name__ == "__main__":
    main()
