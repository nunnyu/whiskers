# Exports Art/cat.blend to Unity: separate parts with pivots at the joints, a smaller body, big eyes,
# and one baked fur texture per breed (all breeds share the same UV layout).
# blender -b --disable-autoexec Art/cat.blend --python Tools/export_cat.py -- Assets/Resources/Cat/Cat.fbx Assets/Resources/Cat/Breeds
import bpy, bmesh, sys, os, subprocess, mathutils
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index('--') + 1:]
fbx_path, breed_dir = args[0], args[1]
only_breed = args[2] if len(args) > 2 else None
os.makedirs(breed_dir, exist_ok=True)

BODY_SCALE = 0.72
TEXTURE_SIZE = 128
EYE_RADIUS = 0.42
EYE_SPACING = 0.5

BREEDS = {
    # base, stripes, belly/socks, patch 1, patch 2, stripe strength, belly strength, sock strength, patch strengths
    'Orange': ((0.85, 0.42, 0.12), (0.55, 0.22, 0.05), (0.98, 0.88, 0.7), (0, 0, 0), (0, 0, 0), 1.0, 0.8, 0.0, 0.0, 0.0),
    'Grey': ((0.5, 0.5, 0.52), (0.22, 0.22, 0.25), (0.88, 0.88, 0.86), (0, 0, 0), (0, 0, 0), 1.0, 0.7, 0.0, 0.0, 0.0),
    'Tuxedo': ((0.06, 0.06, 0.07), (0.06, 0.06, 0.07), (0.97, 0.97, 0.95), (0, 0, 0), (0, 0, 0), 0.0, 1.0, 1.0, 0.0, 0.0),
    'Calico': ((0.97, 0.95, 0.9), (0.97, 0.95, 0.9), (0.97, 0.95, 0.9), (0.9, 0.48, 0.15), (0.08, 0.07, 0.07), 0.0, 0.0, 0.0, 1.0, 1.0),
}

scene = bpy.context.scene
view_layer = bpy.context.view_layer

bpy.data.objects.remove(bpy.data.objects['Plane'])
rename = {'Cube.001': 'Body', 'Sphere': 'Head', 'Cylinder': 'Neck', 'Cylinder.001': 'RightArm',
          'Cylinder.002': 'LeftArm', 'Cylinder.003': 'RightLeg', 'Cylinder.004': 'LeftLeg', 'Cylinder.005': 'Tail'}
for old, new in rename.items():
    bpy.data.objects[old].name = new

depsgraph = bpy.context.evaluated_depsgraph_get()
for o in [o for o in scene.objects if o.type == 'MESH']:
    mesh = bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph))
    mesh.transform(o.matrix_world)
    o.modifiers.clear()
    o.data = mesh
    o.matrix_world = mathutils.Matrix.Identity(4)
    for c in list(o.users_collection):
        c.objects.unlink(o)
    scene.collection.objects.link(o)
    o.hide_set(False); o.hide_viewport = False; o.hide_render = False

head, neck = bpy.data.objects['Head'], bpy.data.objects['Neck']
bm = bmesh.new(); bm.from_mesh(head.data); bm.from_mesh(neck.data); bm.to_mesh(head.data); bm.free()
neck_bottom = min(v.co.z for v in neck.data.vertices)
bpy.data.objects.remove(neck)

for o in [o for o in scene.objects if o.type == 'MESH']:
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()

def bounds(o):
    vs = [v.co for v in o.data.vertices]
    lo = mathutils.Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = mathutils.Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi

def joint(o):
    lo, hi = bounds(o)
    c = (lo + hi) / 2
    if o.name.endswith('Leg'):
        return mathutils.Vector((c.x, c.y, hi.z))
    if o.name.endswith('Arm'):
        side = 1 if c.x > 0 else -1
        return mathutils.Vector((side * 0.62, c.y, c.z))
    if o.name == 'Tail':
        near = [v.co for v in o.data.vertices if 0.6 < v.co.y < 0.8]
        return sum(near, mathutils.Vector()) / len(near)
    if o.name == 'Head':
        return mathutils.Vector((0, 0, neck_bottom))
    return mathutils.Vector((c.x, c.y, lo.z))

# Shrink the body around its center and pull every attached part along with it.
body = bpy.data.objects['Body']
lo, hi = bounds(body)
body_center = (lo + hi) / 2
shrink = (mathutils.Matrix.Translation(body_center) @ mathutils.Matrix.Scale(BODY_SCALE, 4)
          @ mathutils.Matrix.Translation(-body_center))
