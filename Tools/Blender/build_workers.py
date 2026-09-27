"""Original survey-engineer character, animated orthographic color/normal/AO frames.
Not derived from or extracted from Oxygen Not Included assets.
"""
import importlib.util, math
from pathlib import Path
import bpy
from mathutils import Vector
folder=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('industrial',folder/'build_industrial_assets.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
out=m.ROOT/'ArtSource'/'Blender'/'Workers';out.mkdir(parents=True,exist_ok=True)
m.ORTHO_SCALE=2.2
m.COLORS.update({'skin':'D5A47F','skin_shade':'A87857','suit':'467F88','suit_dark':'294F62','visor':'A8D9D4','hair':'3B3036'})
scene=m.setup_scene(256,16)
target=Vector((0,0,.95));scene.camera.location=target+Vector((0,-12,1.0));scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
b=m.box;s=m.sphere;c=m.cylinder

def pivot(name,pos):
    e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e);e.location=pos;return e
def parent_since(group,before):
    bpy.context.view_layer.update()
    for o in set(bpy.data.objects)-before:
        if o.type in ('MESH','CURVE'):
            w=o.matrix_world.copy();o.parent=group;o.matrix_world=w

body=pivot('Torso rig',(0,0,.82));before=set(bpy.data.objects)
s('Pressure suit torso',(0,0,.82),(.26,.19,.36),'suit')
b('Suit collar',(0,0,1.09),(.43,.35,.11),'ivory',.07)
b('Belt',(0,-.01,.60),(.50,.38,.11),'suit_dark',.04)
b('Belt buckle',(.04,-.221,.60),(.12,.048,.082),'copper',.02)
b('Chest patch',(-.10,-.175,.88),(.12,.05,.12),'ivory',.02)
b('Chest luminous strip',(.105,-.18,.90),(.055,.035,.22),'glass',.015)
c('Back oxygen bottle',(-.24,.19,.87),.14,.48,'ivory')
m.pipe('Breathing hose',[(-.27,.10,1.11),(-.36,-.02,1.11),(-.30,-.16,.92),(-.19,-.19,.89)],.029,'copper')
b('Belt pouch',(-.26,-.02,.57),(.14,.27,.21),'copper_dark',.04)
parent_since(body,before)
head=pivot('Head rig',(0,0,1.15));before=set(bpy.data.objects)
s('Helmet shell',(0,.035,1.45),(.365,.285,.375),'ivory')
s('Face',(0,-.207,1.39),(.27,.16,.26),'skin')
b('Helmet front lip',(0,-.27,1.61),(.68,.16,.082),'teal',.035)
b('Visor gasket',(0,-.338,1.45),(.51,.058,.19),'black',.07)
for x in (-.127,.127):
    s('Glass lens',(x,-.367,1.45),(.106,.033,.074),'visor')
    s('Expressive pupil',(x+.025,-.397,1.45),(.027,.012,.040),'black')
    s('Lens highlight',(x-.035,-.399,1.479),(.025,.008,.013),'white')
s('Nose',(.057,-.38,1.347),(.053,.042,.050),'skin')
b('Mouth',(.023,-.338,1.245),(.095,.02,.022),'skin_shade',.01)
for x in (-.35,.35):c('Helmet fastener',(x,-.025,1.44),.072,.072,'copper','X',20)
c('Headlamp bezel',(.27,-.323,1.64),.08,.12,'copper','Y')
c('Headlamp glass',(.27,-.395,1.64),.059,.02,'white','Y')
parent_since(head,before)
limbs={}
for side,x,y in [('back',-.14,.06),('front',.14,-.13)]:
    leg=pivot('Leg '+side,(x,y,.57));before=set(bpy.data.objects)
    s('Suit trouser',(x,y,.36),(.102,.115,.25),'suit_dark')
    b('Kneepad',(x,y-.10,.34),(.135,.071,.14),'ivory_dark',.045)
    b('Heavy boot',(x+.045,y-.018,.097),(.25,.28,.15),'black',.055)
    b('Boot toecap',(x+.104,y-.143,.105),(.12,.049,.071),'steel',.025)
    parent_since(leg,before);limbs['leg_'+side]=leg
for side,x,y in [('back',-.26,.055),('front',.27,-.115)]:
    arm=pivot('Arm '+side,(x,y,.99));before=set(bpy.data.objects)
    s('Shoulder armor',(x,y,.96),(.125,.132,.145),'ivory')
    s('Suit sleeve',(x,y,.77),(.095,.1,.21),'suit')
    b('Wrist cuff',(x,y,.62),(.15,.18,.10),'copper',.03)
    s('Glove',(x,y-.005,.54),(.1,.107,.11),'ivory')
    parent_since(arm,before);limbs['arm_'+side]=arm

tool=pivot('Tool rig',(.27,-.115,.54));before=set(bpy.data.objects)
b('Tool grip',(.29,-.12,.58),(.09,.14,.20),'black',.02)
b('Multitool body',(.35,-.12,.74),(.32,.18,.14),'yellow',.045)
c('Tool nozzle',(.55,-.12,.74),.045,.14,'steel','X',16)
parent_since(tool,before)
tool.parent=limbs['arm_front'];tool.matrix_parent_inverse=limbs['arm_front'].matrix_world.inverted()
tool_objects=[o for o in tool.children]

def pose(kind,frame):
    angle=frame/6*math.tau
    for v in limbs.values():v.rotation_euler=(0,0,0)
    body.location.z=.82;head.rotation_euler.y=0
    for o in tool_objects:o.hide_render=kind!='work'
    if kind=='walk':
        limbs['leg_front'].rotation_euler.y=math.sin(angle)*.53
        limbs['leg_back'].rotation_euler.y=-math.sin(angle)*.53
        limbs['arm_front'].rotation_euler.y=-math.sin(angle)*.30
        limbs['arm_back'].rotation_euler.y=math.sin(angle)*.30
        head.rotation_euler.y=.03*math.sin(angle)
    elif kind=='work':
        limbs['arm_front'].rotation_euler.y=-1.25+.19*math.sin(frame/4*math.tau)
        limbs['arm_back'].rotation_euler.y=-.45
        head.rotation_euler.y=-.05+.025*math.sin(frame/4*math.tau)
    bpy.context.view_layer.update()

m.configure_materials('Preview');bpy.ops.wm.save_as_mainfile(filepath=str(m.SOURCE/'survey_engineer.blend'))
frames=[('idle',0)]+[('walk',i)for i in range(6)]+[('work',i)for i in range(4)]
for kind,frame in frames:
    pose(kind,frame)
    for mode in ('Color','Normal','AO'):
        m.configure_materials(mode);scene.view_settings.view_transform='Standard'if mode=='Color'else'Raw';scene.view_settings.look='None'
        scene.render.filepath=str(out/(f'engineer_{kind}{frame}_{mode}.png'));bpy.ops.render.render(write_still=True)
    print('WORKER_FRAME',kind,frame,flush=True)
