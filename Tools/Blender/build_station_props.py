"""Original modular station furnishings, rendered as color/normal/AO sprites."""
import importlib.util
from pathlib import Path
import math
import bpy

folder=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('industrial',folder/'build_industrial_assets.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.OUTPUT=m.ROOT/'ArtSource'/'Blender'/'Props'
m.OUTPUT.mkdir(parents=True,exist_ok=True)
b=m.box;c=m.cylinder;p=m.panel

def bunk():
    for x in (-1.65,1.65):
        b('Ivory bunk upright',(x,0,1.65),(.13,.85,3.3),'ivory',.035)
        b('Foot',(x,0,.08),(.3,.95,.16),'teal_dark')
    for z in (.58,2.05):
        b('Bed steel rim',(0,0,z),(3.42,1,.14),'steel')
        b('Padded mattress',(0,-.01,z+.16),(3.25,.88,.22),'ivory_dark',.08)
        b('Teal folded blanket',(.24,-.03,z+.32),(2.15,.88,.13),'teal',.05)
        for x in (-.65,.0,.65): b('Blanket stitch',(x,-.48,z+.32),(.018,.012,.12),'ivory_dark',.004)
        b('Pillow',(-1.2,-.02,z+.37),(.64,.73,.16),'white',.09)
    for z in (.5,.93,1.36,1.79,2.22):c('Ladder rung',(1.36,-.57,z),.036,.47,'steel','X',16)
    for x in (1.13,1.58):b('Ladder rail',(x,-.57,1.35),(.052,.052,2.4),'copper',.017)
    b('Upper canopy',(0,.18,3.25),(3.52,.88,.16),'teal_dark')
    b('Reading light',(-1.35,-.53,2.85),(.32,.08,.11),'glass')

def console():
    b('Work console foot',(0,0,.16),(2.9,1,.22),'steel')
    for x in (-1.12,1.12):b('Cabinet pedestal',(x,0,.78),(.64,.9,1.1),'teal')
    b('Work top',(0,-.08,1.39),(3.15,1.13,.13),'ivory')
    for x in (-.88,.37):
        b('Monitor mount',(x,.18,1.72),(.14,.24,.53),'steel')
        p('Screen bezel',(x,-.17,2.15),1.12,.79,'teal_dark')
        b('Screen glow',(x,-.237,2.16),(.93,.018,.57),'glass_dark',.02)
        for j in range(5):b('Data traces',(x-.12,-.26,1.99+j*.08),(.36+j*.07,.012,.012),'glass',.002)
    for i in range(7):b('Keyboard keys',(-.68+i*.16,-.63,1.49),(.105,.1,.043),'black',.01)
    c('Dial',(1.25,-.56,1.03),.17,.07,'ivory','Y')
    m.gauge((-1.1,-.48,.85),.19)
    for x in (-1.35,1.35):m.bolt((x,-.48,.27))

def crate():
    b('Storage case',(0,0,.76),(1.65,.88,1.36),'ivory_dark',.09)
    for x in (-.67,.67):b('Vertical reinforcement',(x,-.47,.76),(.16,.06,1.29),'teal_dark')
    for z in (.14,1.38):b('Corner reinforcement',(0,-.48,z),(1.6,.07,.15),'steel')
    b('Inset face',(0,-.465,.76),(.97,.06,.8),'teal')
    b('Safety stripe',(0,-.51,.98),(.73,.035,.16),'yellow',.01)
    for x in (-.26,-.05,.16):
        o=b('Black diagonal',(x,-.534,.98),(.075,.008,.17),'black',.002);o.rotation_euler.y=.45
    b('Recessed handle',(0,-.512,.64),(.52,.035,.16),'black')
    b('Handle',(0,-.552,.65),(.4,.07,.058),'steel')
    for x in (-.67,.67):
        for z in (.28,1.24):m.bolt((x,-.52,z),.036)

def planter():
    b('Hydroponic tray',(0,0,.66),(2.9,.95,.73),'ivory')
    b('Growing medium',(0,-.02,1.02),(2.63,.8,.12),'black')
    for x in (-1.15,1.15):b('Planter leg',(x,.1,.24),(.16,.67,.48),'steel')
    for x in (-.96,-.42,.16,.78):
        m.pipe('Plant stem',[(x,0,1.03),(x+.07,0,1.65),(x-.06,.04,2.15)],.024,'green')
        for i in range(4):
            z=1.27+i*.21;sign=-1 if i%2 else 1
            o=m.sphere('Leaf',(x+sign*.18,-.06,z),(.26,.05,.075),'teal' if i%2 else 'green');o.rotation_euler.y=-sign*.6
    m.pipe('Water feed',[(-1.5,.15,.6),(-1.67,.1,.8),(-1.67,.1,1.16),(-1.1,.1,1.16)],.045)
    m.gauge((1.1,-.5,.68),.13)

def vent():
    p('Vent housing',(0,0,1.14),1.64,1.84,'teal_dark')
    m.vent((0,-.08,1.13),13,1.2)
    c('Fan rim',(0,-.14,1.15),.44,.075,'steel','Y')
    c('Fan dark well',(0,-.19,1.15),.37,.045,'black','Y')
    for angle in (0,math.pi*.5):
        o=b('Fan blade',(0,-.231,1.15),(.61,.025,.12),'steel');o.rotation_euler.y=angle
    c('Fan hub',(0,-.255,1.15),.085,.024,'copper','Y',20)

builders={'bunk':bunk,'console':console,'crate':crate,'planter':planter,'vent':vent}
bpy.context.preferences.filepaths.save_version=0
for name,build in builders.items():
    scene=m.setup_scene(512,32);build()
    m.configure_materials('Preview')
    bpy.ops.wm.save_as_mainfile(filepath=str(m.SOURCE/(name+'.blend')))
    for mode in ('Color','Normal','AO'):
        m.configure_materials(mode)
        scene.view_settings.view_transform='Standard' if mode=='Color' else 'Raw'
        scene.view_settings.look='None'
        scene.render.filepath=str(m.OUTPUT/(name+'_'+mode+'.png'))
        bpy.ops.render.render(write_still=True)
    print('PROP_COMPLETED',name,flush=True)