joints = {o.name: joint(o) for o in scene.objects if o.type == 'MESH'}
body.data.transform(shrink)
for name, point in joints.items():
    if name != 'Body':
        bpy.data.objects[name].data.transform(mathutils.Matrix.Translation(shrink @ point - point))
joints = {name: shrink @ point for name, point in joints.items()}

parts = [o for o in scene.objects if o.type == 'MESH']

# One shared UV atlas for all parts so every breed texture fits every part.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
    o.select_set(True)
view_layer.objects.active = parts[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.03)
bpy.ops.object.mode_set(mode='OBJECT')

# Fur pattern from world position, baked into the atlas once per breed.
fur = bpy.data.materials.new('Fur')
fur.use_nodes = True
nodes, links = fur.node_tree.nodes, fur.node_tree.links
nodes.clear()
output = nodes.new('ShaderNodeOutputMaterial')
emission = nodes.new('ShaderNodeEmission')
links.new(emission.outputs[0], output.inputs['Surface'])
geometry = nodes.new('ShaderNodeNewGeometry')
xyz = nodes.new('ShaderNodeSeparateXYZ')
links.new(geometry.outputs['Position'], xyz.inputs[0])

def value(name):
    n = nodes.new('ShaderNodeValue'); n.name = 'weight_' + name; return n.outputs[0]

def rgb(name):
    n = nodes.new('ShaderNodeRGB'); n.name = 'color_' + name; return n

def math(op, a, b=None, clamp=False):
    n = nodes.new('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
    for i, x in enumerate((a, b)):
        if x is None:
            continue
        if isinstance(x, (int, float)):
            n.inputs[i].default_value = x
        else:
            links.new(x, n.inputs[i])
    return n.outputs[0]

def mix(a, b, factor):
    n = nodes.new('ShaderNodeMix'); n.data_type = 'RGBA'
    links.new(factor, n.inputs[0])
    links.new(a, n.inputs[6]); links.new(b, n.inputs[7])
    return n.outputs[2]

def ramp(fac, at):
    n = nodes.new('ShaderNodeValToRGB'); n.color_ramp.interpolation = 'CONSTANT'
    n.color_ramp.elements[1].position = at
    links.new(fac, n.inputs[0]); return n.outputs[0]

x, y, z = xyz.outputs[0], xyz.outputs[1], xyz.outputs[2]
head_lo, head_hi = bounds(bpy.data.objects['Head'])
head_mid = (head_lo.z + head_hi.z) / 2

wave = nodes.new('ShaderNodeTexWave'); wave.wave_type = 'BANDS'; wave.bands_direction = 'Z'
wave.inputs['Scale'].default_value = 0.75; wave.inputs['Distortion'].default_value = 1.5
wave.inputs['Detail'].default_value = 1
diagonal = nodes.new('ShaderNodeCombineXYZ')
links.new(x, diagonal.inputs[0]); links.new(y, diagonal.inputs[1])
links.new(math('ADD', z, math('MULTIPLY', y, 0.8)), diagonal.inputs[2])
links.new(diagonal.outputs[0], wave.inputs['Vector'])
stripes = ramp(wave.outputs['Fac'], 0.62)

noise_a = nodes.new('ShaderNodeTexNoise'); noise_a.inputs['Scale'].default_value = 0.6
noise_b = nodes.new('ShaderNodeTexNoise'); noise_b.inputs['Scale'].default_value = 0.7
noise_b.noise_dimensions = '4D'; noise_b.inputs['W'].default_value = 5
links.new(geometry.outputs['Position'], noise_a.inputs['Vector'])
links.new(geometry.outputs['Position'], noise_b.inputs['Vector'])
patch_a = ramp(noise_a.outputs['Fac'], 0.53)
patch_b = ramp(noise_b.outputs['Fac'], 0.56)

front = math('MULTIPLY', math('SUBTRACT', math('MULTIPLY', y, -1), 0.15), 3, clamp=True)
chest = math('MULTIPLY', math('SUBTRACT', head_lo.z + 0.2, z), 3, clamp=True)
muzzle = math('MULTIPLY', math('SUBTRACT', head_mid - 0.15, z), 4, clamp=True)
belly = math('MULTIPLY', front, math('MAXIMUM', chest, muzzle))
socks = math('MULTIPLY', math('SUBTRACT', 0.55, z), 5, clamp=True)

weights = {name: value(name) for name in ('stripe', 'belly', 'sock', 'patch_a', 'patch_b')}
colors = {name: rgb(name) for name in ('base', 'stripe', 'belly', 'patch_a', 'patch_b')}
color = colors['base'].outputs[0]
color = mix(color, colors['stripe'].outputs[0], math('MULTIPLY', stripes, weights['stripe']))
color = mix(color, colors['patch_a'].outputs[0], math('MULTIPLY', patch_a, weights['patch_a']))
color = mix(color, colors['patch_b'].outputs[0], math('MULTIPLY', patch_b, weights['patch_b']))
color = mix(color, colors['belly'].outputs[0], math('MULTIPLY', belly, weights['belly']))
color = mix(color, colors['belly'].outputs[0], math('MULTIPLY', socks, weights['sock']))
links.new(color, emission.inputs['Color'])

bake_image = bpy.data.images.new('FurBake', TEXTURE_SIZE, TEXTURE_SIZE)
image_node = nodes.new('ShaderNodeTexImage'); image_node.image = bake_image; image_node.name = 'bake'

# Cycles keeps stale colors when baking the same objects twice, so each breed bakes in its own Blender process.
if only_breed is None:
    for breed in BREEDS:
        subprocess.run([bpy.app.binary_path, '-b', '--disable-autoexec', bpy.data.filepath,
                        '--python', os.path.abspath(__file__), '--', fbx_path, breed_dir, breed], check=True)
else:
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 4
    scene.cycles.device = 'CPU'
    base, stripe, light, pa, pb, ws, wb, wsock, wpa, wpb = BREEDS[only_breed]
    for key, c in zip(('base', 'stripe', 'belly', 'patch_a', 'patch_b'), (base, stripe, light, pa, pb)):
        colors[key].outputs[0].default_value = (*c, 1)
    for key, w in zip(('stripe', 'belly', 'sock', 'patch_a', 'patch_b'), (ws, wb, wsock, wpa, wpb)):
        weights[key].default_value = w
    nodes.active = image_node
    for o in parts:
        o.data.materials.clear(); o.data.materials.append(fur)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    view_layer.objects.active = parts[0]
    bpy.ops.object.bake(type='EMIT', margin=4, use_clear=True)
    bake_image.filepath_raw = os.path.join(breed_dir, only_breed + '.png')
    bake_image.file_format = 'PNG'
    bake_image.save()
    print('BAKED', only_breed)
    sys.exit(0)

for o in parts:
    o.data.materials.clear(); o.data.materials.append(fur)
nodes.remove(image_node)

# Big eyes on the front of the head, each with a little shine.
def flat_material(name, color):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*color, 1)
    return m

