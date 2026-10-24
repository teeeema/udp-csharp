"""Run with Blender --factory-startup -b --python scripts/convert_visual_assets.py -- PROJECT_ROOT.
Converts licensed source assets staged in tmp/visual-assets into Unity FBX + material metadata.
No downloaded scripts are executed. All material/geometry conversion happens offline.
"""
import bpy,sys,json,re,math
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(sys.argv[-1]);staging=root/'tmp/visual-assets';dest=root/'unity/PredictionDemo/Assets/ThirdParty/MilitaryRange'
assets=[('Abrams',staging/'abrams-tank.blend',45000),('Warehouse',staging/'warehouse/warehouse02.glb',15000)]
for name,limit in [('concrete_road_barrier',12000),('rock_07',6000),('grass_bermuda_01',5000),('wild_rooibos_bush',6000),('barrel_03',3000),('tree_small_02',50000)]:assets.append((name,staging/name/(name+'.gltf'),limit))
def clean(s):return re.sub(r'[^a-zA-Z0-9_]+','_',s).strip('_')
def images_from(socket,seen=None):
 seen=seen or set();result=[]
 for link in socket.links:
  node=link.from_node
  if node in seen:continue
  seen.add(node)
  if node.type=='TEX_IMAGE' and node.image:result.append(node.image)
  else:
   for inp in node.inputs:
    if inp.is_linked:result+=images_from(inp,seen)
 return result
for name,source,limit in assets:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 if source.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
 else:bpy.ops.import_scene.gltf(filepath=str(source))
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH' or (name=='Abrams' and not o.material_slots):bpy.data.objects.remove(o,do_unlink=True)
 meshes=list(bpy.context.scene.objects)
 # Bake evaluated geometry/modifiers into the source model, preserving each material.
 for o in meshes:
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  bpy.ops.object.convert(target='MESH')
 bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=meshes[0]
 bpy.ops.object.join();o=bpy.context.object
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 verts=[o.matrix_world@v.co for v in o.data.vertices]
 lo=Vector([min(v[i] for v in verts) for i in range(3)]);hi=Vector([max(v[i] for v in verts) for i in range(3)])
 center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 if name=='Abrams':center.x=.09664;center.y=.04599
 o.data.transform(o.matrix_world);o.matrix_world=Matrix.Identity(4);o.data.transform(Matrix.Translation(-center));o.name=name
 triangles=sum(len(p.vertices)-2 for p in o.data.polygons)
 if triangles>limit:
  modifier=o.modifiers.new('Offline polygon budget','DECIMATE');modifier.ratio=limit/triangles
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=modifier.name)
 folder=dest/name;folder.mkdir(parents=True,exist_ok=True);(folder/'Textures').mkdir(exist_ok=True)
 exported={};materials=[]
 def save_image(img):
  if img.name in exported:return exported[img.name]
  filename=clean(img.name)+'.png';out=folder/'Textures'/filename
  if max(img.size)>1024:
   factor=1024/max(img.size);img.scale(max(1,round(img.size[0]*factor)),max(1,round(img.size[1]*factor)))
  img.file_format='PNG';img.filepath_raw=str(out);img.save()
  exported[img.name]='Textures/'+filename
  return exported[img.name]
 for slot in o.material_slots:
  m=slot.material
  if not m:continue
  old=m.name;m.name=name+'_'+clean(old)
  node=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.node_tree else None
  color=list(node.inputs['Base Color'].default_value) if node else list(m.diffuse_color)
  data={'name':m.name,'sourceName':old,'color':color,'metallic':float(node.inputs['Metallic'].default_value) if node else 0,'roughness':float(node.inputs['Roughness'].default_value) if node else .75,'alphaClip':bool(node and node.inputs['Alpha'].is_linked),'doubleSided':not m.use_backface_culling,'textures':{}}
  if node:
   for socket,key in [('Base Color','diffuse'),('Normal','normal'),('Roughness','orm')]:
    imgs=images_from(node.inputs[socket])
    if imgs:data['textures'][key]=save_image(imgs[0])
  materials.append(data)
 bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
 stats={'asset':name,'source':str(source.relative_to(root)),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':materials}
 (folder/'materials.json').write_text(json.dumps(stats,indent=2)+'\n');print('CONVERTED',name,stats['triangles'],'triangles',len(materials),'materials',flush=True)
