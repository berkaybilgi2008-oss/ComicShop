"""Blender 4.2+ EEVEE: import GLB, select model, open this script in Text Editor and Run Script.
Sets cel materials on selected meshes, adds an ink shell and a key sun.
Does not export or save automatically. Runtime execution not verified in this environment.
"""
import bpy, bmesh, math
objects=[o for o in bpy.context.selected_objects if o.type=='MESH' and not o.name.endswith('_Ink')]
if not objects:raise RuntimeError('Import ToastRanger_Animated.glb and select its mesh first.')
bpy.context.scene.render.engine='BLENDER_EEVEE_NEXT'
bpy.context.scene.view_settings.view_transform='Standard'
seen=set()
for obj in objects:
 for slot in obj.material_slots:
  mat=slot.material
  if not mat or mat.name in seen:continue
  seen.add(mat.name)
  color=tuple(mat.diffuse_color)
  if mat.use_nodes:
   for node in mat.node_tree.nodes:
    if node.type=='BSDF_PRINCIPLED':color=tuple(node.inputs['Base Color'].default_value);break
  # Give the chosen model its own material; preserve other objects sharing the original.
  source_name=mat.name;mat=mat.copy();mat.name=source_name+'_Cel';slot.material=mat
  mat.use_nodes=True;n=mat.node_tree.nodes;n.clear();link=mat.node_tree.links
  output=n.new('ShaderNodeOutputMaterial');output.location=(650,0)
  emit=n.new('ShaderNodeEmission');emit.location=(440,0)
  diffuse=n.new('ShaderNodeBsdfDiffuse');diffuse.inputs['Color'].default_value=(1,1,1,1);diffuse.location=(-600,0)
  rgb=n.new('ShaderNodeShaderToRGB');rgb.location=(-400,0)
  ramp=n.new('ShaderNodeValToRGB');ramp.location=(-180,0);ramp.color_ramp.interpolation='CONSTANT'
  ramp.color_ramp.elements[0].position=0;ramp.color_ramp.elements[0].color=(.49,.43,.36,1)
  ramp.color_ramp.elements[1].position=.68;ramp.color_ramp.elements[1].color=(1,1,1,1)
  mid=ramp.color_ramp.elements.new(.22);mid.color=(.8,.8,.8,1)
  mul=n.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=color;mul.location=(210,0)
  link.new(diffuse.outputs[0],rgb.inputs[0]);link.new(rgb.outputs[0],ramp.inputs[0]);link.new(ramp.outputs[0],mul.inputs[1]);link.new(mul.outputs[0],emit.inputs['Color']);link.new(emit.outputs[0],output.inputs['Surface'])
 ink=bpy.data.materials.new('ToastRanger_Ink');ink.diffuse_color=(.015,.01,.008,1);ink.use_nodes=True;ink.use_backface_culling=True
 nodes=ink.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');em=nodes.new('ShaderNodeEmission');em.inputs['Color'].default_value=(.015,.01,.008,1);ink.node_tree.links.new(em.outputs[0],out.inputs[0])
 hull=obj.copy();hull.data=obj.data.copy();hull.name=obj.name+'_Ink';bpy.context.collection.objects.link(hull)
 bm=bmesh.new();bm.from_mesh(hull.data)
 blush_slots={i for i,m in enumerate(hull.data.materials) if m and m.name.startswith('Blush')}
 bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index in blush_slots],context='FACES')
 bm.normal_update()
 for v in bm.verts:v.co+=v.normal*.003
 bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(hull.data);bm.free();hull.data.update();hull.data.materials.clear();hull.data.materials.append(ink)
 for face in hull.data.polygons:face.material_index=0
sun=bpy.data.lights.new('ToastRanger_Key','SUN');sun.energy=2
key=bpy.data.objects.new('ToastRanger_Key',sun);bpy.context.collection.objects.link(key);key.rotation_euler=(math.radians(25),math.radians(-30),math.radians(-25))
print('Cel materials ready. Use Rendered viewport with EEVEE. Save As .blend to preserve setup. Run once to avoid duplicate outlines.')