head = bpy.data.objects['Head']
head.data.materials.append(flat_material('Eye', (0.02, 0.02, 0.03)))
head.data.materials.append(flat_material('EyeShine', (1, 1, 1)))
tree = BVHTree.FromObject(head, bpy.context.evaluated_depsgraph_get())
eye_height = head_mid - 0.05
bm = bmesh.new(); bm.from_mesh(head.data)
for side in (-1, 1):
    hit, normal, _, _ = tree.ray_cast(mathutils.Vector((side * EYE_SPACING, -5, eye_height)), mathutils.Vector((0, 1, 0)))
    if hit is None:
        continue
    facing = normal.to_track_quat('Y', 'Z').to_matrix().to_4x4()
    for radius, depth, offset, material in ((EYE_RADIUS, 0.3, mathutils.Vector((0, 0, 0)), 1),
                                            (EYE_RADIUS * 0.28, 0.5, mathutils.Vector((side * -0.13, 0.35, 0.17)), 2)):
        sphere = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=1)
        verts = sphere['verts']
        shape = mathutils.Matrix.Diagonal((radius, radius * depth, radius * 1.2, 1))
        place = mathutils.Matrix.Translation(hit + normal * (radius * depth * 0.4) + facing.to_3x3() @ mathutils.Vector((offset.x, offset.y * EYE_RADIUS, offset.z)))
        bmesh.ops.transform(bm, matrix=place @ facing @ shape, verts=verts)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = material
            f.smooth = True
bm.to_mesh(head.data); bm.free()

for o in parts:
    p = joints[o.name]
    o.data.transform(mathutils.Matrix.Translation(-p))
    o.location = p
    print('PART', o.name, tuple(round(v, 3) for v in o.location), len(o.data.vertices))

bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=False, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP')
print('EXPORTED', fbx_path)
